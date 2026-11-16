using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 访客临时卡（对应 Net 版 VisitorCardPool）：直接取一卡通里已有的访客卡，不维护独立卡池表。
///
/// 取卡口径（业务给定）：
///   ST_CardType.type_sign = 'FK'               → 访客卡类型（Type_No=2/6 都算）
///   ST_Card.Type_No       = ST_CardType.Type_No
///   ST_Card.Person_ID     = ST_Person.Person_ID
///   卡号                   = ST_Person.Card_No
///
/// 可用性：卡正常（Card_State=0）、人员未删、没被未结束的访客单占用。
/// 生命周期：分配 → Card_ValidDate 延长到拜访 End_Date；释放 → Card_ValidDate = 现在（立即作废）。
///
/// 注意：不写 MJ_Person / MJ_PsnDoorRight ——
///   MJ_Person.Acs_Card 是「多卡号换行分隔」字段，覆盖写会把原有卡号冲掉；访客人员的门禁授权本身是常驻的。
/// </summary>
public static class VisitorCardPool
{
    private const string FreeSql = @"
SELECT p.Card_No        AS Card_No,
       p.Person_ID      AS Person_ID,
       c.Type_No        AS Type_No,
       p.Person_Name    AS Person_Name,
       p.Person_No      AS Person_No,
       c.Card_ValidDate AS Card_ValidDate
FROM ST_Card c
JOIN ST_CardType t ON t.Type_No = c.Type_No
JOIN ST_Person p   ON p.Person_ID = c.Person_ID
WHERE t.type_sign = 'FK'
  AND ISNULL(c.Card_State, 0) = 0
  AND p.Is_Del = 0
  AND p.Card_No IS NOT NULL AND p.Card_No <> ''
  AND NOT EXISTS (
      SELECT 1 FROM VisitorApply v
      WHERE v.Empower_Card_No = p.Card_No
        AND v.VisitOrNot IN (0, 1)          -- 待审批也可能已被厂商预占卡号
        AND v.check_outtime IS NULL
        AND v.End_Date >= GETDATE())
ORDER BY p.Card_No";

    /// <summary>分配用：与 FreeSql 同口径，但对 ST_Card 加 UPDLOCK 行锁</summary>
    private const string AllocSql = @"
SELECT p.Card_No        AS Card_No,
       p.Person_ID      AS Person_ID,
       c.Type_No        AS Type_No,
       p.Person_Name    AS Person_Name,
       p.Person_No      AS Person_No,
       c.Card_ValidDate AS Card_ValidDate
FROM ST_Card c WITH (UPDLOCK, ROWLOCK)
JOIN ST_CardType t ON t.Type_No = c.Type_No
JOIN ST_Person p   ON p.Person_ID = c.Person_ID
WHERE t.type_sign = 'FK'
  AND ISNULL(c.Card_State, 0) = 0
  AND p.Is_Del = 0
  AND p.Card_No IS NOT NULL AND p.Card_No <> ''
  AND NOT EXISTS (
      SELECT 1 FROM VisitorApply v
      WHERE v.Empower_Card_No = p.Card_No
        AND v.VisitOrNot IN (0, 1)
        AND v.check_outtime IS NULL
        AND v.End_Date >= GETDATE())
ORDER BY p.Card_No";

    /// <summary>所有访客卡（含被占用的），供运维查看</summary>
    public static List<CardRow> AllCards()
    {
        const string sql = @"
SELECT p.Card_No AS Card_No, p.Person_ID AS Person_ID, c.Type_No AS Type_No,
       p.Person_Name AS Person_Name, p.Person_No AS Person_No, c.Card_ValidDate AS Card_ValidDate,
       CASE WHEN EXISTS (
            SELECT 1 FROM VisitorApply v
            WHERE v.Empower_Card_No = p.Card_No AND v.VisitOrNot IN (0, 1)
              AND v.check_outtime IS NULL AND v.End_Date >= GETDATE()) THEN 1 ELSE 0 END AS InUse,
       (SELECT COUNT(1) FROM VisitorApply v
        WHERE v.Empower_Card_No = p.Card_No AND v.VisitOrNot IN (0, 1)
          AND v.check_outtime IS NULL AND v.End_Date >= GETDATE()) AS InUseCount
FROM ST_Card c
JOIN ST_CardType t ON t.Type_No = c.Type_No
JOIN ST_Person p   ON p.Person_ID = c.Person_ID
WHERE t.type_sign = 'FK' AND ISNULL(c.Card_State, 0) = 0 AND p.Is_Del = 0
ORDER BY p.Card_No";
        return Query(sql);
    }

    /// <summary>可分配的访客卡（供审批/签到页面选择）</summary>
    public static List<CardRow> FreeCards() => Query(FreeSql);

    /// <summary>可分配数量</summary>
    public static int FreeCount() => FreeCards().Count;

    /// <summary>
    /// 分配一张访客卡：占卡 + 延长有效期 + 把卡号写回申请单，全部在同一事务内完成。
    ///   preferredCardNo 为空 → 取第一张可用卡；
    ///   指定卡号 → 必须在该可用集合里，否则报错（不会硬塞被占用的卡）。
    /// </summary>
    public static string? Allocate(string applyNo, DateTime endDate, string? preferredCardNo, out string msg)
    {
        msg = "";
        string want = (preferredCardNo ?? "").Trim();

        using var cn = new SqlConnection(AppConfig.ConnectionString);
        cn.Open();
        using var tx = cn.BeginTransaction();
        try
        {
            // 用 UPDLOCK 行锁串行化“选卡 + 占卡”：
            // 第二个请求会在同一行上阻塞，等第一个提交后再判断（此时占用条件已生效），
            // 因此不会两个人拿到同一张卡。整个动作都在本事务内，回滚即释放。
            var cards = Query(AllocSql, cn, tx);
            CardRow? card;
            if (want.Length > 0)
            {
                card = cards.FirstOrDefault(c => c.Card_No == want);
                if (card == null)
                {
                    tx.Rollback();
                    msg = "指定的访客卡不可用（不存在 / 已作废 / 已被占用）：" + want;
                    return null;
                }
            }
            else
            {
                card = cards.FirstOrDefault();
                if (card == null)
                {
                    tx.Rollback();
                    msg = "暂无可用访客卡";
                    return null;
                }
            }

            // 有效期延长到本次拜访结束时刻
            using (var up = new SqlCommand(
                @"UPDATE ST_Card SET Card_ValidDate = @p0, Update_Time = GETDATE()
                  WHERE Person_ID = @p1 AND Type_No = @p2", cn, tx))
            {
                up.Parameters.AddWithValue("@p0", endDate);
                up.Parameters.AddWithValue("@p1", card.Person_ID);
                up.Parameters.AddWithValue("@p2", card.Type_No);
                if (up.ExecuteNonQuery() == 0)
                {
                    tx.Rollback();
                    msg = "延长卡有效期失败（卡记录已变更）：" + card.Card_No;
                    return null;
                }
            }

            // 占卡：卡号写回申请单，必须在锁内完成，否则释放锁后可能被别人抢走
            using (var occ = new SqlCommand(
                @"UPDATE VisitorApply SET Empower_Card_No = @p0, update_time = GETDATE()
                  WHERE Apply_No = @p1", cn, tx))
            {
                occ.Parameters.AddWithValue("@p0", card.Card_No);
                occ.Parameters.AddWithValue("@p1", applyNo);
                occ.ExecuteNonQuery();
            }

            tx.Commit();
            return card.Card_No;
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { /* 已回滚 */ }
            msg = "分配临时卡失败：" + ex.Message;
            return null;
        }
    }

    /// <summary>不指定卡号的分配</summary>
    public static string? Allocate(string applyNo, DateTime endDate, out string msg)
        => Allocate(applyNo, endDate, null, out msg);

    /// <summary>
    /// 释放：签离/作废后让卡立即失效（按约定不保留原有效期，下次分配时再延长）。
    /// 卡号保留在申请单上，便于追溯“哪次拜访用了哪张卡”。
    /// </summary>
    public static void Release(string? applyNo)
    {
        if (string.IsNullOrEmpty(applyNo)) return;

        using var cn = new SqlConnection(AppConfig.ConnectionString);
        cn.Open();

        string? cardNo;
        using (var q = new SqlCommand("SELECT TOP 1 Empower_Card_No FROM VisitorApply WHERE Apply_No = @p0", cn))
        {
            q.Parameters.AddWithValue("@p0", applyNo);
            var v = q.ExecuteScalar();
            cardNo = v == null || v == DBNull.Value ? null : Convert.ToString(v);
        }
        if (string.IsNullOrEmpty(cardNo)) return;

        using var up = new SqlCommand(
            @"UPDATE ST_Card
              SET Card_ValidDate = GETDATE(), Update_Time = GETDATE()
              WHERE Person_ID = (
                    SELECT TOP 1 p.Person_ID
                    FROM ST_Person p
                    JOIN ST_Card c     ON c.Person_ID = p.Person_ID
                    JOIN ST_CardType t ON t.Type_No = c.Type_No
                    WHERE p.Card_No = @p0 AND t.type_sign = 'FK')", cn);
        up.Parameters.AddWithValue("@p0", cardNo);
        up.ExecuteNonQuery();
    }

    // ---------------- 内部 ----------------

    private static List<CardRow> Query(string sql, SqlConnection? cn = null, SqlTransaction? tx = null)
    {
        var list = new List<CardRow>();
        bool own = cn == null;
        var conn = cn ?? new SqlConnection(AppConfig.ConnectionString);
        try
        {
            if (own) conn.Open();
            using var cmd = new SqlCommand(sql, conn, tx);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new CardRow
                {
                    Card_No = rd["Card_No"] == DBNull.Value ? "" : Convert.ToString(rd["Card_No"]) ?? "",
                    Person_ID = rd["Person_ID"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Person_ID"]),
                    Type_No = rd["Type_No"] == DBNull.Value ? "" : Convert.ToString(rd["Type_No"]) ?? "",
                    Person_Name = rd["Person_Name"] == DBNull.Value ? "" : Convert.ToString(rd["Person_Name"]) ?? "",
                    Person_No = rd["Person_No"] == DBNull.Value ? "" : Convert.ToString(rd["Person_No"]) ?? "",
                    Card_ValidDate = rd["Card_ValidDate"] == DBNull.Value ? null : Convert.ToDateTime(rd["Card_ValidDate"]),
                    InUse = HasColumn(rd, "InUse") && rd["InUse"] != DBNull.Value ? Convert.ToInt32(rd["InUse"]) : 0,
                    InUseCount = HasColumn(rd, "InUseCount") && rd["InUseCount"] != DBNull.Value ? Convert.ToInt32(rd["InUseCount"]) : 0
                });
            }
        }
        finally
        {
            if (own) conn.Dispose();
        }
        return list;
    }

    private static bool HasColumn(System.Data.IDataRecord rd, string name)
    {
        for (int i = 0; i < rd.FieldCount; i++)
            if (string.Equals(rd.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public class CardRow
    {
        public string Card_No { get; set; } = "";
        public int Person_ID { get; set; }
        public string Type_No { get; set; } = "";
        public string Person_Name { get; set; } = "";
        public string Person_No { get; set; } = "";
        public DateTime? Card_ValidDate { get; set; }
        public int InUse { get; set; }
        public int InUseCount { get; set; }
    }
}

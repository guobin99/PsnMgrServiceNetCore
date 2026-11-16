using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers;

/// <summary>
/// 管理端考勤申请审批（Pc 后台，对应 Net 版 Admin_KQ_ApplyController）：查询 / 审批 / 回写厂商考勤表。
/// 审批通过时在同一事务内完成：
///   补签  → 写 KQ_SupplyTime（Supply_Class 1上班 / 2下班）
///   请假  → 按天拆行写 KQ_Leave（跨天每天一条，保留当天时间段）
///   申诉  → 按异常类型/时刻生成对应补签写 KQ_SupplyTime
///   然后 EXEC KQ_pd_CalcDaily 只重算涉及的日期，并给申请人写一条消息中心通知。
/// 说明：按现场要求 KQ_SupplyTimeHistory 保持单写，本控制器不写历史表。
/// </summary>
[ApiController]
[Authorize]
[Route("api/Admin_KQ_Apply")]
public class Admin_KQ_ApplyController : ControllerBase
{
    private const string SelectApply = @"
SELECT ID_KEY, Apply_No, Person_ID, Person_No, Person_Name, Apply_Type,
       Supply_Date, Supply_Time, Supply_Class, Kind_No, Kind_Name,
       Leave_Bgn, Leave_End, Leave_Hour, Leave_Days,
       Appeal_Date, Appeal_Type, Appeal_Time, Reason, CAST(Status AS int) AS Status,
       Audit_User, Audit_Time, Audit_Remark, Target_Table, Target_Ids, Source, Create_Time";

    private readonly AppDbContext _db;

    public Admin_KQ_ApplyController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/Admin_KQ_Apply?status=&type=&bgn=&end=&keyword=&page=1&size=20
    [HttpGet]
    public IActionResult GetList(
        string status = "", string type = "", string bgn = "", string end = "",
        string keyword = "", int page = 1, int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 200) size = 20;

        string where = " WHERE 1=1 ";
        var ps = new List<SqlParameter>();

        if (!string.IsNullOrEmpty(status) && int.TryParse(status, out var st))
        {
            where += " AND Status = @status ";
            ps.Add(new SqlParameter("@status", st));
        }
        if (!string.IsNullOrEmpty(type))
        {
            where += " AND Apply_Type = @type ";
            ps.Add(new SqlParameter("@type", type));
        }
        if (DateTime.TryParse(bgn, out var dt))
        {
            where += " AND Create_Time >= @bgn ";
            ps.Add(new SqlParameter("@bgn", dt.Date));
        }
        if (DateTime.TryParse(end, out dt))
        {
            where += " AND Create_Time < @end ";
            ps.Add(new SqlParameter("@end", dt.Date.AddDays(1)));
        }
        if (!string.IsNullOrEmpty(keyword))
        {
            where += @" AND (Apply_No LIKE @kw OR Person_No LIKE @kw OR Person_Name LIKE @kw
                       OR Reason LIKE @kw OR Kind_Name LIKE @kw OR Appeal_Type LIKE @kw) ";
            ps.Add(new SqlParameter("@kw", "%" + keyword.Trim() + "%"));
        }

        int total = _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(1) AS Value FROM WX_KQ_Apply " + where, ps.ToArray())
            .FirstOrDefault();

        var sql = @"
SELECT * FROM (
    SELECT ID_KEY, Apply_No, Person_ID, Person_No, Person_Name, Apply_Type,
           Supply_Date, Supply_Time, Supply_Class, Kind_No, Kind_Name,
           Leave_Bgn, Leave_End, Leave_Hour, Leave_Days,
           Appeal_Date, Appeal_Type, Appeal_Time, Reason, CAST(Status AS int) AS Status,
           Audit_User, Audit_Time, Audit_Remark, Target_Table, Target_Ids, Source, Create_Time,
           ROW_NUMBER() OVER (ORDER BY Create_Time DESC) AS RowNum
    FROM WX_KQ_Apply " + where + @"
) T
WHERE T.RowNum BETWEEN @start AND @endRow
ORDER BY T.Create_Time DESC";

        var ps2 = new List<SqlParameter>(ps)
        {
            new SqlParameter("@start", (page - 1) * size + 1),
            new SqlParameter("@endRow", page * size)
        };

        var rows = _db.Database.SqlQueryRaw<ApplyAdminRow>(sql, ps2.ToArray()).ToList();

        return Ok(new
        {
            success = true,
            total,
            page,
            size,
            rows = rows.Select(r => new
            {
                id = r.ID_KEY.ToString(),
                applyNo = r.Apply_No ?? "",
                personId = r.Person_ID,
                personNo = r.Person_No ?? "",
                personName = r.Person_Name ?? "",
                type = KqApplyHelper.TypeText(r.Apply_Type),
                typeKey = r.Apply_Type,
                title = TitleOf(r),
                dateRange = RangeOf(r),
                reason = r.Reason ?? "",
                kindName = r.Kind_Name ?? "",
                leaveHour = r.Leave_Hour ?? 0,
                leaveDays = r.Leave_Days ?? 0,
                status = r.Status,
                statusText = KqApplyHelper.StatusText(r.Status),
                auditUser = r.Audit_User ?? "",
                auditTime = r.Audit_Time.HasValue ? r.Audit_Time.Value.ToString("yyyy-MM-dd HH:mm") : "",
                auditRemark = r.Audit_Remark ?? "",
                targetTable = r.Target_Table ?? "",
                targetIds = r.Target_Ids ?? "",
                source = r.Source ?? "",
                time = r.Create_Time.ToString("yyyy-MM-dd HH:mm")
            }).ToList()
        });
    }

    // POST: api/Admin_KQ_Apply/audit
    // body = { applyNo, result: "approve" | "reject", remark, auditUser }
    [HttpPost("audit")]
    public IActionResult Audit([FromBody] KqAuditReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.applyNo))
            return Ok(new { success = false, msg = "申请号不能为空" });

        var apply = _db.Database.SqlQueryRaw<ApplyAdminRow>(
            SelectApply + ", CAST(0 AS bigint) AS RowNum FROM WX_KQ_Apply WHERE Apply_No = @p0",
            new SqlParameter("@p0", req.applyNo)).FirstOrDefault();

        if (apply == null) return Ok(new { success = false, msg = "申请不存在" });
        if (apply.Status != 0) return Ok(new { success = false, msg = "该申请已处理（" + KqApplyHelper.StatusText(apply.Status) + "）" });

        bool approve = string.Equals(req.result, "approve", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(req.result, "1", StringComparison.Ordinal);
        string remark = (req.remark ?? "").Trim();
        if (remark.Length > 200) remark = remark.Substring(0, 200);
        string auditUser = string.IsNullOrEmpty(req.auditUser) ? "管理员" : req.auditUser!.Trim();

        string targetTable = "";
        var targetIds = new List<int>();
        DateTime? calcBgn = null, calcEnd = null;

        using (var cn = new SqlConnection(AppConfig.ConnectionString))
        {
            cn.Open();
            using var tx = cn.BeginTransaction();
            try
            {
                if (approve)
                {
                    if (!WriteBack(cn, tx, apply, targetIds, out targetTable, out calcBgn, out calcEnd, out var err))
                    {
                        tx.Rollback();
                        return Ok(new { success = false, msg = err });
                    }
                }

                using (var up = new SqlCommand(@"
UPDATE WX_KQ_Apply
SET Status = @status, Audit_User = @user, Audit_Time = GETDATE(), Audit_Remark = @remark,
    Target_Table = @table, Target_Ids = @ids
WHERE Apply_No = @applyNo AND Status = 0", cn, tx))
                {
                    up.Parameters.AddWithValue("@status", approve ? 1 : 2);
                    up.Parameters.AddWithValue("@user", auditUser);
                    up.Parameters.AddWithValue("@remark", DbNull(remark));
                    up.Parameters.AddWithValue("@table", DbNull(targetTable));
                    up.Parameters.AddWithValue("@ids", DbNull(string.Join(",", targetIds)));
                    up.Parameters.AddWithValue("@applyNo", apply.Apply_No);
                    up.ExecuteNonQuery();
                }

                // 消息中心通知申请人
                string title = approve ? "考勤申请已通过" : "考勤申请已驳回";
                string content = string.Format("您提交的{0}（{1}）{2}。{3}",
                    KqApplyHelper.TypeText(apply.Apply_Type),
                    RangeOf(apply),
                    approve ? "已通过，考勤记录已更新" : "已被驳回",
                    string.IsNullOrEmpty(remark) ? "" : "审批意见：" + remark);
                if (content.Length > 500) content = content.Substring(0, 500);

                using (var msg = new SqlCommand(@"
INSERT INTO WX_Message (Person_ID, Msg_Type, Title, Content, Is_Read, Create_Time)
VALUES (@pid, 'attendance', @title, @content, 0, GETDATE())", cn, tx))
                {
                    msg.Parameters.AddWithValue("@pid", apply.Person_ID);
                    msg.Parameters.AddWithValue("@title", title);
                    msg.Parameters.AddWithValue("@content", content);
                    msg.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* 已回滚 */ }
                return Ok(new { success = false, msg = "审批失败：" + ex.Message });
            }
        }

        // 事务外触发考勤重算：只算受影响的日期段
        string calcTip = "";
        if (approve && calcBgn.HasValue && calcEnd.HasValue)
        {
            try
            {
                // KQ_pd_CalcDaily 在远程/大库上单日可能跑 1-2 分钟，默认 30s 命令超时必然中断，
                // 这里单独放宽（与 attendance/daily 的补算保持同一口径）
                _db.Database.SetCommandTimeout(600);
                _db.Database.ExecuteSqlRaw("EXEC KQ_pd_CalcDaily @DateBgn, @DateEnd",
                    new SqlParameter("@DateBgn", calcBgn.Value.ToString("yyyy-MM-dd")),
                    new SqlParameter("@DateEnd", calcEnd.Value.ToString("yyyy-MM-dd")));
            }
            catch (Exception ex)
            {
                calcTip = "（考勤重算失败，请在考勤管理中手动重算：" + ex.Message + "）";
            }
        }

        return Ok(new
        {
            success = true,
            status = approve ? 1 : 2,
            statusText = KqApplyHelper.StatusText(approve ? 1 : 2),
            targetTable,
            targetIds = string.Join(",", targetIds),
            calcTip
        });
    }

    // ================= 回写厂商考勤表 =================

    private static bool WriteBack(SqlConnection cn, SqlTransaction tx, ApplyAdminRow a, List<int> ids,
        out string targetTable, out DateTime? calcBgn, out DateTime? calcEnd, out string err)
    {
        targetTable = "";
        calcBgn = null;
        calcEnd = null;
        err = "";

        if (a.Apply_Type == KqApplyHelper.TypeResign || a.Apply_Type == KqApplyHelper.TypeAppeal)
        {
            string? date = a.Apply_Type == KqApplyHelper.TypeResign ? a.Supply_Date : a.Appeal_Date;
            string? time = a.Apply_Type == KqApplyHelper.TypeResign ? a.Supply_Time : a.Appeal_Time;
            short cls = a.Supply_Class ?? 1;

            if (!DateTime.TryParse(date, out var d)) { err = "申请日期不正确，无法回写考勤"; return false; }
            d = d.Date;

            // 申诉：未填应打卡时刻时按异常类型/时段推断（12:00 前算上班，之后算下班）
            if (a.Apply_Type == KqApplyHelper.TypeAppeal)
            {
                if (string.IsNullOrEmpty(time))
                    time = (a.Appeal_Type ?? "").Contains("早退") ? "17:30" : "08:30";
                if (string.IsNullOrEmpty(a.Supply_Time) && !string.IsNullOrEmpty(time))
                    cls = string.CompareOrdinal(time, "12:00") < 0 ? (short)1 : (short)2;
                if ((a.Appeal_Type ?? "").Contains("早退")) cls = 2;
            }
            if (string.IsNullOrEmpty(time)) time = cls == 2 ? "17:30" : "08:30";
            if (time.Length > 5) time = time.Substring(0, 5);

            // 幂等保护：同一人同一天同一类型已有补签，就不再重复写
            using (var dupCmd = new SqlCommand(
                "SELECT COUNT(1) FROM KQ_SupplyTime WHERE Person_ID = @p0 AND Supply_Date = @p1 AND Supply_Class = @p2", cn, tx))
            {
                dupCmd.Parameters.AddWithValue("@p0", a.Person_ID);
                dupCmd.Parameters.AddWithValue("@p1", d.ToString("yyyy-MM-dd"));
                dupCmd.Parameters.AddWithValue("@p2", cls);
                if (Convert.ToInt32(dupCmd.ExecuteScalar() ?? 0) > 0)
                {
                    err = "该日期该类型已存在补签记录，无需重复写入";
                    return false;
                }
            }

            string reason = (a.Reason ?? "").Trim();
            if (reason.Length == 0) reason = a.Apply_Type == KqApplyHelper.TypeAppeal ? "考勤异常申诉补签" : "微信补签";
            if (reason.Length > 40) reason = reason.Substring(0, 40);

            using (var ins = new SqlCommand(@"
INSERT INTO KQ_SupplyTime (Person_ID, Supply_Date, Supply_Time, Supply_Reason, Supply_Class, Remark, User_No, Update_Date)
VALUES (@pid, @date, @time, @reason, @cls, @remark, 'WECHAT', GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx))
            {
                ins.Parameters.AddWithValue("@pid", a.Person_ID);
                ins.Parameters.AddWithValue("@date", d.ToString("yyyy-MM-dd"));
                ins.Parameters.AddWithValue("@time", time);
                ins.Parameters.AddWithValue("@reason", reason);
                ins.Parameters.AddWithValue("@cls", cls);
                ins.Parameters.AddWithValue("@remark", a.Apply_Type == KqApplyHelper.TypeAppeal ? "微信申诉补签" : "微信补签");
                ids.Add(Convert.ToInt32(ins.ExecuteScalar() ?? 0));
            }

            targetTable = "KQ_SupplyTime";
            calcBgn = calcEnd = d;
            return true;
        }

        if (a.Apply_Type == KqApplyHelper.TypeLeave)
        {
            if (!DateTime.TryParse(a.Leave_Bgn, out var b) || !DateTime.TryParse(a.Leave_End, out var e) || e <= b)
            { err = "请假时间段不正确，无法回写考勤"; return false; }

            // 按天拆行：跨天请假每天一条，保留当天的时间段与小时数
            DateTime cursor = b;
            int guard = 0;
            while (cursor < e && guard++ < 400)
            {
                DateTime dayEnd = cursor.Date.AddDays(1);
                DateTime segEnd = dayEnd < e ? dayEnd : e;
                double hours = Math.Round((segEnd - cursor).TotalHours, 2);
                if (hours <= 0) break;

                using var ins = new SqlCommand(@"
INSERT INTO KQ_Leave (Person_ID, Kind_No, Leave_Date, Leave_TimeBgn, Leave_TimeEnd, Leave_Hour, IsOutLeave, Remark, User_No, Update_Date)
VALUES (@pid, @kind, @date, @bgn, @end, @hour, @out, @remark, 'WECHAT', GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx);
                ins.Parameters.AddWithValue("@pid", a.Person_ID);
                ins.Parameters.AddWithValue("@kind", DbNull(a.Kind_No));
                ins.Parameters.AddWithValue("@date", cursor.ToString("yyyy-MM-dd"));
                ins.Parameters.AddWithValue("@bgn", cursor.ToString("yyyy-MM-dd HH:mm"));
                ins.Parameters.AddWithValue("@end", segEnd.ToString("yyyy-MM-dd HH:mm"));
                ins.Parameters.AddWithValue("@hour", (float)hours);
                ins.Parameters.AddWithValue("@out", b.Date != e.Date);   // 跨天视为外出/多日请假
                ins.Parameters.AddWithValue("@remark", Trim("请假 " + (a.Apply_No ?? ""), 100));
                ids.Add(Convert.ToInt32(ins.ExecuteScalar() ?? 0));

                cursor = segEnd;
            }

            if (ids.Count == 0) { err = "请假时间不足一天内的有效时段，无法回写考勤"; return false; }

            targetTable = "KQ_Leave";
            calcBgn = b.Date;
            calcEnd = e.Date;
            return true;
        }

        err = "未知的申请类型：" + a.Apply_Type;
        return false;
    }

    private static string TitleOf(ApplyAdminRow r)
    {
        switch (r.Apply_Type)
        {
            case KqApplyHelper.TypeResign:
                return (r.Supply_Class == 2 ? "下班" : "上班") + "补签";
            case KqApplyHelper.TypeLeave:
                return "请假（" + (string.IsNullOrEmpty(r.Kind_Name) ? (r.Kind_No ?? "") : r.Kind_Name) + "）";
            default:
                return string.IsNullOrEmpty(r.Appeal_Type) ? "考勤申诉" : r.Appeal_Type!;
        }
    }

    private static string RangeOf(ApplyAdminRow r)
    {
        switch (r.Apply_Type)
        {
            case KqApplyHelper.TypeResign:
                return ((r.Supply_Date ?? "") + " " + (r.Supply_Time ?? "")).Trim();
            case KqApplyHelper.TypeLeave:
                string range = (r.Leave_Bgn ?? "") + " ~ " + (r.Leave_End ?? "");
                if (r.Leave_Days.HasValue && r.Leave_Days.Value > 1) range += string.Format("（{0}天）", r.Leave_Days.Value);
                return range;
            default:
                return ((r.Appeal_Date ?? "") + " " + (r.Appeal_Time ?? "")).Trim();
        }
    }

    private static string Trim(string? s, int max)
    {
        s = s ?? "";
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static object DbNull(object? v) => v ?? (object)DBNull.Value;

    public class ApplyAdminRow
    {
        public long ID_KEY { get; set; }
        public string? Apply_No { get; set; }
        public int Person_ID { get; set; }
        public string? Person_No { get; set; }
        public string? Person_Name { get; set; }
        public string? Apply_Type { get; set; }
        public string? Supply_Date { get; set; }
        public string? Supply_Time { get; set; }
        public short? Supply_Class { get; set; }
        public string? Kind_No { get; set; }
        public string? Kind_Name { get; set; }
        public string? Leave_Bgn { get; set; }
        public string? Leave_End { get; set; }
        public double? Leave_Hour { get; set; }
        public int? Leave_Days { get; set; }
        public string? Appeal_Date { get; set; }
        public string? Appeal_Type { get; set; }
        public string? Appeal_Time { get; set; }
        public string? Reason { get; set; }
        public int Status { get; set; }
        public string? Audit_User { get; set; }
        public DateTime? Audit_Time { get; set; }
        public string? Audit_Remark { get; set; }
        public string? Target_Table { get; set; }
        public string? Target_Ids { get; set; }
        public string? Source { get; set; }
        public DateTime Create_Time { get; set; }
        public long RowNum { get; set; }
    }
}

public class KqAuditReq
{
    public string? applyNo { get; set; }
    public string? result { get; set; }
    public string? remark { get; set; }
    public string? auditUser { get; set; }
}

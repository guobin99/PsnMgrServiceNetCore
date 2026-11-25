using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 个人账户：柜台与客服使用的账户全景页，聚合人员、卡、余额、限额、状态、统一流水与操作留痕。
/// 只读账户与流水，写操作仅改密；销户、冻结、挂失只做入口跳转，不在此页执行。
/// </summary>
[Route("api/card/personal")]
[ApiController]
public class CardPersonalController : ControllerBase
{
    private readonly AuditService _audit;

    public CardPersonalController(AuditService audit)
    {
        _audit = audit;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;
    private string ClientIp => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    /// <summary>GET api/card/personal：人员账户检索（只返回已开户人员）。</summary>
    [HttpGet]
    [RequirePermission("card:personal:view")]
    public IActionResult Search(
        [FromQuery] string? keyword,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] int? cardState = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { "ISNULL(p.Is_Del, 0) = 0" };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(p.Person_No LIKE @kw OR p.Person_Name LIKE @kw OR p.Card_No LIKE @kw OR p.id_number LIKE @kw)");
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
        }
        if (cardState != null)
        {
            where.Add("ISNULL(c.Card_State, 0) = @state");
            parameters.Add(SqlSession.P("@state", cardState.Value));
        }
        if (deptId != null)
        {
            if (includeChildren)
            {
                var deptNo = db.Scalar("SELECT Dept_No FROM dbo.ST_Department WHERE Dept_ID = @id",
                    SqlSession.P("@id", deptId.Value)) as string ?? string.Empty;
                where.Add("(p.Dept_ID = @deptId OR d.Dept_No LIKE @deptLike)");
                parameters.Add(SqlSession.P("@deptId", deptId.Value));
                parameters.Add(SqlSession.P("@deptLike", deptNo + "%"));
            }
            else
            {
                where.Add("p.Dept_ID = @deptId");
                parameters.Add(SqlSession.P("@deptId", deptId.Value));
            }
        }

        var filter = string.Join(" AND ", where);
        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card c INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID " +
                                 "LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID WHERE " + filter, parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardPersonalRow>(@"
SELECT * FROM (
    SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
           d.Dept_No AS deptNo, d.Dept_Name AS deptName, p.Card_No AS cardNo,
           RTRIM(c.Type_No) AS typeNo, ct.Type_Name AS typeName,
           c.Card_State AS cardState, c.Card_Balance AS balance, c.Card_SmallBalance AS smallBalance,
           c.Subsidy_Fund AS subsidyFund, c.IC_Balance AS icBalance, c.Card_ValidDate AS validDate,
           ROW_NUMBER() OVER (ORDER BY p.Person_No) AS RowNum
      FROM dbo.ST_Card c
      INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID
      LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
      LEFT JOIN dbo.ST_CardType ct ON RTRIM(ct.Type_No) = RTRIM(c.Type_No)
     WHERE " + filter + @"
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        foreach (var item in items)
        {
            item.cardStateText = CardState.Text(item.cardState);
        }
        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/personal/{personId}：账户全景（余额、限额、状态、有效期）。</summary>
    [HttpGet("{personId:int}")]
    [RequirePermission("card:personal:view")]
    public IActionResult Detail(int personId)
    {
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var recent = db.Query<CardLogRow>(@"
SELECT TOP 10 ID_KEY AS idKey, Op_Type AS opType, Before_State AS beforeState, After_State AS afterState,
       Refund_Amount AS refundAmount, Modify_Fee AS modifyFee, Op_User AS opUser, Op_Time AS opTime, Remark AS remark
  FROM dbo.BS_CardLog WHERE Person_ID = @id ORDER BY Op_Time DESC", SqlSession.P("@id", personId));

        var state = snapshot.cardState ?? 0;
        var balance = snapshot.balance ?? 0m;
        var icBalance = snapshot.icBalance ?? 0m;
        var mismatch = Math.Abs(balance - icBalance) > 0.009m;

        return Api.Ok(new
        {
            person = new
            {
                personId,
                personNo = snapshot.personNo,
                personName = snapshot.personName,
                deptId = snapshot.deptId,
                deptNo = snapshot.deptNo,
                deptName = snapshot.deptName,
                cardNo = snapshot.cardNo,
                cardSn = snapshot.cardSn,
            },
            account = new
            {
                typeNo = snapshot.typeNo,
                typeName = snapshot.typeName,
                cardState = state,
                cardStateText = CardState.Text(state),
                allowedActions = CardState.AllowedActions(state),
                newDate = snapshot.newDate,
                validDate = snapshot.validDate,
                cardUserNo = snapshot.cardUserNo,
                updateTime = snapshot.updateTime,
                lockReasonText = snapshot.lockReasonText,
                lockTime = snapshot.lockTime,
                pwdConfigured = string.IsNullOrWhiteSpace(snapshot.cardPassword) ? 0 : 1,
            },
            amounts = new
            {
                balance,
                smallBalance = snapshot.smallBalance ?? 0m,
                subsidyFund = snapshot.subsidyFund ?? 0m,
                icBalance,
                foregift = snapshot.foregift ?? 0m,
                charge = snapshot.charge ?? 0m,
                balanceMismatch = mismatch,
                mismatchNote = mismatch ? "库内余额与卡内余额不一致，请核对读卡结果" : null,
            },
            limits = new
            {
                minBalance = snapshot.minBalance ?? 0m,
                sfMaxFund = snapshot.sfMaxFund ?? 0m,
                sfMaxDayFund = snapshot.sfMaxDayFund ?? 0m,
                sfLimitTimes = snapshot.sfLimitTimes ?? 0,
                maxIcFunds = snapshot.maxIcFunds ?? 0m,
                smallDiscount = snapshot.smallDiscount ?? 100,
                smallAccountMax = snapshot.smallAccountMax ?? 0m,
                blackDays = snapshot.blackDays ?? 182,
            },
            recentLogs = recent,
        });
    }

    /// <summary>
    /// GET api/card/personal/{personId}/flows：统一流水（充值、取款、小账户转入、补贴、消费、换卡、销户退款）。
    /// 单次查询时间跨度不超过 1 年；按类型分查后合并，避免对全部流水表做全表扫描。
    /// </summary>
    [HttpGet("{personId:int}/flows")]
    [RequirePermission("card:personal:view")]
    public IActionResult Flows(
        int personId,
        [FromQuery] string? startTime,
        [FromQuery] string? endTime,
        [FromQuery] string? types = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        var end = CardRules.ParseDate(endTime) ?? DateTime.Today.AddDays(1).AddSeconds(-1);
        var start = CardRules.ParseDate(startTime) ?? end.AddYears(-1);
        if (start > end) return Api.Fail(400, "40005", "开始时间不能晚于结束时间");
        if (end.AddYears(-1) > start) return Api.Fail(400, "40009", "单次查询时间跨度不能超过 1 年");

        var selected = ParseFlowTypes(types);
        using var db = SqlAccess.Open();

        var union = new StringBuilder();
        var parameters = new List<SqlParameter> { SqlSession.P("@id", personId), SqlSession.P("@start", start), SqlSession.P("@end", end) };

        if (selected.Contains("SUPPLY"))
            union.Append(@"
UNION ALL SELECT f.Supply_Date AS flowTime, 'SUPPLY' AS flowType, f.Supply_Fund AS amount, 'IN' AS direction,
       f.Card_Balance AS balance, '操作后余额' AS balanceLabel, f.User_No AS opUser, f.Remark AS remark, 'ST_SupplyFund' AS sourceTable
  FROM dbo.ST_SupplyFund f WHERE f.Person_ID = @id AND f.Supply_Date BETWEEN @start AND @end");
        if (selected.Contains("TAKE"))
            union.Append(@"
UNION ALL SELECT f.Take_Date, 'TAKE', f.Take_Fund, 'OUT', f.Card_Balance, '操作后余额', f.User_No, f.Remark, 'ST_TakeFund'
  FROM dbo.ST_TakeFund f WHERE f.Person_ID = @id AND f.Take_Date BETWEEN @start AND @end");
        if (selected.Contains("TRANSFER"))
            union.Append(@"
UNION ALL SELECT f.Transfer_Date, 'TRANSFER', f.Transfer_Fund, 'INNER', f.Card_Balance, '操作后余额', f.User_No, f.Remark, 'ST_TransferAccount'
  FROM dbo.ST_TransferAccount f WHERE f.Person_ID = @id AND f.Transfer_Date BETWEEN @start AND @end");
        if (selected.Contains("SUBSIDY"))
            union.Append(@"
UNION ALL SELECT f.Subsidy_Date, 'SUBSIDY', f.Subsidy_Fund, 'IN', NULL, NULL, f.User_No, f.Remark, 'ST_Subsidy'
  FROM dbo.ST_Subsidy f WHERE f.Person_ID = @id AND f.Subsidy_Date BETWEEN @start AND @end");
        if (selected.Contains("CONSUME"))
            union.Append(@"
UNION ALL SELECT f.Consume_Time, 'CONSUME', f.Consume_Fund, 'OUT', f.Card_Balance, '操作后余额', NULL, f.Remark, 'XF_Consume'
  FROM dbo.XF_Consume f WHERE f.Person_ID = @id AND f.Consume_Time BETWEEN @start AND @end");
        if (selected.Contains("ALTER"))
            union.Append(@"
UNION ALL SELECT f.Alter_Date, 'ALTER', f.Alter_Fee, 'OUT', NULL, NULL, f.User_No, f.Remark, 'ST_AlterCard'
  FROM dbo.ST_AlterCard f WHERE f.Person_ID = @id AND f.Alter_Date BETWEEN @start AND @end");
        if (selected.Contains("DELETE"))
            union.Append(@"
UNION ALL SELECT f.Del_Date, 'DELETE', f.Fact_Fund, 'OUT', NULL, NULL, f.User_No, f.Remark, 'ST_DeleteCard'
  FROM dbo.ST_DeleteCard f WHERE f.Person_ID = @id AND f.Del_Date BETWEEN @start AND @end");

        if (union.Length == 0) return Api.Ok(new { total = 0, items = Array.Empty<object>(), start, end, types = selected.ToArray() });
        var body = union.ToString().TrimStart().Substring("UNION ALL".Length);

        var total = db.ScalarInt("SELECT COUNT(1) FROM (" + body + ") f", parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardFlowRow>(@"
SELECT * FROM (
    SELECT f.*, ROW_NUMBER() OVER (ORDER BY f.flowTime DESC) AS RowNum FROM (" + body + @") f
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        foreach (var item in items) item.flowTypeName = FlowTypeText(item.flowType);
        return Api.Ok(new { total, items, start, end, types = selected.ToArray(), rangeDays = (end - start).TotalDays });
    }

    /// <summary>POST api/card/personal/{personId}/change-password：改密（先写卡、后写库）。</summary>
    [HttpPost("{personId:int}/change-password")]
    [RequirePermission("card:personal:changePwd")]
    public IActionResult ChangePassword(int personId, [FromBody] CardChangePwdRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var state = snapshot.cardState ?? 0;
        if (state == CardState.Frozen)
            return Api.Fail(409, "40910", "冻结状态不允许修改卡密码，请先解冻");

        if (!CardCrypto.IsValidPassword(request.NewPassword, false, out var pwdError))
            return Api.Fail(400, "40012", pwdError);
        if (CardCrypto.PasswordEquals(snapshot.cardPassword, request.NewPassword))
            return Api.Fail(400, "40012", "新密码不能与当前密码相同");

        // 旧密码或复核码
        var locked = CardBiz.PasswordLockedUntil(db, personId);
        if (locked != null) return Api.Fail(403, "40301", $"卡密码连续错误已锁定，请于 {locked.Value:HH:mm:ss} 后重试");

        var authOk = !string.IsNullOrWhiteSpace(request.AuthCode) && CardCrypto.VerifySuperPassword(db, request.AuthCode, out _);
        if (!authOk)
        {
            if (string.IsNullOrWhiteSpace(snapshot.cardPassword))
                return Api.Fail(403, "40301", "该卡未设置密码，请使用复核码验证身份");
            if (!CardCrypto.PasswordEquals(snapshot.cardPassword, request.OldPassword))
            {
                var maxFail = CardBiz.ConfigInt(db, "Card.Pwd.MaxFail", 5);
                CardBiz.PasswordFailed(db, personId, maxFail, CardBiz.ConfigInt(db, "Card.Pwd.LockMinutes", 10), out var fails, out var until);
                return Api.Fail(403, "40301", until == null
                    ? $"原密码不正确，还可尝试 {Math.Max(0, maxFail - fails)} 次"
                    : $"原密码连续错误，已锁定至 {until.Value:HH:mm:ss}");
            }
        }

        // 写卡与写库不在同一事务内：参数为 1 时必须先由读卡器写入成功
        var syncCard = CardBiz.ConfigInt(db, "Card.Pwd.SyncCard", 1) == 1;
        if (syncCard && !request.CardWritten)
            return Api.Fail(422, "42201", "请先通过读卡器写入新密码，写卡成功后再提交；BS 不直接操作硬件");

        var newPwd = CardCrypto.EncryptPassword(request.NewPassword);
        db.Begin();
        try
        {
            var affected = db.Exec("UPDATE dbo.ST_Card SET Card_Password = @pwd, Update_Time = GETDATE() WHERE Person_ID = @id",
                SqlSession.P("@pwd", newPwd), SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(404, "40914", "账户不存在，请刷新后重试");
            }

            CardBiz.PasswordPassed(db, personId);
            CardBiz.TouchExt(db, personId, snapshot.cardNo, "CHANGEPWD", userNo, null, null, null, null);
            CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "CHANGEPWD",
                BeforeState = state,
                AfterState = state,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(request.Remark ?? "修改卡密码", 200),
                Ext1 = request.CardWritten ? "卡内已同步" : "仅写库",
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-personal", "change-password", "card", personId.ToString(CultureInfo.InvariantCulture), null, null, false, ex.Message);
            return Api.ServerError("写库失败，卡内可能已改为新密码，请重试：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-personal", "change-password", "card", personId.ToString(CultureInfo.InvariantCulture),
            new { passwordChanged = true }, new { cardWritten = request.CardWritten });
        return Api.Ok(new { personId, cardWritten = request.CardWritten, synced = request.CardWritten && syncCard },
            request.CardWritten ? "卡密码已修改，卡内与库内已同步" : "卡密码已修改，但卡内未同步，终端可能仍使用旧密码");
    }

    /// <summary>GET api/card/personal/{personId}/export：导出账户摘要与近一年流水（xlsx）。</summary>
    [HttpGet("{personId:int}/export")]
    [RequirePermission("card:personal:export")]
    public IActionResult Export(int personId)
    {
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var end = DateTime.Now;
        var start = end.AddYears(-1);
        var flows = db.Query<CardFlowRow>(@"
SELECT * FROM (
    SELECT f.Supply_Date AS flowTime, 'SUPPLY' AS flowType, f.Supply_Fund AS amount, 'IN' AS direction,
           f.Card_Balance AS balance, f.User_No AS opUser, f.Remark AS remark, 'ST_SupplyFund' AS sourceTable
      FROM dbo.ST_SupplyFund f WHERE f.Person_ID = @id AND f.Supply_Date BETWEEN @start AND @end
    UNION ALL
    SELECT f.Take_Date, 'TAKE', f.Take_Fund, 'OUT', f.Card_Balance, f.User_No, f.Remark, 'ST_TakeFund'
      FROM dbo.ST_TakeFund f WHERE f.Person_ID = @id AND f.Take_Date BETWEEN @start AND @end
    UNION ALL
    SELECT f.Transfer_Date, 'TRANSFER', f.Transfer_Fund, 'INNER', f.Card_Balance, f.User_No, f.Remark, 'ST_TransferAccount'
      FROM dbo.ST_TransferAccount f WHERE f.Person_ID = @id AND f.Transfer_Date BETWEEN @start AND @end
    UNION ALL
    SELECT f.Subsidy_Date, 'SUBSIDY', f.Subsidy_Fund, 'IN', NULL, f.User_No, f.Remark, 'ST_Subsidy'
      FROM dbo.ST_Subsidy f WHERE f.Person_ID = @id AND f.Subsidy_Date BETWEEN @start AND @end
    UNION ALL
    SELECT f.Consume_Time, 'CONSUME', f.Consume_Fund, 'OUT', f.Card_Balance, NULL, f.Remark, 'XF_Consume'
      FROM dbo.XF_Consume f WHERE f.Person_ID = @id AND f.Consume_Time BETWEEN @start AND @end
    UNION ALL
    SELECT f.Alter_Date, 'ALTER', f.Alter_Fee, 'OUT', NULL, f.User_No, f.Remark, 'ST_AlterCard'
      FROM dbo.ST_AlterCard f WHERE f.Person_ID = @id AND f.Alter_Date BETWEEN @start AND @end
    UNION ALL
    SELECT f.Del_Date, 'DELETE', f.Fact_Fund, 'OUT', NULL, f.User_No, f.Remark, 'ST_DeleteCard'
      FROM dbo.ST_DeleteCard f WHERE f.Person_ID = @id AND f.Del_Date BETWEEN @start AND @end
) t ORDER BY t.flowTime DESC",
            SqlSession.P("@id", personId), SqlSession.P("@start", start), SqlSession.P("@end", end));

        var rows = new List<string[]>
        {
            new[] { "个人账户明细", string.Empty, string.Empty },
            new[] { "人员编号", snapshot.personNo ?? string.Empty, "姓名", snapshot.personName ?? string.Empty },
            new[] { "部门", snapshot.deptName ?? string.Empty, "卡号", snapshot.cardNo ?? string.Empty },
            new[] { "卡类", (snapshot.typeNo ?? string.Empty) + " " + (snapshot.typeName ?? string.Empty), "卡状态", CardState.Text(snapshot.cardState) },
            new[] { "主账户余额", (snapshot.balance ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), "小账户余额", (snapshot.smallBalance ?? 0m).ToString("0.00", CultureInfo.InvariantCulture) },
            new[] { "补贴余额", (snapshot.subsidyFund ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), "IC 余额", (snapshot.icBalance ?? 0m).ToString("0.00", CultureInfo.InvariantCulture) },
            new[] { "押金", (snapshot.foregift ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), "卡费", (snapshot.charge ?? 0m).ToString("0.00", CultureInfo.InvariantCulture) },
            new[] { "有效期", snapshot.validDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, "导出时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
            new[] { "操作员", CurrentUser, "流水区间", start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 至 " + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            Array.Empty<string>(),
            new[] { "时间", "类型", "金额", "方向", "余额快照", "操作员", "备注", "来源表" },
        };

        foreach (var flow in flows.Take(5000))
        {
            rows.Add(new[]
            {
                flow.flowTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                FlowTypeText(flow.flowType),
                (flow.amount ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
                DirectionText(flow.direction),
                (flow.balance ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
                flow.opUser ?? string.Empty,
                flow.remark ?? string.Empty,
                flow.sourceTable ?? string.Empty,
            });
        }

        var bytes = CardXlsx.Build("个人账户", rows, new[] { 2 });
        var fileName = $"个人账户_{(snapshot.personNo ?? personId.ToString(CultureInfo.InvariantCulture))}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

        _audit.LogAndSave(HttpContext, "card-personal", "export", "card", personId.ToString(CultureInfo.InvariantCulture),
            null, new { flowCount = flows.Count, fileName });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /* ---------------- 内部方法 ---------------- */

    private static readonly string[] AllFlowTypes = { "SUPPLY", "TAKE", "TRANSFER", "SUBSIDY", "CONSUME", "ALTER", "DELETE" };

    private static HashSet<string> ParseFlowTypes(string? types)
    {
        if (string.IsNullOrWhiteSpace(types)) return new HashSet<string>(AllFlowTypes, StringComparer.OrdinalIgnoreCase);
        var set = types.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToUpperInvariant())
            .Where(t => AllFlowTypes.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return set.Count == 0 ? new HashSet<string>(AllFlowTypes, StringComparer.OrdinalIgnoreCase) : set;
    }

    private static string FlowTypeText(string? type)
    {
        return (type ?? string.Empty).ToUpperInvariant() switch
        {
            "SUPPLY" => "充值",
            "TAKE" => "取款",
            "TRANSFER" => "小账户转入",
            "SUBSIDY" => "补贴发放",
            "CONSUME" => "消费",
            "ALTER" => "换卡",
            "DELETE" => "销户退款",
            _ => type ?? string.Empty,
        };
    }

    private static string DirectionText(string? direction)
    {
        return (direction ?? string.Empty).ToUpperInvariant() switch
        {
            "IN" => "入",
            "OUT" => "出",
            "INNER" => "内部",
            _ => direction ?? string.Empty,
        };
    }
}

public class CardPersonalRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public string? cardNo { get; set; }
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? cardState { get; set; }
    public string? cardStateText { get; set; }
    public decimal? balance { get; set; }
    public decimal? smallBalance { get; set; }
    public decimal? subsidyFund { get; set; }
    public decimal? icBalance { get; set; }
    public DateTime? validDate { get; set; }
}

public class CardFlowRow
{
    public DateTime? flowTime { get; set; }
    public string? flowType { get; set; }
    public string? flowTypeName { get; set; }
    public decimal? amount { get; set; }
    public string? direction { get; set; }
    public decimal? balance { get; set; }
    public string? balanceLabel { get; set; }
    public string? opUser { get; set; }
    public string? remark { get; set; }
    public string? sourceTable { get; set; }
}

public class CardChangePwdRequest
{
    public string? OldPassword { get; set; }
    public string? AuthCode { get; set; }
    public string? NewPassword { get; set; }
    public bool CardWritten { get; set; }
    public string? Remark { get; set; }
}

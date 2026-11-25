using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 卡片状态类业务：冻结/解冻、挂失/解挂、换卡/补卡。
/// 所有状态变更使用带前置条件的 UPDATE，以受影响行数判断是否成功，禁止「先查后改」。
/// 冻结与挂失共用 ST_LockCard，解冻与解挂共用 ST_UnLockCard，动作类型以 BS_CardLog.Op_Type 为准。
/// </summary>
[Route("api/card/cards")]
[ApiController]
public class CardCardsController : ControllerBase
{
    private readonly AuditService _audit;

    public CardCardsController(AuditService audit)
    {
        _audit = audit;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;
    private string ClientIp => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private const string CardSelect = @"
SELECT c.Person_ID          AS personId,
       p.Person_No          AS personNo,
       p.Person_Name        AS personName,
       p.Dept_ID            AS deptId,
       d.Dept_No            AS deptNo,
       d.Dept_Name          AS deptName,
       p.Card_No            AS cardNo,
       p.Card_SN            AS cardSn,
       p.mobile             AS mobile,
       RTRIM(c.Type_No)     AS typeNo,
       ct.Type_Name         AS typeName,
       c.Card_State         AS cardState,
       c.Card_Balance       AS balance,
       c.Card_SmallBalance  AS smallBalance,
       c.IC_Balance         AS icBalance,
       c.Card_Foregift      AS foregift,
       c.Card_Charge        AS charge,
       c.Card_NewDate       AS newDate,
       c.Card_ValidDate     AS validDate,
       c.User_No            AS cardUserNo,
       c.Update_Time        AS updateTime,
       ext.Lock_Reason_Code AS lockReasonCode,
       ext.Lock_Reason_Text AS lockReasonText,
       ext.Lock_Time        AS lockTime,
       ext.Last_Op_Type     AS lastOpType,
       ext.Last_Op_Time     AS lastOpTime,
       ext.Last_Op_User     AS lastOpUser,
       (SELECT MAX(l.Lock_Date) FROM dbo.ST_LockCard l WHERE l.Person_ID = c.Person_ID) AS lastLockTime,
       (SELECT MAX(u.UnLock_Date) FROM dbo.ST_UnLockCard u WHERE u.Person_ID = c.Person_ID) AS lastUnlockTime,
       (SELECT COUNT(1) FROM dbo.ST_AlterCard a WHERE a.Person_ID = c.Person_ID) AS alterCount
  FROM dbo.ST_Card c
  INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID
  LEFT  JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT  JOIN dbo.ST_CardType ct ON RTRIM(ct.Type_No) = RTRIM(c.Type_No)
  LEFT  JOIN dbo.BS_CardExt ext ON ext.Person_ID = c.Person_ID";

    /// <summary>GET api/card/cards：卡片列表，冻结与换卡页面共用。</summary>
    [HttpGet]
    [RequirePermission("card:freeze:view")]
    public IActionResult List(
        [FromQuery] string? keyword,
        [FromQuery] int? cardState,
        [FromQuery] string? typeNo,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { "ISNULL(p.Is_Del, 0) = 0" };

        AppendCardFilter(db, where, parameters, keyword, cardState, typeNo, deptId, includeChildren, from, to);
        var filter = string.Join(" AND ", where);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card c INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID " +
                                 "LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID WHERE " + filter, parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardCardRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.updateTime DESC, q.personId) AS RowNum
      FROM (" + CardSelect + " WHERE " + filter + @") q
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        foreach (var item in items)
        {
            item.cardStateText = CardState.Text(item.cardState);
            item.allowedActions = CardState.AllowedActions(item.cardState);
        }
        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/cards/loss-list：挂失清单，默认只查状态 1。</summary>
    [HttpGet("loss-list")]
    [RequirePermission("card:cardLoss:view")]
    public IActionResult LossList(
        [FromQuery] string? keyword,
        [FromQuery] bool allStates = false,
        [FromQuery] string? typeNo="",
        [FromQuery] int? deptId=null,
        [FromQuery] bool includeChildren = true,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { "ISNULL(p.Is_Del, 0) = 0" };
        if (!allStates) where.Add("ISNULL(c.Card_State, 0) = 1");

        AppendCardFilter(db, where, parameters, keyword, null, typeNo, deptId, includeChildren, from, to);
        var filter = string.Join(" AND ", where);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card c INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID " +
                                 "LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID WHERE " + filter, parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardCardRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.lastLockTime DESC, q.personId) AS RowNum
      FROM (" + CardSelect + " WHERE " + filter + @") q
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        var remindDays = CardBiz.ConfigInt(db, "Card.Loss.RemindDays", 30);
        foreach (var item in items)
        {
            item.cardStateText = CardState.Text(item.cardState);
            item.allowedActions = CardState.AllowedActions(item.cardState);
            if (item.lastLockTime != null)
            {
                item.lostDays = (int)Math.Floor((DateTime.Now - item.lastLockTime.Value).TotalDays);
                item.longTerm = item.lostDays >= remindDays;
            }
            item.lostChannel = db.Scalar(
                "SELECT TOP 1 Remark FROM dbo.BS_CardLog WHERE Person_ID = @id AND Op_Type = 'LOSS' ORDER BY Op_Time DESC",
                SqlSession.P("@id", item.personId)) as string;
        }
        return Api.Ok(new { total, items, remindDays });
    }

    /// <summary>GET api/card/cards/{personId}/lock-history：冻结解冻记录（以 BS_CardLog 为动作凭证）。</summary>
    [HttpGet("{personId:int}/lock-history")]
    [RequirePermission("card:freeze:view")]
    public IActionResult LockHistory(int personId)
    {
        using var db = SqlAccess.Open();
        var items = db.Query<CardLogRow>(@"
SELECT ID_KEY AS idKey, Person_ID AS personId, Card_No AS cardNo, Op_Type AS opType,
       Before_State AS beforeState, After_State AS afterState, Op_User AS opUser, Op_Time AS opTime,
       Remark AS remark, Ext1 AS ext1, Ext2 AS ext2
  FROM dbo.BS_CardLog
 WHERE Person_ID = @id AND Op_Type IN ('FREEZE', 'UNFREEZE', 'LOSS', 'UNLOSS')
 ORDER BY Op_Time DESC", SqlSession.P("@id", personId));

        var lockRows = db.Query<CardLockRow>(@"
SELECT ID_KEY AS idKey, Lock_Date AS actionTime, User_No AS userNo, Remark AS remark
  FROM dbo.ST_LockCard WHERE Person_ID = @id ORDER BY Lock_Date DESC", SqlSession.P("@id", personId));
        var unlockRows = db.Query<CardLockRow>(@"
SELECT ID_KEY AS idKey, UnLock_Date AS actionTime, User_No AS userNo, Remark AS remark
  FROM dbo.ST_UnLockCard WHERE Person_ID = @id ORDER BY UnLock_Date DESC", SqlSession.P("@id", personId));

        return Api.Ok(new
        {
            items,
            lockRecords = lockRows,
            unlockRecords = unlockRows,
            lastLockTime = lockRows.Count > 0 ? lockRows[0].actionTime : null,
            lastUnlockTime = unlockRows.Count > 0 ? unlockRows[0].actionTime : null,
        });
    }

    /// <summary>POST api/card/cards/{personId}/freeze：冻结（仅状态 0）。</summary>
    [HttpPost("{personId:int}/freeze")]
    [RequirePermission("card:freeze:freeze")]
    public IActionResult Freeze(int personId, [FromBody] CardFreezeRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var reason = BuildReason(request.ReasonCode, request.ReasonText);
        if (reason == null) return Api.Fail(400, "40005", "请选择冻结原因");

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId && previous.opType == "FREEZE")
            return Api.Ok(new
            {
                personId, cardNo = previous.cardNo, beforeState = previous.beforeState,
                afterState = previous.afterState, lockTime = previous.opTime, logId = previous.idKey, duplicate = true,
            }, "重复提交，返回首次冻结结果");

        var state = snapshot.cardState ?? 0;
        if (state == CardState.Frozen)
            return Api.Ok(new { personId, cardNo = snapshot.cardNo, beforeState = 2, afterState = 2, lockTime = snapshot.lockTime, logId = 0, duplicate = true }, "该卡已处于冻结状态");
        if (state != CardState.Normal)
            return Api.Fail(409, "40910", $"当前卡状态为「{CardState.Text(state)}」，只有正常状态可以冻结；挂失卡请先解挂");

        var verify = VerifyCardPassword(db, personId, snapshot, request.Password, request.AuthCode);
        if (verify != null) return verify;

        long logId = 0;
        var lockTime = DateTime.Now;
        db.Begin();
        try
        {
            var affected = db.Exec("UPDATE dbo.ST_Card SET Card_State = 2, Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = 0",
                SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40910", "卡状态已变化，请刷新后重试");
            }

            db.Exec("INSERT INTO dbo.ST_LockCard (Person_ID, Lock_Date, User_No, Remark) VALUES (@id, GETDATE(), @user, @remark)",
                SqlSession.P("@id", personId), SqlSession.P("@user", userNo), SqlSession.P("@remark", CardBiz.Cut(reason, 50)));

            CardBiz.TouchExt(db, personId, snapshot.cardNo, "FREEZE", userNo, 2,
                request.ReasonCode, reason, lockTime);

            logId = CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "FREEZE",
                BeforeState = state,
                AfterState = 2,
                Balance = snapshot.balance,
                SmallBalance = snapshot.smallBalance,
                Foregift = snapshot.foregift,
                Charge = snapshot.charge,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(reason, 200),
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-freeze", "freeze", "card", personId.ToString(CultureInfo.InvariantCulture), snapshot, request, false, ex.Message);
            return Api.ServerError("冻结失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-freeze", "freeze", "card", personId.ToString(CultureInfo.InvariantCulture),
            new { cardState = state }, new { cardState = 2, reason });
        return Api.Ok(new { personId, cardNo = snapshot.cardNo, beforeState = state, afterState = 2, lockTime, logId }, "账户已冻结");
    }

    /// <summary>POST api/card/cards/{personId}/unfreeze：解冻（仅状态 2）。</summary>
    [HttpPost("{personId:int}/unfreeze")]
    [RequirePermission("card:freeze:unfreeze")]
    public IActionResult Unfreeze(int personId, [FromBody] CardFreezeRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var reason = BuildReason(request.ReasonCode, request.ReasonText);
        if (reason == null) return Api.Fail(400, "40005", "请填写解冻原因");

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId && previous.opType == "UNFREEZE")
            return Api.Ok(new
            {
                personId, cardNo = previous.cardNo, beforeState = previous.beforeState,
                afterState = previous.afterState, lockTime = previous.opTime, logId = previous.idKey, duplicate = true,
            }, "重复提交，返回首次解冻结果");

        var state = snapshot.cardState ?? 0;
        if (state != CardState.Frozen)
            return Api.Fail(409, "40910", $"当前卡状态为「{CardState.Text(state)}」，只有冻结状态可以解冻");

        var verify = VerifyCardPassword(db, personId, snapshot, request.Password, request.AuthCode);
        if (verify != null) return verify;

        var warning = string.Empty;
        var lastLock = db.Scalar("SELECT MAX(Lock_Date) FROM dbo.ST_LockCard WHERE Person_ID = @id", SqlSession.P("@id", personId)) as DateTime?;
        var lastUnlock = db.Scalar("SELECT MAX(UnLock_Date) FROM dbo.ST_UnLockCard WHERE Person_ID = @id", SqlSession.P("@id", personId)) as DateTime?;
        if (lastLock == null || (lastUnlock != null && lastUnlock > lastLock))
            warning = "无对应冻结记录的解冻，已在操作日志中标注";

        long logId = 0;
        var lockTime = DateTime.Now;
        db.Begin();
        try
        {
            var affected = db.Exec("UPDATE dbo.ST_Card SET Card_State = 0, Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = 2",
                SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40910", "卡状态已变化，请刷新后重试");
            }

            db.Exec("INSERT INTO dbo.ST_UnLockCard (Person_ID, UnLock_Date, User_No, Remark) VALUES (@id, GETDATE(), @user, @remark)",
                SqlSession.P("@id", personId), SqlSession.P("@user", userNo), SqlSession.P("@remark", CardBiz.Cut(reason, 50)));

            CardBiz.TouchExt(db, personId, snapshot.cardNo, "UNFREEZE", userNo, null, null, null, null);

            logId = CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "UNFREEZE",
                BeforeState = state,
                AfterState = 0,
                Balance = snapshot.balance,
                SmallBalance = snapshot.smallBalance,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(reason, 200),
                Ext1 = CardBiz.Cut(warning, 100),
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-freeze", "unfreeze", "card", personId.ToString(CultureInfo.InvariantCulture), snapshot, request, false, ex.Message);
            return Api.ServerError("解冻失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-freeze", "unfreeze", "card", personId.ToString(CultureInfo.InvariantCulture),
            new { cardState = state }, new { cardState = 0, reason });
        return Api.Ok(new { personId, cardNo = snapshot.cardNo, beforeState = state, afterState = 0, lockTime, logId, warning }, "账户已解冻");
    }

    /// <summary>POST api/card/cards/freeze-batch：批量冻结，逐条提交，部分失败不回滚已成功项。</summary>
    [HttpPost("freeze-batch")]
    [RequirePermission("card:freeze:freeze")]
    public IActionResult FreezeBatch([FromBody] CardFreezeBatchRequest request)
    {
        var userNo = CurrentUser;
        var ids = (request.PersonIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return Api.Fail(400, "40005", "请选择要冻结的人员");
        using var settings = SqlAccess.Open();
        var limit = CardBiz.ConfigInt(settings, "Card.Freeze.BatchLimit", 200);
        if (ids.Length > limit) return Api.Fail(400, "40009", $"批量冻结单次上限 {limit} 条，请分批执行");

        var reason = BuildReason(request.ReasonCode, request.ReasonText);
        if (reason == null) return Api.Fail(400, "40005", "请选择冻结原因");

        var details = new List<object>();
        var success = 0;
        var failed = 0;

        foreach (var personId in ids)
        {
            using var db = SqlAccess.Open();
            var snapshot = CardBiz.Snapshot(db, personId);
            if (snapshot == null)
            {
                failed++;
                details.Add(new { personId, success = false, message = "该人员尚未开户" });
                continue;
            }
            if ((snapshot.cardState ?? 0) != CardState.Normal)
            {
                failed++;
                details.Add(new { personId, personNo = snapshot.personNo, personName = snapshot.personName,
                    success = false, message = "卡状态为「" + CardState.Text(snapshot.cardState) + "」，跳过" });
                continue;
            }

            try
            {
                db.Begin();
                var affected = db.Exec("UPDATE dbo.ST_Card SET Card_State = 2, Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = 0",
                    SqlSession.P("@id", personId));
                if (affected == 0)
                {
                    db.Rollback();
                    failed++;
                    details.Add(new { personId, personNo = snapshot.personNo, personName = snapshot.personName, success = false, message = "卡状态已变化" });
                    continue;
                }
                db.Exec("INSERT INTO dbo.ST_LockCard (Person_ID, Lock_Date, User_No, Remark) VALUES (@id, GETDATE(), @user, @remark)",
                    SqlSession.P("@id", personId), SqlSession.P("@user", userNo), SqlSession.P("@remark", CardBiz.Cut("批量冻结：" + reason, 50)));
                CardBiz.TouchExt(db, personId, snapshot.cardNo, "FREEZE", userNo, 2, request.ReasonCode, reason, DateTime.Now);
                CardBiz.WriteLog(db, new CardLogEntry
                {
                    PersonId = personId,
                    CardNo = snapshot.cardNo,
                    OpType = "FREEZE",
                    BeforeState = 0,
                    AfterState = 2,
                    Balance = snapshot.balance,
                    SmallBalance = snapshot.smallBalance,
                    Source = "BATCH",
                    OpUser = userNo,
                    ClientIp = ClientIp,
                    Remark = CardBiz.Cut("批量冻结：" + reason, 200),
                });
                db.Commit();
                success++;
                details.Add(new { personId, personNo = snapshot.personNo, personName = snapshot.personName, success = true, message = "已冻结" });
            }
            catch (Exception ex)
            {
                db.Rollback();
                failed++;
                details.Add(new { personId, personNo = snapshot.personNo, personName = snapshot.personName, success = false, message = ex.Message });
            }
        }

        _audit.LogAndSave(HttpContext, "card-freeze", "freeze-batch", "card", null, null,
            new { total = ids.Length, success, failed, reason });
        return Api.Ok(new { total = ids.Length, success, failed, details }, $"批量冻结完成：成功 {success} 条，失败 {failed} 条");
    }

    /// <summary>GET api/card/cards/{personId}/change-context：换卡上下文（旧卡、卡类、余额）。</summary>
    [HttpGet("{personId:int}/change-context")]
    [RequirePermission("card:cardChange:view")]
    public IActionResult ChangeContext(int personId)
    {
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var state = snapshot.cardState ?? 0;
        var isIcCard = (snapshot.typeNo ?? string.Empty).ToUpperInvariant().Contains("IC");
        return Api.Ok(new
        {
            personId,
            personNo = snapshot.personNo,
            personName = snapshot.personName,
            deptName = snapshot.deptName,
            oldCardNo = snapshot.cardNo,
            oldCardSn = snapshot.cardSn,
            typeNo = snapshot.typeNo,
            typeName = snapshot.typeName,
            cardState = state,
            cardStateText = CardState.Text(state),
            balance = snapshot.balance ?? 0m,
            smallBalance = snapshot.smallBalance ?? 0m,
            foregift = snapshot.foregift ?? 0m,
            validDate = snapshot.validDate,
            isIcCard,
            canChange = state == CardState.Normal || state == CardState.Lost,
            canReplace = state == CardState.Lost,
            lastLockTime = db.Scalar("SELECT MAX(Lock_Date) FROM dbo.ST_LockCard WHERE Person_ID = @id", SqlSession.P("@id", personId)),
            allowedActions = CardState.AllowedActions(state),
        });
    }

    /// <summary>GET api/card/cards/new-card-check：新卡号可用性（换卡页面即时校验）。</summary>
    [HttpGet("new-card-check")]
    [RequirePermission("card:cardChange:view")]
    public IActionResult NewCardCheck([FromQuery] string? cardNo, [FromQuery] int personId, [FromQuery] string? oldCardNo)
    {
        var no = (cardNo ?? string.Empty).Trim();
        if (no.Length == 0 || no.Length > 12)
            return Api.Ok(new { cardNo = no, available = false, reason = "卡号长度必须在 1 至 12 位之间" });

        using var db = SqlAccess.Open();
        var old = (oldCardNo ?? string.Empty).Trim();
        if (old.Length == 0)
        {
            old = db.Scalar("SELECT Card_No FROM dbo.ST_Person WHERE Person_ID = @id", SqlSession.P("@id", personId)) as string ?? string.Empty;
        }
        if (string.Equals(no, old.Trim(), StringComparison.Ordinal))
            return Api.Ok(new { cardNo = no, available = false, reason = "新卡号不能与旧卡号相同" });

        var ownerId = CardBiz.CardOwner(db, no, personId);
        if (ownerId > 0)
        {
            var owner = db.Scalar("SELECT Person_Name FROM dbo.ST_Person WHERE Person_ID = @id", SqlSession.P("@id", ownerId)) as string;
            return Api.Ok(new { cardNo = no, available = false, reason = $"卡号已被 {owner} 使用" });
        }
        if (CardBiz.IsCardDisabled(db, no))
            return Api.Ok(new { cardNo = no, available = false, reason = "新卡号已禁用" });
        if (CardBiz.IsCashierCard(db, no))
            return Api.Ok(new { cardNo = no, available = false, reason = "新卡号是出纳卡，不能作为人员卡" });

        var black = CardBiz.BlacklistedSince(db, no, 182);
        return Api.Ok(new
        {
            cardNo = no,
            available = true,
            reason = black == null ? string.Empty : "该卡号在黑名单中，换卡后会再次进入黑名单",
            blacklisted = black != null,
        });
    }

    /// <summary>POST api/card/cards/{personId}/change：换卡或补卡（同一事务）。</summary>
    [HttpPost("{personId:int}/change")]
    [RequirePermission("card:cardChange:change")]
    public IActionResult Change(int personId, [FromBody] CardChangeRequest request)
    {
        var userNo = CurrentUser;
        var newCardNo = (request.NewCardNo ?? string.Empty).Trim();
        var newCardSn = (request.NewCardSn ?? string.Empty).Trim();
        var changeType = string.Equals(request.ChangeType, "REPLACE", StringComparison.OrdinalIgnoreCase) ? "REPLACE" : "CHANGE";
        var alterFee = request.AlterFee ?? 0m;

        if (newCardNo.Length == 0 || newCardNo.Length > 12) return Api.Fail(400, "40008", "新卡号长度必须在 1 至 12 位之间");
        if (newCardSn.Length > 12) return Api.Fail(400, "40008", "新卡序列长度不能超过 12 位");
        if (!CardBiz.IsValidMoney(alterFee, false, out var feeError)) return Api.Fail(400, "40009", "工本费" + feeError);
        if (!CardCrypto.IsValidPassword(request.NewPassword, true, out var pwdError)) return Api.Fail(400, "40012", pwdError);

        using var db = SqlAccess.Open();

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId && previous.opType == "CHANGE")
            return Api.Ok(new { personId, oldCardNo = previous.cardNo, newCardNo = previous.newCardNo, alterFee = previous.modifyFee,
                alterId = previous.idKey, duplicate = true }, "重复提交，返回首次换卡结果");

        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var state = snapshot.cardState ?? 0;
        var currentCardNo = (snapshot.cardNo ?? string.Empty).Trim();
        if (state != CardState.Normal && state != CardState.Lost)
            return Api.Fail(409, "40910", $"当前卡状态为「{CardState.Text(state)}」，冻结状态不允许换卡，请先解冻");
        if (changeType == "REPLACE" && state != CardState.Lost)
            return Api.Fail(409, "40910", "补卡只允许挂失状态，请先办理挂失");
        if (string.Equals(newCardNo, currentCardNo, StringComparison.Ordinal))
            return Api.Fail(400, "40008", "新卡号不能与旧卡号相同");

        if (!string.IsNullOrWhiteSpace(request.OldCardNo) &&
            !string.Equals(request.OldCardNo.Trim(), currentCardNo, StringComparison.Ordinal))
            return Api.Fail(409, "40919", "卡号已被其他操作变更，请刷新后按最新卡号重新发起");

        var ownerId = CardBiz.CardOwner(db, newCardNo, personId);
        if (ownerId > 0) return Api.Fail(409, "40911", "新卡号已被占用");
        if (CardBiz.IsCardDisabled(db, newCardNo)) return Api.Fail(409, "40911", "新卡号已禁用");
        if (CardBiz.IsCashierCard(db, newCardNo)) return Api.Fail(409, "40918", "出纳卡不能作为人员卡");

        // 补卡可免密，换卡必须校验旧卡密码
        if (changeType == "CHANGE")
        {
            var verify = VerifyCardPassword(db, personId, snapshot, request.Password, request.AuthCode);
            if (verify != null) return verify;
        }

        var encryptedCardNo = CardCrypto.EncryptCardNo(newCardNo);
        var newPwd = string.IsNullOrWhiteSpace(request.NewPassword) ? null : CardCrypto.EncryptPassword(request.NewPassword);
        var isIcCard = (snapshot.typeNo ?? string.Empty).ToUpperInvariant().Contains("IC");
        var remark = CardBiz.Cut((request.Remark ?? string.Empty).Trim().Length == 0
            ? (changeType == "REPLACE" ? "补卡" : "换卡")
            : request.Remark!.Trim(), 200);
        long alterId = 0;

        db.Begin();
        try
        {
            // 1 换卡记录
            alterId = Convert.ToInt64(db.Scalar(@"
INSERT INTO dbo.ST_AlterCard (Person_ID, Alter_Date, Card_No, NewCard_No, Alter_Fee, Alter_Type, User_No, Remark)
OUTPUT INSERTED.ID_KEY
VALUES (@id, GETDATE(), @oldCardNo, @newCardNo, @fee, 0, @user, @remark)",
                SqlSession.P("@id", personId),
                SqlSession.P("@oldCardNo", CardBiz.Cut(currentCardNo, 12) ?? string.Empty),
                SqlSession.P("@newCardNo", newCardNo),
                SqlSession.P("@fee", alterFee),
                SqlSession.P("@user", userNo),
                SqlSession.P("@remark", CardBiz.Cut(remark, 50))) ?? 0L, CultureInfo.InvariantCulture);

            // 2 人员卡号与加密卡号：WHERE 带旧卡号，防止并发换卡导致卡号错位
            var affected = db.Exec(@"UPDATE dbo.ST_Person
                                        SET Card_No = @newCardNo, Card_SN = @cardSn, C_PSD = @psd, update_time = GETDATE()
                                      WHERE Person_ID = @id AND RTRIM(ISNULL(Card_No, '')) = @oldCardNo",
                SqlSession.P("@newCardNo", newCardNo),
                SqlSession.P("@cardSn", newCardSn.Length == 0 ? null : newCardSn),
                SqlSession.P("@psd", encryptedCardNo),
                SqlSession.P("@id", personId),
                SqlSession.P("@oldCardNo", currentCardNo));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40919", "卡号已被其他操作变更，请刷新后重试");
            }

            // 3 IC 卡清小账户并恢复状态（只对卡类编号含 IC 的卡执行，否则会误删余额）
            if (isIcCard)
            {
                db.Exec(@"UPDATE dbo.ST_Card SET Card_SmallBalance = 0, Card_State = 0, Update_Time = GETDATE()
                           WHERE Person_ID = @id", SqlSession.P("@id", personId));
            }

            // 4 旧卡入黑名单
            if (currentCardNo.Length > 0)
            {
                db.Exec(@"INSERT INTO dbo.ST_BlackPerson (Person_ID, Card_No, Del_Date, Remark)
                          VALUES (@id, @cardNo, GETDATE(), @remark)",
                    SqlSession.P("@id", personId),
                    SqlSession.P("@cardNo", CardBiz.Cut(currentCardNo, 12)),
                    SqlSession.P("@remark", CardBiz.Cut(CardBiz.Config(db, "Card.Change.BlackRemark", "换卡产生"), 50)));
            }

            // 5 可选重置密码
            if (newPwd != null)
            {
                db.Exec("UPDATE dbo.ST_Card SET Card_Password = @pwd, Update_Time = GETDATE() WHERE Person_ID = @id",
                    SqlSession.P("@pwd", newPwd), SqlSession.P("@id", personId));
            }

            CardBiz.TouchExt(db, personId, newCardNo, "CHANGE", userNo, null, null, null, null);
            CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = currentCardNo,
                NewCardNo = newCardNo,
                OpType = "CHANGE",
                BeforeState = state,
                AfterState = isIcCard ? 0 : state,
                Balance = snapshot.balance,
                SmallBalance = isIcCard ? 0m : snapshot.smallBalance,
                ModifyFee = alterFee,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = remark,
                Ext1 = changeType,
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-change", "change", "card", personId.ToString(CultureInfo.InvariantCulture), snapshot, request, false, ex.Message);
            return Api.ServerError("换卡失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-change", changeType.ToLowerInvariant(), "card", personId.ToString(CultureInfo.InvariantCulture),
            new { cardNo = currentCardNo }, new { cardNo = newCardNo, alterFee, alterId });
        return Api.Ok(new
        {
            personId,
            oldCardNo = currentCardNo,
            newCardNo,
            alterFee,
            alterId,
            cardState = isIcCard ? 0 : state,
            smallBalance = isIcCard ? 0m : snapshot.smallBalance,
        }, changeType == "REPLACE" ? "补卡完成，原挂失记录保留" : "换卡完成，旧卡号已进入黑名单");
    }

    /// <summary>GET api/card/alter-records：换卡记录查询。</summary>
    [HttpGet("/api/card/alter-records")]
    [RequirePermission("card:cardChange:view")]
    public IActionResult AlterRecords(
        [FromQuery] string? keyword,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { "1 = 1" };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(p.Person_No LIKE @kw OR p.Person_Name LIKE @kw OR a.Card_No LIKE @kw OR a.NewCard_No LIKE @kw)");
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
        }
        var fromDate = CardRules.ParseDate(from);
        if (fromDate != null)
        {
            where.Add("a.Alter_Date >= @from");
            parameters.Add(SqlSession.P("@from", fromDate.Value.Date));
        }
        var toDate = CardRules.ParseDate(to);
        if (toDate != null)
        {
            where.Add("a.Alter_Date < @to");
            parameters.Add(SqlSession.P("@to", toDate.Value.Date.AddDays(1)));
        }
        var filter = string.Join(" AND ", where);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_AlterCard a LEFT JOIN dbo.ST_Person p ON p.Person_ID = a.Person_ID WHERE " + filter,
            parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardAlterRow>(@"
SELECT * FROM (
    SELECT a.ID_KEY AS idKey, a.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
           a.Alter_Date AS alterDate, a.Card_No AS cardNo, a.NewCard_No AS newCardNo,
           a.Alter_Fee AS alterFee, a.Alter_Type AS alterType, a.User_No AS userNo, a.Remark AS remark,
           ROW_NUMBER() OVER (ORDER BY a.Alter_Date DESC, a.ID_KEY DESC) AS RowNum
      FROM dbo.ST_AlterCard a
      LEFT JOIN dbo.ST_Person p ON p.Person_ID = a.Person_ID
     WHERE " + filter + @"
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>POST api/card/cards/{personId}/loss：挂失（仅状态 0；状态 1 幂等）。</summary>
    [HttpPost("{personId:int}/loss")]
    [RequirePermission("card:cardLoss:loss")]
    public IActionResult Loss(int personId, [FromBody] CardLossRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var reason = BuildReason(request.ReasonCode, request.ReasonText);
        if (reason == null) return Api.Fail(400, "40005", "请选择挂失原因");

        var channel = string.Equals(request.Channel, "PHONE", StringComparison.OrdinalIgnoreCase) ? "PHONE" : "WINDOW";
        if (channel == "PHONE" && string.IsNullOrWhiteSpace(request.Contact))
            return Api.Fail(400, "40013", "电话受理必须填写联系方式");

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId && previous.opType == "LOSS")
            return Api.Ok(new { personId, cardNo = previous.cardNo, beforeState = previous.beforeState,
                afterState = previous.afterState, lockTime = previous.opTime, logId = previous.idKey, duplicate = true },
                "重复提交，返回首次挂失结果");

        var state = snapshot.cardState ?? 0;
        if (state == CardState.Lost)
        {
            var lockTime0 = db.Scalar("SELECT MAX(Lock_Date) FROM dbo.ST_LockCard WHERE Person_ID = @id", SqlSession.P("@id", personId));
            return Api.Fail(409, "40913", $"该卡已挂失（挂失时间：{(lockTime0 is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss") : "未知")}），无需重复挂失");
        }
        if (state != CardState.Normal)
            return Api.Fail(409, "40910", $"当前卡状态为「{CardState.Text(state)}」，冻结状态请先解冻再挂失");

        var verify = VerifyCardPassword(db, personId, snapshot, request.Password, request.AuthCode);
        if (verify != null) return verify;

        var channelText = channel == "PHONE" ? "电话" : "窗口";
        var logRemark = channelText + "受理";
        if (!string.IsNullOrWhiteSpace(request.Contact)) logRemark += "，联系方式：" + request.Contact!.Trim();
        if (!string.IsNullOrWhiteSpace(request.FoundNote)) logRemark += "，" + request.FoundNote!.Trim();

        long logId = 0;
        var lockTime = DateTime.Now;
        db.Begin();
        try
        {
            var affected = db.Exec("UPDATE dbo.ST_Card SET Card_State = 1, Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = 0",
                SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40910", "卡状态已变化，请刷新后重试");
            }

            db.Exec("INSERT INTO dbo.ST_LockCard (Person_ID, Lock_Date, User_No, Remark) VALUES (@id, GETDATE(), @user, @remark)",
                SqlSession.P("@id", personId), SqlSession.P("@user", userNo), SqlSession.P("@remark", CardBiz.Cut(reason, 50)));

            CardBiz.TouchExt(db, personId, snapshot.cardNo, "LOSS", userNo, 1, request.ReasonCode, reason, lockTime);

            logId = CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "LOSS",
                BeforeState = state,
                AfterState = 1,
                Balance = snapshot.balance,
                SmallBalance = snapshot.smallBalance,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(logRemark, 200),
                Ext1 = request.PoliceReport ? "已报警" : "未报警",
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-loss", "loss", "card", personId.ToString(CultureInfo.InvariantCulture), snapshot, request, false, ex.Message);
            return Api.ServerError("挂失失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-loss", "loss", "card", personId.ToString(CultureInfo.InvariantCulture),
            new { cardState = state }, new { cardState = 1, reason, channel });
        return Api.Ok(new { personId, cardNo = snapshot.cardNo, beforeState = state, afterState = 1, lockTime, logId }, "挂失成功，该卡已禁止消费");
    }

    /// <summary>POST api/card/cards/{personId}/unloss：解挂（仅状态 1，已补卡禁止解挂）。</summary>
    [HttpPost("{personId:int}/unloss")]
    [RequirePermission("card:cardLoss:unloss")]
    public IActionResult Unloss(int personId, [FromBody] CardUnlossRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId && previous.opType == "UNLOSS")
            return Api.Ok(new { personId, cardNo = previous.cardNo, beforeState = previous.beforeState,
                afterState = previous.afterState, logId = previous.idKey, duplicate = true }, "重复提交，返回首次解挂结果");

        var state = snapshot.cardState ?? 0;
        if (state == CardState.Normal) return Api.Fail(409, "40913", "该卡当前为正常状态，无需解挂");
        if (state != CardState.Lost)
            return Api.Fail(409, "40910", $"当前卡状态为「{CardState.Text(state)}」，只有挂失状态可以解挂");

        var reason = CardBiz.Cut((request.ReasonText ?? string.Empty).Trim().Length == 0 ? "解挂" : request.ReasonText!.Trim(), 200);

        long logId = 0;
        db.Begin();
        try
        {
            // 补卡检查与状态更新在同一事务内，防止检查通过后另一操作员立即补卡
            var lastLock = db.Scalar("SELECT MAX(Lock_Date) FROM dbo.ST_LockCard WITH (UPDLOCK, HOLDLOCK) WHERE Person_ID = @id",
                SqlSession.P("@id", personId)) as DateTime?;
            if (lastLock != null)
            {
                var replaced = db.ScalarInt(@"
SELECT COUNT(1) FROM dbo.ST_AlterCard
 WHERE Person_ID = @id AND Alter_Date > @lastLock AND ISNULL(NewCard_No, '') <> ''",
                    SqlSession.P("@id", personId), SqlSession.P("@lastLock", lastLock.Value));
                if (replaced > 0)
                {
                    db.Rollback();
                    return Api.Fail(409, "40920", "该卡已补办，原卡不可恢复，无需解挂");
                }
            }

            var affected = db.Exec("UPDATE dbo.ST_Card SET Card_State = 0, Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = 1",
                SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40910", "卡状态已变化，请刷新后重试");
            }

            db.Exec("INSERT INTO dbo.ST_UnLockCard (Person_ID, UnLock_Date, User_No, Remark) VALUES (@id, GETDATE(), @user, @remark)",
                SqlSession.P("@id", personId), SqlSession.P("@user", userNo), SqlSession.P("@remark", CardBiz.Cut(reason, 50)));

            CardBiz.TouchExt(db, personId, snapshot.cardNo, "UNLOSS", userNo, null, null, null, null);

            logId = CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "UNLOSS",
                BeforeState = state,
                AfterState = 0,
                Balance = snapshot.balance,
                SmallBalance = snapshot.smallBalance,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(reason + (string.IsNullOrWhiteSpace(request.FoundNote) ? string.Empty : "；找回经过：" + request.FoundNote!.Trim()), 200),
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-loss", "unloss", "card", personId.ToString(CultureInfo.InvariantCulture), snapshot, request, false, ex.Message);
            return Api.ServerError("解挂失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-loss", "unloss", "card", personId.ToString(CultureInfo.InvariantCulture),
            new { cardState = state }, new { cardState = 0, reason });
        return Api.Ok(new { personId, cardNo = snapshot.cardNo, beforeState = state, afterState = 0, logId }, "解挂成功，卡已恢复正常");
    }

    /// <summary>GET api/card/cards/{personId}/loss-history：挂失解挂记录。</summary>
    [HttpGet("{personId:int}/loss-history")]
    [RequirePermission("card:cardLoss:view")]
    public IActionResult LossHistory(int personId)
    {
        using var db = SqlAccess.Open();
        var items = db.Query<CardLogRow>(@"
SELECT ID_KEY AS idKey, Person_ID AS personId, Card_No AS cardNo, Op_Type AS opType,
       Before_State AS beforeState, After_State AS afterState, Op_User AS opUser, Op_Time AS opTime,
       Remark AS remark, Ext1 AS ext1, Ext2 AS ext2
  FROM dbo.BS_CardLog
 WHERE Person_ID = @id AND Op_Type IN ('LOSS', 'UNLOSS')
 ORDER BY Op_Time DESC", SqlSession.P("@id", personId));
        return Api.Ok(new { items });
    }

    /* ---------------- 内部方法 ---------------- */

    private static void AppendCardFilter(
        SqlSession db,
        List<string> where,
        List<SqlParameter> parameters,
        string? keyword,
        int? cardState,
        string? typeNo,
        int? deptId,
        bool includeChildren,
        string? from,
        string? to)
    {
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
        if (!string.IsNullOrWhiteSpace(typeNo))
        {
            where.Add("RTRIM(c.Type_No) = @typeNo");
            parameters.Add(SqlSession.P("@typeNo", typeNo.Trim()));
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
        var fromDate = CardRules.ParseDate(from);
        if (fromDate != null)
        {
            where.Add("c.Update_Time >= @from");
            parameters.Add(SqlSession.P("@from", fromDate.Value.Date));
        }
        var toDate = CardRules.ParseDate(to);
        if (toDate != null)
        {
            where.Add("c.Update_Time < @to");
            parameters.Add(SqlSession.P("@to", toDate.Value.Date.AddDays(1)));
        }
    }

    private static string? BuildReason(string? code, string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0) return null;
        return value.Length <= 100 ? value : value.Substring(0, 100);
    }

    /// <summary>
    /// 卡密码校验。模式由 Card.PwdVerify.Mode 控制：Card 必须卡密码；Manager 仅复核码；Both 二者其一。
    /// 连续失败达到阈值后锁定 10 分钟，只影响 BS 操作，不写 ST_Card。
    /// </summary>
    private IActionResult? VerifyCardPassword(SqlSession db, int personId, CardSnapshotRow snapshot, string? password, string? authCode)
    {
        var locked = CardBiz.PasswordLockedUntil(db, personId);
        if (locked != null)
            return Api.Fail(403, "40301", $"卡密码连续错误已锁定，请于 {locked.Value:HH:mm:ss} 后重试");

        var mode = CardBiz.Config(db, "Card.PwdVerify.Mode", "Card").Trim();
        var hasPassword = !string.IsNullOrWhiteSpace(snapshot.cardPassword);
        var pwdOk = hasPassword && CardCrypto.PasswordEquals(snapshot.cardPassword, password);
        var authOk = !string.IsNullOrWhiteSpace(authCode) && CardCrypto.VerifySuperPassword(db, authCode, out _);

        var maxFail = CardBiz.ConfigInt(db, "Card.Pwd.MaxFail", 5);
        var lockMinutes = CardBiz.ConfigInt(db, "Card.Pwd.LockMinutes", 10);

        if (string.Equals(mode, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            if (!authOk) return Api.Fail(403, "40301", "请输入复核码（超级密码）");
            CardBiz.PasswordPassed(db, personId);
            return null;
        }

        if (string.Equals(mode, "Both", StringComparison.OrdinalIgnoreCase))
        {
            if (pwdOk || authOk)
            {
                CardBiz.PasswordPassed(db, personId);
                return null;
            }
            CardBiz.PasswordFailed(db, personId, maxFail, lockMinutes, out var fails, out var until);
            return Api.Fail(403, "40301", until == null
                ? $"卡密码或复核码不正确，还可尝试 {Math.Max(0, maxFail - fails)} 次"
                : $"卡密码或复核码连续错误，已锁定至 {until.Value:HH:mm:ss}");
        }

        // 默认 Card 模式：卡未设置密码时视为无需校验
        if (!hasPassword) return null;
        if (pwdOk)
        {
            CardBiz.PasswordPassed(db, personId);
            return null;
        }

        CardBiz.PasswordFailed(db, personId, maxFail, lockMinutes, out var failCount, out var lockUntil);
        return Api.Fail(403, "40301", lockUntil == null
            ? $"卡密码不正确，还可尝试 {Math.Max(0, maxFail - failCount)} 次"
            : $"卡密码连续错误，已锁定至 {lockUntil.Value:HH:mm:ss}");
    }
}

/* ---------------- 行与请求 ---------------- */

public class CardCardRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public int? deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public string? cardNo { get; set; }
    public string? cardSn { get; set; }
    public string? mobile { get; set; }
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? cardState { get; set; }
    public string? cardStateText { get; set; }
    public string[]? allowedActions { get; set; }
    public decimal? balance { get; set; }
    public decimal? smallBalance { get; set; }
    public decimal? icBalance { get; set; }
    public decimal? foregift { get; set; }
    public decimal? charge { get; set; }
    public DateTime? newDate { get; set; }
    public DateTime? validDate { get; set; }
    public string? cardUserNo { get; set; }
    public DateTime? updateTime { get; set; }
    public string? lockReasonCode { get; set; }
    public string? lockReasonText { get; set; }
    public DateTime? lockTime { get; set; }
    public string? lastOpType { get; set; }
    public DateTime? lastOpTime { get; set; }
    public string? lastOpUser { get; set; }
    public DateTime? lastLockTime { get; set; }
    public DateTime? lastUnlockTime { get; set; }
    public int alterCount { get; set; }
    public int lostDays { get; set; }
    public bool longTerm { get; set; }
    public string? lostChannel { get; set; }
}

public class CardLockRow
{
    public int idKey { get; set; }
    public DateTime? actionTime { get; set; }
    public string? userNo { get; set; }
    public string? remark { get; set; }
}

public class CardAlterRow
{
    public int idKey { get; set; }
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public DateTime? alterDate { get; set; }
    public string? cardNo { get; set; }
    public string? newCardNo { get; set; }
    public decimal? alterFee { get; set; }
    public int? alterType { get; set; }
    public string? userNo { get; set; }
    public string? remark { get; set; }
}

public class CardFreezeRequest
{
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
    public string? Password { get; set; }
    public string? AuthCode { get; set; }
    public string? RequestNo { get; set; }
}

public class CardFreezeBatchRequest
{
    public int[]? PersonIds { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
}

public class CardChangeRequest
{
    public string? OldCardNo { get; set; }
    public string? NewCardNo { get; set; }
    public string? NewCardSn { get; set; }
    public decimal? AlterFee { get; set; }
    public bool ResetPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ChangeType { get; set; }
    public string? Password { get; set; }
    public string? AuthCode { get; set; }
    public string? Remark { get; set; }
    public string? RequestNo { get; set; }
}

public class CardLossRequest
{
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
    public string? Channel { get; set; }
    public string? Contact { get; set; }
    public bool PoliceReport { get; set; }
    public string? Password { get; set; }
    public string? AuthCode { get; set; }
    public string? FoundNote { get; set; }
    public string? RequestNo { get; set; }
}

public class CardUnlossRequest
{
    public string? ReasonText { get; set; }
    public string? FoundNote { get; set; }
    public string? RequestNo { get; set; }
}

using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 开户与销户。账户权威数据在 ST_Card（一人一条，主键 Person_ID），卡号在 ST_Person.Card_No。
/// 所有资金动作与流水在同一事务内完成：卡表、流水表、人员卡号、审计要么全部成功，要么全部回滚。
/// </summary>
[Route("api/card/accounts")]
[ApiController]
public class CardAccountsController : ControllerBase
{
    private readonly AuditService _audit;

    public CardAccountsController(AuditService audit)
    {
        _audit = audit;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;
    private string ClientIp => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private const string AccountSelect = @"
SELECT c.Person_ID             AS personId,
       p.Person_No             AS personNo,
       p.Person_Name           AS personName,
       p.Dept_ID               AS deptId,
       d.Dept_No               AS deptNo,
       d.Dept_Name             AS deptName,
       p.Card_No               AS cardNo,
       p.Card_SN               AS cardSn,
       p.Is_Del                AS personIsDel,
       RTRIM(c.Type_No)        AS typeNo,
       ct.Type_Name            AS typeName,
       c.Card_State            AS cardState,
       c.Card_Balance          AS balance,
       c.Card_SmallBalance     AS smallBalance,
       c.Subsidy_Fund          AS subsidyFund,
       c.IC_Balance            AS icBalance,
       c.Card_Foregift         AS foregift,
       c.Card_Charge           AS charge,
       c.Card_NewDate          AS newDate,
       c.Card_ValidDate        AS validDate,
       c.User_No               AS cardUserNo,
       c.Update_Time           AS updateTime,
       ct.Validity             AS validity,
       ct.Min_Balance          AS minBalance,
       ct.SF_Max_Fund          AS sfMaxFund,
       ct.SF_Max_DayFund       AS sfMaxDayFund,
       ct.SF_LimitTimes        AS sfLimitTimes,
       ct.Max_IcFunds          AS maxIcFunds,
       ct.Small_Discount       AS smallDiscount,
       ISNULL(ci.SmallAccount_Max, 0) AS smallAccountMax,
       ISNULL(ci.Day_DelSysBlack, 182) AS blackDays
  FROM dbo.ST_Card c
  INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID
  LEFT  JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT  JOIN dbo.ST_CardType ct ON RTRIM(ct.Type_No) = RTRIM(c.Type_No)
  OUTER APPLY (SELECT TOP 1 * FROM dbo.ST_CardInfo ORDER BY Card_Type) ci";

    /// <summary>GET api/card/accounts：账户分页查询；opened=true 只查已开户，false 只查未开户人员。</summary>
    [HttpGet]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult List(
        [FromQuery] string? keyword,
        [FromQuery] string? typeNo,
        [FromQuery] int? cardState,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] bool? opened = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { includeDeleted ? "1 = 1" : "ISNULL(p.Is_Del, 0) = 0" };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(p.Person_No LIKE @kw OR p.Person_Name LIKE @kw OR p.Card_No LIKE @kw OR p.id_number LIKE @kw)");
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
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

        // 未开户人员列表：人员条件与已开户列表共用，卡类与卡状态条件不适用
        if (opened == false)
        {
            var personFilter = string.Join(" AND ", where);
            var totalUnopened = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.ST_Person p LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID " +
                "LEFT JOIN dbo.ST_Card c ON c.Person_ID = p.Person_ID WHERE c.Person_ID IS NULL AND " + personFilter,
                parameters.ToArray());

            parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
            parameters.Add(SqlSession.P("@size", pageSize));
            var unopened = db.Query<CardAccountRow>(@"
SELECT * FROM (
    SELECT q.personId, q.personNo, q.personName, q.deptId, q.deptNo, q.deptName, q.cardNo, q.cardSn, q.personIsDel,
           ROW_NUMBER() OVER (ORDER BY q.deptNo, q.personNo) AS RowNum
      FROM (
        SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
               p.Dept_ID AS deptId, d.Dept_No AS deptNo, d.Dept_Name AS deptName,
               p.Card_No AS cardNo, p.Card_SN AS cardSn, p.Is_Del AS personIsDel
          FROM dbo.ST_Person p
          LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
          LEFT JOIN dbo.ST_Card c ON c.Person_ID = p.Person_ID
         WHERE c.Person_ID IS NULL AND " + personFilter + @"
      ) q
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

            return Api.Ok(new { total = totalUnopened, items = unopened });
        }

        if (!string.IsNullOrWhiteSpace(typeNo))
        {
            where.Add("RTRIM(c.Type_No) = @typeNo");
            parameters.Add(SqlSession.P("@typeNo", typeNo.Trim()));
        }
        if (cardState != null)
        {
            where.Add("ISNULL(c.Card_State, 0) = @state");
            parameters.Add(SqlSession.P("@state", cardState.Value));
        }

        var filter = string.Join(" AND ", where);
        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card c INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID " +
                                 "LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID WHERE " + filter, parameters.ToArray());

        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));
        var items = db.Query<CardAccountRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.deptNo, q.personNo) AS RowNum
      FROM (" + AccountSelect + " WHERE " + filter + @") q
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/accounts/open-options：开户初始数据（卡类、账户信息、参数）。</summary>
    [HttpGet("open-options")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult OpenOptions()
    {
        using var db = SqlAccess.Open();
        var types = db.Query<CardTypeRow>(@"
SELECT RTRIM(t.Type_No) AS typeNo, t.Type_Name AS typeName, ISNULL(t.Validity, 0) AS validity,
       ISNULL(t.Min_Balance, 0) AS minBalance, ISNULL(t.Subsidy, 0) AS subsidy, ISNULL(t.Max_IcFunds, 0) AS maxIcFunds,
       ISNULL(t.SF_Max_Fund, 0) AS sfMaxFund, ISNULL(t.SF_Max_DayFund, 0) AS sfMaxDayFund,
       ISNULL(t.SF_LimitTimes, 0) AS sfLimitTimes, ISNULL(t.SF_Max_Count, 0) AS sfMaxCount,
       ISNULL(t.SF_Discount, 100) AS sfDiscount, ISNULL(t.LH_Max_Fund, 0) AS lhMaxFund,
       ISNULL(t.LH_Max_DayFund, 0) AS lhMaxDayFund, ISNULL(t.LH_Discount, 100) AS lhDiscount,
       ISNULL(t.Small_Discount, 100) AS smallDiscount, ISNULL(t.Is_OnlySubsidy, 0) AS isOnlySubsidy,
       ISNULL(t.Is_Del, 0) AS isDel,
       ISNULL(ci.Card_Foregift, 0) AS foregift, ISNULL(ci.Card_Charge, 0) AS charge,
       ISNULL(ci.SmallAccount_Max, 0) AS smallAccountMax
  FROM dbo.ST_CardType t
  OUTER APPLY (SELECT TOP 1 * FROM dbo.ST_CardInfo ORDER BY Card_Type) ci
 WHERE ISNULL(t.Is_Del, 0) = 0
 ORDER BY t.Type_No");

        var isPersonNoAsCardNo = CardBiz.ConfigInt(db, "IsPersonNoAsCardNo", 0);
        var supplyRemark = CardBiz.Config(db, "Card.Open.SupplyRemark", "开户充值");
        return Api.Ok(new
        {
            items = types,
            isPersonNoAsCardNo,
            idCardLen = CardBiz.ConfigInt(db, "IDCardLen", 10),
            supplyRemark,
            pwdVerifyMode = CardBiz.Config(db, "Card.PwdVerify.Mode", "Card"),
            blacklistCheck = CardBiz.ConfigInt(db, "Card.Open.CheckBlacklist", 1),
        });
    }

    /// <summary>GET api/card/accounts/available：卡号可用性即时校验。</summary>
    [HttpGet("available")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult Available([FromQuery] string? cardNo, [FromQuery] int personId = 0)
    {
        var no = (cardNo ?? string.Empty).Trim();
        if (no.Length == 0) return Api.Ok(new { cardNo = no, available = false, reason = "请输入卡号" });
        if (!CardRules.IsValidCardNo(no) || no.Length > 12)
            return Api.Ok(new { cardNo = no, available = false, reason = "卡号长度不能超过 12 位" });

        using var db = SqlAccess.Open();
        var ownerId = CardBiz.CardOwner(db, no, personId);
        if (ownerId > 0)
        {
            var owner = db.Scalar("SELECT Person_Name FROM dbo.ST_Person WHERE Person_ID = @id", SqlSession.P("@id", ownerId)) as string;
            return Api.Ok(new { cardNo = no, available = false, reason = $"卡号已被 {owner} 使用", ownerId });
        }
        if (CardBiz.IsCardDisabled(db, no))
            return Api.Ok(new { cardNo = no, available = false, reason = "卡号已禁用，禁止开户" });
        if (CardBiz.IsCashierCard(db, no))
            return Api.Ok(new { cardNo = no, available = false, reason = "该卡号已作为出纳卡使用" });

        var black = CardBiz.BlacklistedSince(db, no, CardBiz.ConfigInt(db, "Card.BlackDays", 182));
        return Api.Ok(new
        {
            cardNo = no,
            available = true,
            reason = black == null ? string.Empty : "该卡号存在于黑名单，请确认后继续",
            blacklisted = black != null,
            blackDate = black,
        });
    }

    /// <summary>POST api/card/accounts/open：开户（单事务八步）。</summary>
    [HttpPost("open")]
    [RequirePermission("card:accountOpen:open")]
    public IActionResult Open([FromBody] CardOpenRequest request)
    {
        var userNo = CurrentUser;
        if (string.IsNullOrWhiteSpace(userNo)) return Api.Unauthorized();

        var cardNo = (request.CardNo ?? string.Empty).Trim();
        var cardSn = (request.CardSn ?? string.Empty).Trim();
        var typeNo = CardBiz.NormalizeTypeNo(request.TypeNo);
        var mainFund = request.MainFund ?? 0m;
        var smallFund = request.SmallFund ?? 0m;

        if (request.PersonId <= 0) return Api.Fail(400, "40005", "请选择人员");
        if (typeNo.Length == 0) return Api.Fail(400, "40005", "请选择卡类");
        if (cardNo.Length == 0 || cardNo.Length > 12) return Api.Fail(400, "40008", "卡号长度必须在 1 至 12 位之间");
        if (cardSn.Length > 12) return Api.Fail(400, "40008", "卡号序列长度不能超过 12 位");
        if (!CardBiz.IsValidMoney(mainFund, false, out var moneyError)) return Api.Fail(400, "40009", "主账户金额" + moneyError);
        if (!CardBiz.IsValidMoney(smallFund, false, out moneyError)) return Api.Fail(400, "40009", "小账户金额" + moneyError);
        if (!CardCrypto.IsValidPassword(request.Password, true, out var pwdError)) return Api.Fail(400, "40012", pwdError);

        using var db = SqlAccess.Open();

        // 幂等：同一 requestNo 返回首次结果，不重复写流水
        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == request.PersonId)
        {
            var snapshot0 = CardBiz.Snapshot(db, previous.personId);
            return Api.Ok(BuildOpenResult(db, previous.personId, snapshot0, previous.idKey), "重复提交，返回首次开户结果");
        }

        var person = db.QuerySingle<CardPersonLite>(@"
SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName, p.Is_Del AS isDel
  FROM dbo.ST_Person p WHERE p.Person_ID = @id", SqlSession.P("@id", request.PersonId));
        if (person == null) return Api.Fail(422, "42201", "人员不存在或已注销");
        if (person.isDel == true) return Api.Fail(422, "42201", "人员已注销，请先恢复后再开户");

        var cardType = db.QuerySingle<CardTypeLite>(@"
SELECT RTRIM(Type_No) AS typeNo, Type_Name AS typeName, ISNULL(Validity, 0) AS validity, ISNULL(Is_Del, 0) AS isDel
  FROM dbo.ST_CardType WHERE RTRIM(Type_No) = @no", SqlSession.P("@no", typeNo));
        if (cardType == null) return Api.Fail(400, "40005", "卡类不存在");
        if (cardType.isDel == true) return Api.Fail(400, "40010", "卡类已停用，请重新选择");

        // ST_CardInfo 为全局单记录参数表，开户直接取该唯一记录，不按卡类匹配
        var info = db.QuerySingle<CardInfoLite>(@"
SELECT TOP 1 ISNULL(Card_Foregift, 0) AS foregift, ISNULL(Card_Charge, 0) AS charge,
       ISNULL(SmallAccount_Max, 0) AS smallAccountMax, ISNULL(Day_DelSysBlack, 182) AS blackDays
  FROM dbo.ST_CardInfo ORDER BY Card_Type");
        var warning = info == null ? "卡务参数尚未初始化，押金与卡费按 0 处理，请先在账户信息设置中配置" : null;
        var foregift = info?.foregift ?? 0m;
        var charge = info?.charge ?? 0m;
        var smallMax = info?.smallAccountMax ?? 0m;

        if (smallFund > 0)
        {
            if (smallMax <= 0) return Api.Fail(400, "40009", "卡务参数未设置小账户上限，不允许转入小账户");
            if (smallFund > smallMax) return Api.Fail(400, "40009", $"小账户金额超过上限 {smallMax.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        var validDate = CardRules.ParseDate(request.ValidDate) ?? DateTime.Today.AddDays(cardType.validity is > 0 ? cardType.validity!.Value : 365);
        if (validDate.Date < DateTime.Today) return Api.Fail(400, "40011", "有效期不能早于开户当日");

        var ownerId = CardBiz.CardOwner(db, cardNo, request.PersonId);
        if (ownerId > 0) return Api.Fail(409, "40911", "卡号已被占用，请更换卡号");
        if (CardBiz.IsCardDisabled(db, cardNo)) return Api.Fail(409, "40911", "卡号已禁用，禁止开户");

        var blackDays = info?.blackDays ?? 182;
        var blackDate = CardBiz.BlacklistedSince(db, cardNo, blackDays);
        if (blackDate != null && !request.ConfirmBlacklist)
            return Api.Fail(409, "40911", $"该卡号在 {blackDate.Value:yyyy-MM-dd} 进入黑名单，请确认后继续");

        var encryptedPassword = string.IsNullOrWhiteSpace(request.Password) ? null : CardCrypto.EncryptPassword(request.Password);
        var encryptedCardNo = CardCrypto.EncryptCardNo(cardNo);
        var supplyRemark = CardBiz.Config(db, "Card.Open.SupplyRemark", "开户充值");
        long logId = 0;

        db.Begin();
        try
        {
            // 并发保护：同一人员与同一卡号的加锁校验必须在事务内完成
            if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card WITH (UPDLOCK, HOLDLOCK) WHERE Person_ID = @id",
                    SqlSession.P("@id", request.PersonId)) > 0)
            {
                db.Rollback();
                return Api.Fail(409, "40909", "该人员已开户，请转入修改卡信息或换卡");
            }
            if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person WITH (UPDLOCK, HOLDLOCK) WHERE Card_No = @no AND ISNULL(Is_Del, 0) = 0 AND Person_ID <> @id",
                    SqlSession.P("@no", cardNo), SqlSession.P("@id", request.PersonId)) > 0)
            {
                db.Rollback();
                return Api.Fail(409, "40911", "卡号已被占用，请更换卡号");
            }

            // 1 账户
            db.Exec(@"
INSERT INTO dbo.ST_Card
    (Person_ID, Type_No, Card_Password, Card_NewDate, Card_ValidDate, Card_Foregift, Card_Charge,
     Card_Balance, Card_SmallBalance, Card_State, User_No, Subsidy_Fund, Update_Time, IC_Balance)
VALUES
    (@id, @typeNo, @pwd, GETDATE(), @valid, @foregift, @charge,
     @main, @small, 0, @user, 0, GETDATE(), @main)",
                SqlSession.P("@id", request.PersonId),
                SqlSession.P("@typeNo", typeNo),
                SqlSession.P("@pwd", encryptedPassword),
                SqlSession.P("@valid", validDate),
                SqlSession.P("@foregift", foregift),
                SqlSession.P("@charge", charge),
                SqlSession.P("@main", mainFund),
                SqlSession.P("@small", smallFund),
                SqlSession.P("@user", userNo));

            // 2 人员卡号与卡序列
            db.Exec(@"UPDATE dbo.ST_Person SET Card_No = @cardNo, Card_SN = @cardSn, C_PSD = @psd, update_time = GETDATE()
                       WHERE Person_ID = @id",
                SqlSession.P("@cardNo", cardNo),
                SqlSession.P("@cardSn", cardSn.Length == 0 ? null : cardSn),
                SqlSession.P("@psd", encryptedCardNo),
                SqlSession.P("@id", request.PersonId));

            // 3 首笔充值流水
            db.Exec(@"INSERT INTO dbo.ST_SupplyFund (Person_ID, Supply_Fund, Card_Balance, User_No, Remark)
                      VALUES (@id, @fund, @balance, @user, @remark)",
                SqlSession.P("@id", request.PersonId),
                SqlSession.P("@fund", mainFund),
                SqlSession.P("@balance", mainFund),
                SqlSession.P("@user", userNo),
                SqlSession.P("@remark", supplyRemark));

            // 4 小账户转账流水
            if (smallFund > 0)
            {
                db.Exec(@"INSERT INTO dbo.ST_TransferAccount (Person_ID, Transfer_Date, Transfer_Fund, Card_Balance, User_No, Remark)
                          VALUES (@id, GETDATE(), @fund, @balance, @user, @remark)",
                    SqlSession.P("@id", request.PersonId),
                    SqlSession.P("@fund", smallFund),
                    SqlSession.P("@balance", mainFund),
                    SqlSession.P("@user", userNo),
                    SqlSession.P("@remark", "开户转入小账户"));

                // 5 主账户扣减小账户并同步扣减 IC 余额（两列各自扣减，避免依赖 SET 的旧值语义）
                db.Exec(@"UPDATE dbo.ST_Card
                             SET Card_Balance = ISNULL(Card_Balance, 0) - @small,
                                 IC_Balance = ISNULL(IC_Balance, 0) - @small,
                                 Update_Time = GETDATE()
                           WHERE Person_ID = @id",
                    SqlSession.P("@small", smallFund), SqlSession.P("@id", request.PersonId));
            }

            // 6 扩展表与 7 动作审计
            CardBiz.TouchExt(db, request.PersonId, cardNo, "OPEN", userNo, null, null, null, null);
            logId = CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = request.PersonId,
                CardNo = cardNo,
                OpType = "OPEN",
                AfterState = CardState.Normal,
                Balance = mainFund - smallFund,
                SmallBalance = smallFund,
                Foregift = foregift,
                Charge = charge,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(request.Remark, 200) ?? "开户",
            });

            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account", "open", "card", request.PersonId.ToString(CultureInfo.InvariantCulture),
                null, request, false, ex.Message);
            if (ex.Message.Contains("IX_BS_CardLog_Request", StringComparison.OrdinalIgnoreCase))
                return Api.Fail(409, "40913", "该请求已处理，请勿重复提交");
            return Api.ServerError("开户失败：" + ex.Message);
        }

        var snapshot = CardBiz.Snapshot(db, request.PersonId);
        var result = BuildOpenResult(db, request.PersonId, snapshot, logId);
        _audit.LogAndSave(HttpContext, "card-account", "open", "card", request.PersonId.ToString(CultureInfo.InvariantCulture),
            null, new { cardNo, typeNo, mainFund, smallFund, foregift, charge, validDate });

        if (!string.IsNullOrEmpty(warning))
            return Api.Ok(result, "开户成功。" + warning);
        return Api.Ok(result, "开户成功");
    }

    /// <summary>GET api/card/accounts/{personId}：账户与卡详情。</summary>
    [HttpGet("{personId:int}")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult Detail(int personId)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardAccountRow>(AccountSelect + " WHERE c.Person_ID = @id", SqlSession.P("@id", personId));
        if (row == null) return Api.Fail(404, "40914", "该人员尚未开户");
        row.cardStateText = CardState.Text(row.cardState);
        row.allowedActions = CardState.AllowedActions(row.cardState);
        var (lastLock, lastUnlock) = LastLockTimes(db, personId);
        row.lastLockTime = lastLock;
        row.lastUnlockTime = lastUnlock;
        row.pwdConfigured = string.IsNullOrWhiteSpace(row.cardPassword) ? 0 : 1;
        row.cardPassword = null;
        return Api.Ok(row);
    }

    /// <summary>PUT api/card/accounts/{personId}：修改卡类、有效期与密码，不动余额。</summary>
    [HttpPut("{personId:int}")]
    [RequirePermission("card:accountOpen:update")]
    public IActionResult UpdateCard(int personId, [FromBody] CardUpdateRequest request)
    {
        var userNo = CurrentUser;
        using var db = SqlAccess.Open();
        var before = CardBiz.Snapshot(db, personId);
        if (before == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var typeNo = string.IsNullOrWhiteSpace(request.TypeNo) ? CardBiz.NormalizeTypeNo(before.typeNo) : CardBiz.NormalizeTypeNo(request.TypeNo);
        var cardType = db.QuerySingle<CardTypeLite>(@"
SELECT RTRIM(Type_No) AS typeNo, Type_Name AS typeName, ISNULL(Validity, 0) AS validity, ISNULL(Is_Del, 0) AS isDel
  FROM dbo.ST_CardType WHERE RTRIM(Type_No) = @no", SqlSession.P("@no", typeNo));
        if (cardType == null) return Api.Fail(400, "40005", "卡类不存在");
        if (cardType.isDel == true) return Api.Fail(400, "40010", "卡类已停用，请重新选择");
        if (!CardCrypto.IsValidPassword(request.Password, true, out var pwdError)) return Api.Fail(400, "40012", pwdError);

        var validDate = CardRules.ParseDate(request.ValidDate) ?? before.validDate ?? DateTime.Today;
        if (validDate.Date < DateTime.Today) return Api.Fail(400, "40011", "有效期不能早于今天");

        var typeChanged = !string.Equals(CardBiz.NormalizeTypeNo(before.typeNo), typeNo, StringComparison.Ordinal);
        if (typeChanged && !request.ConfirmTypeChange)
            return Api.Fail(409, "40910", "卡类变更会影响消费限额与补贴规则，请确认后提交");

        var pwd = string.IsNullOrWhiteSpace(request.Password) ? null : CardCrypto.EncryptPassword(request.Password);

        db.Begin();
        try
        {
            db.Exec(@"UPDATE dbo.ST_Card
                         SET Type_No = @typeNo, Card_ValidDate = @valid,
                             Card_Password = CASE WHEN @pwd IS NULL THEN Card_Password ELSE @pwd END,
                             Update_Time = GETDATE()
                       WHERE Person_ID = @id",
                SqlSession.P("@typeNo", typeNo), SqlSession.P("@valid", validDate),
                SqlSession.P("@pwd", pwd), SqlSession.P("@id", personId));

            CardBiz.TouchExt(db, personId, before.cardNo, "UPDATE", userNo, null, null, null, null);
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account", "update", "card", personId.ToString(CultureInfo.InvariantCulture), before, request, false, ex.Message);
            return Api.ServerError("修改卡信息失败：" + ex.Message);
        }

        var after = CardBiz.Snapshot(db, personId);
        _audit.LogAndSave(HttpContext, "card-account", "update", "card", personId.ToString(CultureInfo.InvariantCulture), before, after);
        return Api.Ok(new { personId, typeNo, validDate, typeChanged }, "卡信息已保存");
    }

    /// <summary>GET api/card/accounts/{personId}/refund-preview：退款试算（服务端口径）。</summary>
    [HttpGet("{personId:int}/refund-preview")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult RefundPreview(int personId)
    {
        using var db = SqlAccess.Open();
        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户");

        var balance = snapshot.balance ?? 0m;
        var small = snapshot.smallBalance ?? 0m;
        var foregift = snapshot.foregift ?? 0m;
        var charge = snapshot.charge ?? 0m;
        var refund = balance + small + foregift;

        return Api.Ok(new
        {
            personId,
            personNo = snapshot.personNo,
            personName = snapshot.personName,
            deptName = snapshot.deptName,
            cardNo = snapshot.cardNo,
            typeNo = snapshot.typeNo,
            typeName = snapshot.typeName,
            cardState = snapshot.cardState,
            cardStateText = CardState.Text(snapshot.cardState),
            balance,
            smallBalance = small,
            foregift,
            charge,
            icBalance = snapshot.icBalance ?? 0m,
            subsidyFund = snapshot.subsidyFund ?? 0m,
            refundFund = refund,
            dbFund = refund,
            newDate = snapshot.newDate,
            validDate = snapshot.validDate,
            chargeNote = "卡费只登记不退还，界面标注为「卡费不退，仅登记」",
            newUserNo = snapshot.cardUserNo,
        });
    }

    /// <summary>POST api/card/accounts/{personId}/close：销户（退款、销卡记录、卡号黑名单、删除账户）。</summary>
    [HttpPost("{personId:int}/close")]
    [RequirePermission("card:accountOpen:close")]
    public IActionResult Close(int personId, [FromBody] CardCloseRequest request)
    {
        var userNo = CurrentUser;
        if (string.IsNullOrWhiteSpace(userNo)) return Api.Unauthorized();

        using var db = SqlAccess.Open();

        var previous = CardBiz.FindLogByRequest(db, request.RequestNo);
        if (previous != null && previous.personId == personId)
            return Api.Ok(new
            {
                deleteCardId = previous.idKey,
                takeFundId = (long?)null,
                refundFund = previous.refundAmount ?? 0m,
                backFund = 0m,
                factFund = previous.refundAmount ?? 0m,
                balanceAdjust = 0m,
                duplicate = true,
            }, "重复提交，返回首次销户结果");

        var snapshot = CardBiz.Snapshot(db, personId);
        if (snapshot == null) return Api.Fail(404, "40914", "该人员尚未开户或已销户");

        var state = snapshot.cardState ?? 0;
        if (state != CardState.Normal && state != CardState.Lost && state != CardState.Frozen)
            return Api.Fail(409, "40910", "卡状态不允许销户");
        if (state == CardState.Lost && !request.ConfirmLost)
            return Api.Fail(409, "40910", "该卡处于挂失状态，请确认后再销户");

        if (CardBiz.ConfigInt(db, "Card.Close.CheckInflight", 1) == 1)
        {
            var inflight = CardBiz.InflightConsume(db, personId, 24);
            if (inflight > 0) return Api.Fail(409, "40915", $"存在 {inflight} 条在途消费记录，请稍后重试");
        }

        if (!string.Equals((request.ConfirmText ?? string.Empty).Trim(), "确认销户", StringComparison.Ordinal))
            return Api.Fail(400, "40013", "请输入确认短语「确认销户」");
        if (!request.Confirmed) return Api.Fail(400, "40013", "请勾选「已核对退款金额」");
        if (string.IsNullOrWhiteSpace(request.VoucherNo)) return Api.Fail(400, "40013", "请录入退款凭证号");

        var balance = snapshot.balance ?? 0m;
        var small = snapshot.smallBalance ?? 0m;
        var foregift = snapshot.foregift ?? 0m;
        var charge = snapshot.charge ?? 0m;
        var dbFund = balance + small + foregift;

        // 读卡值优先：BS 无读卡器时由前端可选传入，差额记 ST_PersonAccount 平账
        var refund = request.ReadCardBalance == null ? dbFund : request.ReadCardBalance.Value + small + foregift;
        if (refund < 0) return Api.Fail(400, "40009", "退款金额不能为负数");
        if (request.RefundFund != null && decimal.Round(request.RefundFund.Value, 2) != decimal.Round(refund, 2))
            return Api.Fail(409, "40916", $"退款金额已变化（当前应为 {refund.ToString("0.00", CultureInfo.InvariantCulture)}），请重新试算");

        var adjust = dbFund - refund;
        var newUserNo = string.IsNullOrWhiteSpace(snapshot.cardUserNo) ? userNo : snapshot.cardUserNo!.Trim();
        var remark = BuildCloseRemark(request);
        long deleteCardId = 0;
        long takeFundId = 0;

        db.Begin();
        try
        {
            // 前置条件更新，避免检查与更新之间的窗口
            var affected = db.Exec("UPDATE dbo.ST_Card SET Update_Time = GETDATE() WHERE Person_ID = @id AND ISNULL(Card_State, 0) = @state",
                SqlSession.P("@id", personId), SqlSession.P("@state", state));
            if (affected == 0)
            {
                db.Rollback();
                return Api.Fail(409, "40910", "卡状态已变化，请刷新后重试");
            }

            // 分支一：销卡不销户，先写取款流水
            if (request.HasCard && !request.DeletePerson)
            {
                takeFundId = Convert.ToInt64(db.Scalar(@"
INSERT INTO dbo.ST_TakeFund (Person_ID, Take_Date, Take_Fund, Card_Balance, User_No, Remark)
OUTPUT INSERTED.ID_KEY
VALUES (@id, GETDATE(), @fund, @balance, @user, @remark)",
                    SqlSession.P("@id", personId), SqlSession.P("@fund", refund),
                    SqlSession.P("@balance", balance), SqlSession.P("@user", userNo),
                    SqlSession.P("@remark", "注销卡号退金额")) ?? 0L, CultureInfo.InvariantCulture);
            }

            // 销卡记录：New_User_No 必须在删除 ST_Card 之前取值
            deleteCardId = Convert.ToInt64(db.Scalar(@"
INSERT INTO dbo.ST_DeleteCard
    (Person_ID, New_Date, Del_Date, Card_No, Back_Fund, Fact_Fund, Back_SmallFund, Back_Foregift,
     Card_Charge, User_No, New_User_No, Remark)
OUTPUT INSERTED.ID_KEY
VALUES
    (@id, @newDate, GETDATE(), @cardNo, @backFund, @factFund, @small, @foregift,
     @charge, @user, @newUser, @remark)",
                SqlSession.P("@id", personId),
                SqlSession.P("@newDate", snapshot.newDate ?? DateTime.Now),
                SqlSession.P("@cardNo", CardBiz.Cut(snapshot.cardNo, 12) ?? string.Empty),
                SqlSession.P("@backFund", request.HasCard && !request.DeletePerson ? 0m : refund),
                SqlSession.P("@factFund", refund),
                SqlSession.P("@small", small),
                SqlSession.P("@foregift", foregift),
                SqlSession.P("@charge", charge),
                SqlSession.P("@user", userNo),
                SqlSession.P("@newUser", newUserNo),
                SqlSession.P("@remark", CardBiz.Cut(remark, 50))) ?? 0L, CultureInfo.InvariantCulture);

            // 卡库不平平账（读卡值或历史数据差异）
            if (adjust != 0m) WriteBalanceAdjust(db, personId, adjust);

            // 无卡注销时把卡号写入黑名单；持卡注销不写，避免补卡与重新开户被误拦
            if (!request.HasCard && !string.IsNullOrWhiteSpace(snapshot.cardNo))
            {
                db.Exec(@"INSERT INTO dbo.ST_BlackPerson (Person_ID, Card_No, Del_Date, Remark)
                          VALUES (@id, @cardNo, GETDATE(), @remark)",
                    SqlSession.P("@id", personId),
                    SqlSession.P("@cardNo", CardBiz.Cut(snapshot.cardNo, 12)),
                    SqlSession.P("@remark", CardBiz.Cut(CardBiz.Config(db, "Card.Close.BlackRemark", "销卡产生"), 50)));
            }

            // 删除账户并清空人员卡号：Card_No 置空串，Card_SN 置 NULL（与 CS 一致）
            db.Exec("DELETE FROM dbo.ST_Card WHERE Person_ID = @id", SqlSession.P("@id", personId));
            db.Exec("UPDATE dbo.ST_Person SET Card_No = '', Card_SN = NULL, update_time = GETDATE() WHERE Person_ID = @id",
                SqlSession.P("@id", personId));

            if (request.DeletePerson)
                db.Exec("UPDATE dbo.ST_Person SET Is_Del = 1, update_time = GETDATE() WHERE Person_ID = @id",
                    SqlSession.P("@id", personId));

            CardBiz.TouchExt(db, personId, snapshot.cardNo, "CLOSE", userNo, null, "CLOSE",
                CardBiz.Cut(request.RefundMode, 20), DateTime.Now);
            CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = personId,
                CardNo = snapshot.cardNo,
                OpType = "CLOSE",
                BeforeState = state,
                AfterState = null,
                Balance = 0m,
                SmallBalance = 0m,
                Foregift = foregift,
                Charge = charge,
                RefundAmount = refund,
                RequestNo = request.RequestNo,
                Source = "WEB",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut(remark, 200),
                Ext1 = CardBiz.Cut(request.VoucherNo, 100),
                Ext2 = request.DeletePerson ? "销卡并销户" : "销卡不销户",
            });

            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account", "close", "card", personId.ToString(CultureInfo.InvariantCulture),
                snapshot, request, false, ex.Message);
            return Api.ServerError("销户失败：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-account", "close", "card", personId.ToString(CultureInfo.InvariantCulture),
            snapshot, new { refund, deletePerson = request.DeletePerson, hasCard = request.HasCard, deleteCardId });

        return Api.Ok(new
        {
            deleteCardId,
            takeFundId = takeFundId == 0 ? (long?)null : takeFundId,
            refundFund = refund,
            backFund = request.HasCard && !request.DeletePerson ? 0m : refund,
            factFund = refund,
            balanceAdjust = adjust,
            cardNo = snapshot.cardNo,
            personName = snapshot.personName,
            deletePerson = request.DeletePerson,
        }, request.DeletePerson ? "销户完成，人员已同步注销" : "销卡完成，人员档案保留");
    }

    /// <summary>GET api/card/accounts/close-records：销卡记录查询。</summary>
    [HttpGet("close-records")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult CloseRecords(
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
            where.Add("(p.Person_No LIKE @kw OR p.Person_Name LIKE @kw OR d.Card_No LIKE @kw)");
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
        }
        var fromDate = CardRules.ParseDate(from);
        if (fromDate != null)
        {
            where.Add("d.Del_Date >= @from");
            parameters.Add(SqlSession.P("@from", fromDate.Value.Date));
        }
        var toDate = CardRules.ParseDate(to);
        if (toDate != null)
        {
            where.Add("d.Del_Date < @to");
            parameters.Add(SqlSession.P("@to", toDate.Value.Date.AddDays(1)));
        }
        var filter = string.Join(" AND ", where);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_DeleteCard d LEFT JOIN dbo.ST_Person p ON p.Person_ID = d.Person_ID WHERE " + filter,
            parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardCloseRecordRow>(@"
SELECT * FROM (
    SELECT d.ID_KEY AS idKey, d.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
           d.Card_No AS cardNo, d.New_Date AS newDate, d.Del_Date AS delDate,
           d.Back_Fund AS backFund, d.Fact_Fund AS factFund, d.Back_SmallFund AS backSmallFund,
           d.Back_Foregift AS backForegift, d.Card_Charge AS cardCharge,
           d.User_No AS userNo, d.New_User_No AS newUserNo, d.Remark AS remark,
           ROW_NUMBER() OVER (ORDER BY d.Del_Date DESC, d.ID_KEY DESC) AS RowNum
      FROM dbo.ST_DeleteCard d
      LEFT JOIN dbo.ST_Person p ON p.Person_ID = d.Person_ID
     WHERE " + filter + @"
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/accounts/close-records/{id}：销卡记录详情。</summary>
    [HttpGet("close-records/{id:long}")]
    [RequirePermission("card:accountOpen:view")]
    public IActionResult CloseRecord(long id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardCloseRecordRow>(@"
SELECT d.ID_KEY AS idKey, d.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
       d.Card_No AS cardNo, d.New_Date AS newDate, d.Del_Date AS delDate,
       d.Back_Fund AS backFund, d.Fact_Fund AS factFund, d.Back_SmallFund AS backSmallFund,
       d.Back_Foregift AS backForegift, d.Card_Charge AS cardCharge,
       d.User_No AS userNo, d.New_User_No AS newUserNo, d.Remark AS remark
  FROM dbo.ST_DeleteCard d
  LEFT JOIN dbo.ST_Person p ON p.Person_ID = d.Person_ID
 WHERE d.ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("销卡记录不存在");
        return Api.Ok(row);
    }

    /* ---------------- 内部方法 ---------------- */

    private static object BuildOpenResult(SqlSession db, int personId, CardSnapshotRow? snapshot, long logId)
    {
        return new
        {
            personId,
            cardNo = snapshot?.cardNo,
            cardSn = snapshot?.cardSn,
            typeNo = snapshot?.typeNo,
            typeName = snapshot?.typeName,
            cardBalance = snapshot?.balance ?? 0m,
            smallBalance = snapshot?.smallBalance ?? 0m,
            foregift = snapshot?.foregift ?? 0m,
            charge = snapshot?.charge ?? 0m,
            validDate = snapshot?.validDate,
            cardState = snapshot?.cardState,
            logId,
        };
    }

    private static (DateTime? lastLock, DateTime? lastUnlock) LastLockTimes(SqlSession db, int personId)
    {
        var lockValue = db.Scalar("SELECT TOP 1 Lock_Date FROM dbo.ST_LockCard WHERE Person_ID = @id ORDER BY Lock_Date DESC",
            SqlSession.P("@id", personId));
        var unlockValue = db.Scalar("SELECT TOP 1 UnLock_Date FROM dbo.ST_UnLockCard WHERE Person_ID = @id ORDER BY UnLock_Date DESC",
            SqlSession.P("@id", personId));
        return (lockValue as DateTime?, unlockValue as DateTime?);
    }

    private static string BuildCloseRemark(CardCloseRequest request)
    {
        var mode = string.Equals(request.RefundMode, "TRANSFER", StringComparison.OrdinalIgnoreCase) ? "转账" : "现金";
        var text = "退款方式：" + mode;
        if (!string.IsNullOrWhiteSpace(request.VoucherNo)) text += "，凭证号：" + request.VoucherNo.Trim();
        if (!string.IsNullOrWhiteSpace(request.Remark)) text += "，" + request.Remark!.Trim();
        return text;
    }

    private static void WriteBalanceAdjust(SqlSession db, int personId, decimal adjust)
    {
        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_PersonAccount WHERE Person_ID = @id", SqlSession.P("@id", personId)) > 0)
        {
            db.Exec(@"UPDATE dbo.ST_PersonAccount
                         SET Back_Fund = ISNULL(Back_Fund, 0) + @adjust, Remark = 'FOR-DELETE-BALANCE'
                       WHERE Person_ID = @id",
                SqlSession.P("@adjust", adjust), SqlSession.P("@id", personId));
        }
        else
        {
            db.Exec(@"INSERT INTO dbo.ST_PersonAccount
                        (Person_ID, Card_Foregift, Card_Charge, Supply_Fund, Subsidy_Fund, Take_Fund,
                         AlterCard_Fund, Transfer_Fund, Account_Balance, Back_Foregift, Back_Fund,
                         Back_SmallFund, EM_ConsumeFund, XF_ConsumeFund, Remark)
                      VALUES (@id, 0, 0, 0, 0, 0, 0, 0, 0, 0, @adjust, 0, 0, 0, 'FOR-DELETE-BALANCE')",
                SqlSession.P("@id", personId), SqlSession.P("@adjust", adjust));
        }
    }
}

/* ---------------- 行与请求 ---------------- */

public class CardAccountRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public int? deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public string? cardNo { get; set; }
    public string? cardSn { get; set; }
    public bool? personIsDel { get; set; }
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? cardState { get; set; }
    public string? cardStateText { get; set; }
    public string[]? allowedActions { get; set; }
    public decimal? balance { get; set; }
    public decimal? smallBalance { get; set; }
    public decimal? subsidyFund { get; set; }
    public decimal? icBalance { get; set; }
    public decimal? foregift { get; set; }
    public decimal? charge { get; set; }
    public DateTime? newDate { get; set; }
    public DateTime? validDate { get; set; }
    public string? cardUserNo { get; set; }
    public DateTime? updateTime { get; set; }
    public int? validity { get; set; }
    public decimal? minBalance { get; set; }
    public decimal? sfMaxFund { get; set; }
    public decimal? sfMaxDayFund { get; set; }
    public int? sfLimitTimes { get; set; }
    public decimal? maxIcFunds { get; set; }
    public int? smallDiscount { get; set; }
    public decimal? smallAccountMax { get; set; }
    public int? blackDays { get; set; }
    public string? cardPassword { get; set; }
    public int pwdConfigured { get; set; }
    public DateTime? lastLockTime { get; set; }
    public DateTime? lastUnlockTime { get; set; }
}

public class CardPersonLite
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public bool? isDel { get; set; }
}

public class CardTypeLite
{
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? validity { get; set; }
    public bool? isDel { get; set; }
}

public class CardInfoLite
{
    public decimal? foregift { get; set; }
    public decimal? charge { get; set; }
    public decimal? smallAccountMax { get; set; }
    public int? blackDays { get; set; }
}

public class CardCloseRecordRow
{
    public long idKey { get; set; }
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? cardNo { get; set; }
    public DateTime? newDate { get; set; }
    public DateTime? delDate { get; set; }
    public decimal? backFund { get; set; }
    public decimal? factFund { get; set; }
    public decimal? backSmallFund { get; set; }
    public decimal? backForegift { get; set; }
    public decimal? cardCharge { get; set; }
    public string? userNo { get; set; }
    public string? newUserNo { get; set; }
    public string? remark { get; set; }
}

public class CardOpenRequest
{
    public int PersonId { get; set; }
    public string? TypeNo { get; set; }
    public string? CardNo { get; set; }
    public string? CardSn { get; set; }
    public string? Password { get; set; }
    public string? ValidDate { get; set; }
    public decimal? MainFund { get; set; }
    public decimal? SmallFund { get; set; }
    public bool ConfirmBlacklist { get; set; }
    public string? Remark { get; set; }
    public string? RequestNo { get; set; }
}

public class CardUpdateRequest
{
    public string? TypeNo { get; set; }
    public string? ValidDate { get; set; }
    public string? Password { get; set; }
    public bool ConfirmTypeChange { get; set; }
}

public class CardCloseRequest
{
    public decimal? RefundFund { get; set; }
    public decimal? ReadCardBalance { get; set; }
    public string? RefundMode { get; set; }
    public string? VoucherNo { get; set; }
    public bool DeletePerson { get; set; }
    public bool HasCard { get; set; } = true;
    public bool ConfirmLost { get; set; }
    public bool Confirmed { get; set; }
    public string? ConfirmText { get; set; }
    public string? Remark { get; set; }
    public string? RequestNo { get; set; }
}

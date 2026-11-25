using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 账户类别设置：维护 ST_CardType 的全部业务字段（BS 不裁剪字段，否则限额与补贴规则失效）。
/// 停用只置 Is_Del = 1，不删除记录；删除动作不提供，统一走停用。
/// </summary>
[Route("api/card/account-types")]
[ApiController]
public class CardAccountTypesController : ControllerBase
{
    private readonly AuditService _audit;

    public CardAccountTypesController(AuditService audit)
    {
        _audit = audit;
    }

    private const string TypeSelect = @"
SELECT RTRIM(t.Type_No)     AS typeNo,
       t.Type_Name          AS typeName,
       ISNULL(t.Validity, 0) AS validity,
       ISNULL(t.Min_Balance, 0) AS minBalance,
       ISNULL(t.Subsidy, 0) AS subsidy,
       ISNULL(t.Max_IcFunds, 0) AS maxIcFunds,
       ISNULL(t.SF_Max_Fund, 0) AS sfMaxFund,
       ISNULL(t.SF_Max_DayFund, 0) AS sfMaxDayFund,
       ISNULL(t.SF_LimitTimes, 0) AS sfLimitTimes,
       ISNULL(t.SF_Max_Count, 0) AS sfMaxCount,
       ISNULL(t.SF_Discount, 100) AS sfDiscount,
       ISNULL(t.LH_Max_Fund, 0) AS lhMaxFund,
       ISNULL(t.LH_Max_DayFund, 0) AS lhMaxDayFund,
       ISNULL(t.LH_Discount, 100) AS lhDiscount,
       ISNULL(t.Small_Discount, 100) AS smallDiscount,
       ISNULL(t.Is_OnlySubsidy, 0) AS isOnlySubsidy,
       ISNULL(t.Is_Del, 0) AS isDel,
       t.Use_MOC            AS useMoc,
       t.Use_Repast         AS useRepast,
       t.Remark             AS remark,
       t.type_sign          AS typeSign,
       (SELECT COUNT(1) FROM dbo.ST_Card c WHERE RTRIM(c.Type_No) = RTRIM(t.Type_No)) AS cardCount
  FROM dbo.ST_CardType t";

    /// <summary>GET api/card/account-types：卡类列表，附下一个可用编号。</summary>
    [HttpGet]
    [RequirePermission("card:accountType:view")]
    public IActionResult List([FromQuery] string? keyword, [FromQuery] bool includeDisabled = true)
    {
        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = " WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where += " AND (t.Type_No LIKE @kw OR t.Type_Name LIKE @kw)";
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
        }
        if (!includeDisabled) where += " AND ISNULL(t.Is_Del, 0) = 0";

        var items = db.Query<CardTypeRow>(TypeSelect + where + " ORDER BY t.Type_No", parameters.ToArray());
        return Api.Ok(new { total = items.Count, items, nextNo = NextTypeNo(db) });
    }

    /// <summary>GET api/card/account-types/{typeNo}：卡类详情。</summary>
    [HttpGet("{typeNo}")]
    [RequirePermission("card:accountType:view")]
    public IActionResult Detail(string typeNo)
    {
        var no = CardBiz.NormalizeTypeNo(typeNo);
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardTypeRow>(TypeSelect + " WHERE RTRIM(t.Type_No) = @no", SqlSession.P("@no", no));
        if (row == null) return Api.Fail(404, "40405", "卡类不存在");
        return Api.Ok(row);
    }

    /// <summary>POST api/card/account-types：新增卡类。押金、卡费与小账户上限属于全局卡务参数，不在此写入。</summary>
    [HttpPost]
    [RequirePermission("card:accountType:create")]
    public IActionResult Create([FromBody] CardTypeSaveRequest request)
    {
        var name = (request.TypeName ?? string.Empty).Trim();
        if (!CardRules.IsValidName(name)) return Api.Fail(400, "40005", "卡类名称必填，长度 1 至 20 位");

        var error = ValidateFields(request);
        if (error != null) return error;

        using var db = SqlAccess.Open();
        var typeNo = CardBiz.NormalizeTypeNo(request.TypeNo);
        if (typeNo.Length == 0) typeNo = NextTypeNo(db);
        if (typeNo.Length == 0)
            return Api.Fail(400, "40005", "卡类编号已用满 3 位，请手工录入字母编号");
        if (typeNo.Length > 3)
            return Api.Fail(400, "40005", "卡类编号为 1 至 3 位");

        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardType WHERE RTRIM(Type_No) = @no", SqlSession.P("@no", typeNo)) > 0)
            return Api.Fail(409, "40907", "卡类编号已存在");
        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardType WHERE Type_Name = @name", SqlSession.P("@name", name)) > 0)
            return Api.Fail(409, "40906", "卡类名称已存在");

        db.Begin();
        try
        {
            db.Exec(@"
INSERT INTO dbo.ST_CardType
    (Type_No, Type_Name, Min_Balance, Subsidy, Validity, Use_MOC,
     SF_Max_Fund, SF_Max_DayFund, SF_LimitTimes, Max_IcFunds, SF_Max_Count, SF_Discount,
     LH_Max_Fund, LH_Max_DayFund, LH_Discount, Use_Repast, Small_Discount,
     Is_Del, Is_OnlySubsidy, Remark, type_sign)
VALUES
    (@no, @name, @minBalance, @subsidy, @validity, @useMoc,
     @sfMaxFund, @sfMaxDayFund, @sfLimitTimes, @maxIcFunds, @sfMaxCount, @sfDiscount,
     @lhMaxFund, @lhMaxDayFund, @lhDiscount, @useRepast, @smallDiscount,
     0, @onlySubsidy, @remark, @typeSign)",
                SqlSession.P("@no", typeNo),
                SqlSession.P("@name", name),
                SqlSession.P("@minBalance", request.MinBalance ?? 0m),
                SqlSession.P("@subsidy", request.Subsidy ?? 0m),
                SqlSession.P("@validity", request.Validity ?? 365),
                SqlSession.P("@useMoc", CardBiz.Cut(request.UseMoc, 3000)),
                SqlSession.P("@sfMaxFund", request.SfMaxFund ?? 0m),
                SqlSession.P("@sfMaxDayFund", request.SfMaxDayFund ?? 0m),
                SqlSession.P("@sfLimitTimes", request.SfLimitTimes ?? 0),
                SqlSession.P("@maxIcFunds", request.MaxIcFunds ?? 0m),
                SqlSession.P("@sfMaxCount", request.SfMaxCount ?? 0),
                SqlSession.P("@sfDiscount", request.SfDiscount ?? 100),
                SqlSession.P("@lhMaxFund", request.LhMaxFund ?? 0m),
                SqlSession.P("@lhMaxDayFund", request.LhMaxDayFund ?? 0m),
                SqlSession.P("@lhDiscount", request.LhDiscount ?? 100),
                SqlSession.P("@useRepast", CardBiz.Cut(request.UseRepast, 3000)),
                SqlSession.P("@smallDiscount", request.SmallDiscount ?? 100),
                SqlSession.P("@onlySubsidy", request.IsOnlySubsidy),
                SqlSession.P("@remark", CardBiz.Cut(request.Remark, 50)),
                SqlSession.P("@typeSign", CardBiz.Cut(request.TypeSign, 20)));

            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account-type", "create", "cardType", typeNo, null, request, false, ex.Message);
            return Api.ServerError("新增卡类失败：" + ex.Message);
        }

        var created = db.QuerySingle<CardTypeRow>(TypeSelect + " WHERE RTRIM(t.Type_No) = @no", SqlSession.P("@no", typeNo));
        _audit.LogAndSave(HttpContext, "card-account-type", "create", "cardType", typeNo, null, created);
        return Api.Ok(created, "卡类已新增");
    }

    /// <summary>PUT api/card/account-types/{typeNo}：修改卡类，编号不可改。</summary>
    [HttpPut("{typeNo}")]
    [RequirePermission("card:accountType:update")]
    public IActionResult Update(string typeNo, [FromBody] CardTypeSaveRequest request)
    {
        var no = CardBiz.NormalizeTypeNo(typeNo);
        var name = (request.TypeName ?? string.Empty).Trim();
        if (!CardRules.IsValidName(name)) return Api.Fail(400, "40005", "卡类名称必填，长度 1 至 20 位");

        var error = ValidateFields(request);
        if (error != null) return error;

        using var db = SqlAccess.Open();
        var before = db.QuerySingle<CardTypeRow>(TypeSelect + " WHERE RTRIM(t.Type_No) = @no", SqlSession.P("@no", no));
        if (before == null) return Api.Fail(404, "40405", "卡类不存在");
        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardType WHERE Type_Name = @name AND RTRIM(Type_No) <> @no",
                SqlSession.P("@name", name), SqlSession.P("@no", no)) > 0)
            return Api.Fail(409, "40906", "卡类名称已存在");

        db.Begin();
        try
        {
            db.Exec(@"
UPDATE dbo.ST_CardType
   SET Type_Name = @name, Min_Balance = @minBalance, Subsidy = @subsidy, Validity = @validity,
       Use_MOC = @useMoc, SF_Max_Fund = @sfMaxFund, SF_Max_DayFund = @sfMaxDayFund,
       SF_LimitTimes = @sfLimitTimes, Max_IcFunds = @maxIcFunds, SF_Max_Count = @sfMaxCount,
       SF_Discount = @sfDiscount, LH_Max_Fund = @lhMaxFund, LH_Max_DayFund = @lhMaxDayFund,
       LH_Discount = @lhDiscount, Use_Repast = @useRepast, Small_Discount = @smallDiscount,
       Is_OnlySubsidy = @onlySubsidy, Remark = @remark, type_sign = @typeSign
 WHERE RTRIM(Type_No) = @no",
                SqlSession.P("@no", no),
                SqlSession.P("@name", name),
                SqlSession.P("@minBalance", request.MinBalance ?? 0m),
                SqlSession.P("@subsidy", request.Subsidy ?? 0m),
                SqlSession.P("@validity", request.Validity ?? 365),
                SqlSession.P("@useMoc", CardBiz.Cut(request.UseMoc, 3000)),
                SqlSession.P("@sfMaxFund", request.SfMaxFund ?? 0m),
                SqlSession.P("@sfMaxDayFund", request.SfMaxDayFund ?? 0m),
                SqlSession.P("@sfLimitTimes", request.SfLimitTimes ?? 0),
                SqlSession.P("@maxIcFunds", request.MaxIcFunds ?? 0m),
                SqlSession.P("@sfMaxCount", request.SfMaxCount ?? 0),
                SqlSession.P("@sfDiscount", request.SfDiscount ?? 100),
                SqlSession.P("@lhMaxFund", request.LhMaxFund ?? 0m),
                SqlSession.P("@lhMaxDayFund", request.LhMaxDayFund ?? 0m),
                SqlSession.P("@lhDiscount", request.LhDiscount ?? 100),
                SqlSession.P("@useRepast", CardBiz.Cut(request.UseRepast, 3000)),
                SqlSession.P("@smallDiscount", request.SmallDiscount ?? 100),
                SqlSession.P("@onlySubsidy", request.IsOnlySubsidy),
                SqlSession.P("@remark", CardBiz.Cut(request.Remark, 50)),
                SqlSession.P("@typeSign", CardBiz.Cut(request.TypeSign, 20)));
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account-type", "update", "cardType", no, before, request, false, ex.Message);
            return Api.ServerError("修改卡类失败：" + ex.Message);
        }

        var after = db.QuerySingle<CardTypeRow>(TypeSelect + " WHERE RTRIM(t.Type_No) = @no", SqlSession.P("@no", no));
        _audit.LogAndSave(HttpContext, "card-account-type", "update", "cardType", no, before, after);
        return Api.Ok(after, "卡类已保存，新限额立即对新旧卡生效");
    }

    /// <summary>PUT api/card/account-types/{typeNo}/status：停用或启用（删除不提供）。</summary>
    [HttpPut("{typeNo}/status")]
    [RequirePermission("card:accountType:delete")]
    public IActionResult SetStatus(string typeNo, [FromBody] CardTypeStatusRequest request)
    {
        var no = CardBiz.NormalizeTypeNo(typeNo);
        using var db = SqlAccess.Open();
        var exists = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardType WHERE RTRIM(Type_No) = @no", SqlSession.P("@no", no));
        if (exists == 0) return Api.Fail(404, "40405", "卡类不存在");

        if (request.Disabled)
        {
            var occupied = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card WHERE RTRIM(Type_No) = @no", SqlSession.P("@no", no));
            if (occupied > 0)
                return Api.Fail(409, "40908", $"该卡类下仍有 {occupied} 个未销户账户，请先处理后再停用");
        }

        var affected = db.Exec("UPDATE dbo.ST_CardType SET Is_Del = @del WHERE RTRIM(Type_No) = @no",
            SqlSession.P("@del", request.Disabled), SqlSession.P("@no", no));
        if (affected == 0) return Api.Fail(404, "40405", "卡类不存在");

        _audit.LogAndSave(HttpContext, "card-account-type", request.Disabled ? "disable" : "enable", "cardType", no,
            new { isDel = !request.Disabled }, new { isDel = request.Disabled });
        return Api.Ok(new { typeNo = no, isDel = request.Disabled }, request.Disabled ? "卡类已停用" : "卡类已启用");
    }

    /// <summary>卡类编号生成：取现有纯数字编号最大值加一，保留原位数，超过 3 位时提示手工录入。</summary>
    private static string NextTypeNo(SqlSession db)
    {
        var rows = db.Query<CardTypeNoRow>(@"
SELECT RTRIM(Type_No) AS typeNo FROM dbo.ST_CardType
 WHERE RTRIM(Type_No) NOT LIKE '%[^0-9]%' AND LEN(RTRIM(Type_No)) BETWEEN 1 AND 3");
        var max = 0;
        var width = 2;
        foreach (var row in rows)
        {
            var text = (row.typeNo ?? string.Empty).Trim();
            if (text.Length == 0) continue;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > max)
            {
                max = value;
                width = text.Length;
            }
        }
        var next = max + 1;
        if (next > 999) return string.Empty;
        var result = next.ToString(CultureInfo.InvariantCulture);
        return result.Length >= width ? result : result.PadLeft(width, '0');
    }

    private IActionResult? ValidateFields(CardTypeSaveRequest request)
    {
        if (request.Validity is < 1 or > 3650) return Api.Fail(400, "40009", "有效期天数必须在 1 至 3650 之间");
        foreach (var (label, value) in new (string, decimal?)[]
                 {
                     ("最低余额", request.MinBalance), ("补贴标准", request.Subsidy), ("IC 离线限额", request.MaxIcFunds),
                     ("售饭单次上限", request.SfMaxFund), ("售饭日上限", request.SfMaxDayFund),
                     ("计次上限", request.SfMaxCount), ("灵活消费单次上限", request.LhMaxFund),
                     ("灵活消费日上限", request.LhMaxDayFund),
                 })
        {
            if (!CardBiz.IsValidMoney(value, true, out var error)) return Api.Fail(400, "40009", label + error);
        }
        foreach (var (label, value) in new (string, int?)[]
                 {
                     ("售饭次数", request.SfLimitTimes), ("售饭折扣", request.SfDiscount),
                     ("灵活消费折扣", request.LhDiscount), ("小账户折扣", request.SmallDiscount),
                 })
        {
            if (value is < 0 or > 100) return Api.Fail(400, "40009", label + "必须在 0 至 100 之间");
        }
        return null;
    }
}

public class CardTypeRow
{
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? validity { get; set; }
    public decimal? minBalance { get; set; }
    public decimal? subsidy { get; set; }
    public decimal? maxIcFunds { get; set; }
    public decimal? sfMaxFund { get; set; }
    public decimal? sfMaxDayFund { get; set; }
    public int? sfLimitTimes { get; set; }
    public int? sfMaxCount { get; set; }
    public int? sfDiscount { get; set; }
    public decimal? lhMaxFund { get; set; }
    public decimal? lhMaxDayFund { get; set; }
    public int? lhDiscount { get; set; }
    public int? smallDiscount { get; set; }
    public bool? isOnlySubsidy { get; set; }
    public bool? isDel { get; set; }
    public string? useMoc { get; set; }
    public string? useRepast { get; set; }
    public string? remark { get; set; }
    public string? typeSign { get; set; }
    public int cardCount { get; set; }
}

public class CardTypeNoRow
{
    public string? typeNo { get; set; }
}

public class CardTypeSaveRequest
{
    public string? TypeNo { get; set; }
    public string? TypeName { get; set; }
    public int? Validity { get; set; }
    public decimal? MinBalance { get; set; }
    public decimal? Subsidy { get; set; }
    public decimal? MaxIcFunds { get; set; }
    public decimal? SfMaxFund { get; set; }
    public decimal? SfMaxDayFund { get; set; }
    public int? SfLimitTimes { get; set; }
    public int? SfMaxCount { get; set; }
    public int? SfDiscount { get; set; }
    public decimal? LhMaxFund { get; set; }
    public decimal? LhMaxDayFund { get; set; }
    public int? LhDiscount { get; set; }
    public int? SmallDiscount { get; set; }
    public bool IsOnlySubsidy { get; set; }
    public string? UseMoc { get; set; }
    public string? UseRepast { get; set; }
    public string? Remark { get; set; }
    public string? TypeSign { get; set; }
}

public class CardTypeStatusRequest
{
    public bool Disabled { get; set; }
}

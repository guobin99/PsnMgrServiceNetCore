using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 账户信息设置：维护全局卡务参数（押金、卡费、初始密码、小账户上限、补贴截止、
/// 黑名单保留天数与卡片规格）。ST_CardInfo 为全局单记录参数表，全表仅一条，
/// 不与 ST_CardType 或其它表按字段关联。CS 的保存语句没有 WHERE，
/// 因此 BS 在保存前必须确认全表只有一条记录，多于一条视为数据异常。
/// </summary>
[Route("api/card/account-info")]
[ApiController]
public class CardAccountInfoController : ControllerBase
{
    private readonly AuditService _audit;

    public CardAccountInfoController(AuditService audit)
    {
        _audit = audit;
    }

    private const string SingleSelect = @"
SELECT TOP 1
       RTRIM(ci.Card_Type)                  AS cardType,
       ISNULL(ci.Card_Foregift, 0)          AS foregift,
       ISNULL(ci.Card_Charge, 0)            AS charge,
       ISNULL(ci.SmallAccount_Max, 0)       AS smallAccountMax,
       ci.Subsidy_CutOffDate                AS subsidyCutOffDate,
       ISNULL(ci.Is_DelSubsidy, 0)          AS isDelSubsidy,
       ISNULL(ci.Day_DelSysBlack, 182)      AS dayDelSysBlack,
       ISNULL(ci.Card_Spec, 0)              AS cardSpec,
       CASE WHEN LEN(ISNULL(ci.Init_Code, '')) > 0 THEN 1 ELSE 0 END AS initCodeConfigured,
       CASE WHEN LEN(ISNULL(ci.Passwd_Value, '')) > 0 THEN 1 ELSE 0 END AS passwdConfigured
  FROM dbo.ST_CardInfo ci
 ORDER BY ci.Card_Type";

    /// <summary>GET api/card/account-info：查询全局账户信息（恒为同一条记录）。</summary>
    [HttpGet]
    [RequirePermission("card:account:view")]
    public IActionResult Get()
    {
        using var db = SqlAccess.Open();
        var count = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardInfo");
        if (count == 0)
            return Api.Fail(404, "40404", "账户信息尚未初始化，请先在卡务参数中建立唯一记录");

        var row = db.QuerySingle<CardAccountInfoRow>(SingleSelect);
        if (row == null) return Api.Fail(404, "40404", "账户信息不存在，请刷新后重试");

        row.recordCount = count;
        row.idCardLen = CardBiz.ConfigInt(db, "IDCardLen", 10);
        FillLastModified(db, row);
        return Api.Ok(row);
    }

    /// <summary>
    /// PUT api/card/account-info：保存全局账户信息。
    /// 三条写语句在同一事务内：更新唯一记录、参数 IDCardLen、记录标识含 ID 时清零全部卡类 Is_OnlySubsidy。
    /// </summary>
    [HttpPut]
    [RequirePermission("card:account:update")]
    public IActionResult Save([FromBody] CardAccountInfoSaveRequest request)
    {
        if (!CardBiz.IsValidMoney(request.Foregift, true, out var moneyError))
            return Api.Fail(400, "40009", "押金" + moneyError);
        if (!CardBiz.IsValidMoney(request.Charge, true, out moneyError))
            return Api.Fail(400, "40009", "卡费" + moneyError);
        if (!CardBiz.IsValidMoney(request.SmallAccountMax, true, out moneyError))
            return Api.Fail(400, "40009", "小账户上限" + moneyError);
        if ((request.Foregift ?? 0m) > 99999m) return Api.Fail(400, "40009", "押金不能超过 99999");
        if ((request.Charge ?? 0m) > 99999m) return Api.Fail(400, "40009", "卡费不能超过 99999");
        if (request.DayDelSysBlack is < 0 or > 3650)
            return Api.Fail(400, "40009", "黑名单保留天数必须在 0 至 3650 之间");
        if (request.CardSpec is < 0 or > 1)
            return Api.Fail(400, "40009", "卡片型号只能取 0（IC-MF1S50）或 1（CPU-FMCOS2）");
        if (request.IdCardLen.HasValue &&
            request.IdCardLen.Value != 10 && request.IdCardLen.Value != 8 && request.IdCardLen.Value != 6)
            return Api.Fail(400, "40009", "ID 卡读卡长度只能取 10、8 或 6");

        var newCardType = (request.CardType ?? string.Empty).Trim();
        if (newCardType.Length > 0 && newCardType != "IC卡" && newCardType != "ID卡")
            return Api.Fail(400, "40009", "卡类型只能选择 IC卡 或 ID卡");

        // 密码留空表示保持原值；非空必须用 EncryptDB 加密后写库（与 CS 端存储口径一致）。
        // EncryptStr 输出大写 HEX，每 8 字节明文对应 16 字符，Passwd_Value 为 varchar(50)，
        // 因此明文最多 24 位，这里提前拦截避免写库截断。
        string? passwdCipher = null;
        if (!string.IsNullOrWhiteSpace(request.PasswdValue))
        {
            var plain = request.PasswdValue!.Trim();
            if (plain.Length > 24)
                return Api.Fail(400, "40012", "卡初始密码不能超过 24 位");
            passwdCipher = new EncryptDB().EncryptStr(plain);
            if (passwdCipher.Length > 50)
                return Api.Fail(400, "40012", "卡初始密码加密后超出字段长度，请缩短密码");
        }

        DateTime? cutOff = CardRules.ParseDate(request.SubsidyCutOffDate);
        if (!string.IsNullOrWhiteSpace(request.SubsidyCutOffDate) && cutOff == null)
            return Api.Fail(400, "40011", "补贴截止日期格式非法");
        if (cutOff != null && cutOff.Value.Date < DateTime.Today)
            return Api.Fail(400, "40011", "补贴截止日期不能早于今天");

        using var db = SqlAccess.Open();
        db.Begin();

        CardAccountInfoRow? before;
        string cardType;
        string targetCardType;
        try
        {
            // 0 定位唯一记录并加锁：0 条说明未初始化，多条属于数据异常，均拒绝保存
            var count = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardInfo WITH (UPDLOCK, HOLDLOCK)");
            if (count == 0)
            {
                db.Rollback();
                _audit.LogAndSave(HttpContext, "card-account", "save", "cardInfo", null, null, request, false,
                    "账户信息缺失，需先初始化卡务参数");
                return Api.Fail(404, "40404", "账户信息缺失，请先初始化卡务参数");
            }
            if (count > 1)
            {
                db.Rollback();
                _audit.LogAndSave(HttpContext, "card-account", "save", "cardInfo", null, null, request, false,
                    "检测到 " + count + " 条账户信息记录，需先清理为单条");
                return Api.Fail(404, "40404", "检测到 " + count + " 条账户信息记录，请先清理为单条后再保存");
            }

            before = db.QuerySingle<CardAccountInfoRow>(SingleSelect);
            cardType = before?.cardType ?? string.Empty;
            if (cardType.Length == 0)
            {
                db.Rollback();
                return Api.Fail(404, "40404", "账户信息记录标识为空，请先修复卡务参数");
            }

            targetCardType = newCardType.Length > 0 ? newCardType : cardType;

            // 1 更新唯一记录（CS 原语句无 WHERE，BS 以主键精确定位）
            //    Passwd_Value 留空时保持原值；Subsidy_CutOffDate 未传时也保持原值（页面已不展示该项）
            var affected = db.Exec(@"
UPDATE dbo.ST_CardInfo
   SET Card_Type = @newCardType, Card_Foregift = @foregift, Card_Charge = @charge,
       Passwd_Value = CASE WHEN @passwd IS NULL THEN Passwd_Value ELSE @passwd END,
       SmallAccount_Max = @smallMax,
       Subsidy_CutOffDate = CASE WHEN @keepCutOff = 1 THEN Subsidy_CutOffDate ELSE @cutOff END,
       Is_DelSubsidy = @delSubsidy, Day_DelSysBlack = @blackDays, Card_Spec = @spec
 WHERE Card_Type = @cardType;",
                SqlSession.P("@cardType", cardType),
                SqlSession.P("@newCardType", targetCardType),
                SqlSession.P("@foregift", request.Foregift ?? 0m),
                SqlSession.P("@charge", request.Charge ?? 0m),
                SqlSession.P("@passwd", passwdCipher),
                SqlSession.P("@smallMax", request.SmallAccountMax ?? 0m),
                SqlSession.P("@keepCutOff", request.SubsidyCutOffDate == null ? 1 : 0),
                SqlSession.P("@cutOff", cutOff),
                SqlSession.P("@delSubsidy", request.IsDelSubsidy),
                SqlSession.P("@blackDays", request.DayDelSysBlack ?? 182),
                SqlSession.P("@spec", request.CardSpec ?? 0));
            if (affected != 1)
            {
                db.Rollback();
                _audit.LogAndSave(HttpContext, "card-account", "save", "cardInfo", cardType, before, request, false,
                    "唯一记录更新命中 " + affected + " 行");
                return Api.Fail(404, "40404", "账户信息记录异常，更新命中 " + affected + " 行，请刷新后重试");
            }

            // 2 系统参数 IDCardLen（CS 物理表 ST_SysParam）
            var idCardLen = (request.IdCardLen ?? 10).ToString(CultureInfo.InvariantCulture);
            db.Exec(@"
IF EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = 'IDCardLen')
    UPDATE dbo.ST_SysParam SET Param_Value = @len WHERE Param_Code = 'IDCardLen';
ELSE
    INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value) VALUES ('IDCardLen', 'ID卡读卡长度', @len);",
                SqlSession.P("@len", idCardLen));

            // 3 记录标识含 ID 时，CS 行为是清零全部卡类的 Is_OnlySubsidy（只使用补贴），此处保持一致
            if (targetCardType.ToUpperInvariant().Contains("ID"))
                db.Exec("UPDATE dbo.ST_CardType SET Is_OnlySubsidy = 0");

            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-account", "save", "cardInfo", null, null, request, false, ex.Message);
            return Api.ServerError("保存账户信息失败：" + ex.Message);
        }

        var after = db.QuerySingle<CardAccountInfoRow>(SingleSelect);
        if (after != null)
        {
            after.recordCount = 1;
            after.idCardLen = CardBiz.ConfigInt(db, "IDCardLen", 10);
            FillLastModified(db, after);
        }
        _audit.LogAndSave(HttpContext, "card-account", "save", "cardInfo", targetCardType, before, after);
        return Api.Ok(after, "账户信息已保存，仅影响后续开户");
    }

    /// <summary>
    /// POST api/card/account-info/init-code：写入读卡代理生成的初始密钥。
    /// 读卡不在事务内（外部依赖），事务只包住最后的 UPDATE。
    /// </summary>
    [HttpPost("init-code")]
    [RequirePermission("card:account:update")]
    public IActionResult GenerateInitCode([FromBody] CardInitCodeRequest request)
    {
        var code = (request?.InitCode ?? string.Empty).Trim();
        if (code.Length == 0)
            return Api.Fail(422, "42201", "需要读卡代理读取卡内密钥后再提交，BS 不直接操作硬件");
        if (code.Length > 100) return Api.Fail(400, "40009", "初始密钥长度不能超过 100 位");

        using var db = SqlAccess.Open();
        var count = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_CardInfo");
        if (count == 0) return Api.Fail(404, "40404", "账户信息尚未初始化，请先保存账户信息");
        if (count > 1) return Api.Fail(404, "40404", "检测到 " + count + " 条账户信息记录，请先清理为单条");

        var cardType = db.Scalar("SELECT TOP 1 RTRIM(Card_Type) FROM dbo.ST_CardInfo") as string ?? string.Empty;
        var before = db.Scalar("SELECT Init_Code FROM dbo.ST_CardInfo WHERE Card_Type = @cardType",
            SqlSession.P("@cardType", cardType)) as string;

        var affected = db.Exec("UPDATE dbo.ST_CardInfo SET Init_Code = @code WHERE Card_Type = @cardType",
            SqlSession.P("@code", code), SqlSession.P("@cardType", cardType));
        if (affected == 0) return Api.Fail(404, "40404", "账户信息不存在，请刷新后重试");

        _audit.LogAndSave(HttpContext, "card-account", "init-code", "cardInfo", cardType,
            new { initCode = string.IsNullOrEmpty(before) ? "未配置" : "已配置" }, new { initCode = "已配置" });
        return Api.Ok(new { cardType, initCodeConfigured = 1 }, "初始密钥已配置");
    }

    /// <summary>最近修改时间与操作员取自 BS 操作日志，ST_CardInfo 本身没有时间列。</summary>
    private static void FillLastModified(SqlSession db, CardAccountInfoRow row)
    {
        var log = db.QuerySingle<CardInfoLastModified>(@"
SELECT TOP 1 Opt_Time AS updateTime, User_No AS updateUser
  FROM dbo.BS_OperLog
 WHERE Module = 'card-account' AND Target = 'cardInfo' AND Result = 0
 ORDER BY Opt_Time DESC, ID_KEY DESC");
        if (log == null) return;
        row.updateTime = log.updateTime;
        row.updateUser = log.updateUser;
    }
}

public class CardAccountInfoRow
{
    public string? cardType { get; set; }
    public decimal? foregift { get; set; }
    public decimal? charge { get; set; }
    public decimal? smallAccountMax { get; set; }
    public DateTime? subsidyCutOffDate { get; set; }
    public bool? isDelSubsidy { get; set; }
    public int? dayDelSysBlack { get; set; }
    public int? cardSpec { get; set; }
    public int initCodeConfigured { get; set; }
    public int passwdConfigured { get; set; }
    public int idCardLen { get; set; }
    /// <summary>ST_CardInfo 实际记录数，正常恒为 1，大于 1 时前端提示数据异常。</summary>
    public int recordCount { get; set; }
    public DateTime? updateTime { get; set; }
    public string? updateUser { get; set; }
}

public class CardInfoLastModified
{
    public DateTime? updateTime { get; set; }
    public string? updateUser { get; set; }
}

public class CardAccountInfoSaveRequest
{
    /// <summary>卡类型：IC卡 / ID卡；不传表示不修改（ST_CardInfo 主键）。</summary>
    public string? CardType { get; set; }
    public decimal? Foregift { get; set; }
    public decimal? Charge { get; set; }
    public string? PasswdValue { get; set; }
    public decimal? SmallAccountMax { get; set; }
    public string? SubsidyCutOffDate { get; set; }
    public bool IsDelSubsidy { get; set; }
    public int? DayDelSysBlack { get; set; }
    public int? CardSpec { get; set; }
    public int? IdCardLen { get; set; }
}

public class CardInitCodeRequest
{
    public string? InitCode { get; set; }
}

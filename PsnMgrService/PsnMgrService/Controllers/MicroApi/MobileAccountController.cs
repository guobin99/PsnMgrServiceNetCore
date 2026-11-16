using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 移动端帐户服务（资料/改密/换手机号/充值记录/消费记录）。
/// 与 PsnMgrService(Net) 的 MobileAccountController 等价（路由 api/m/account/*）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m/account")]
public partial class MobileAccountController : ControllerBase
{
    private readonly AppDbContext _db;
    public MobileAccountController(AppDbContext db) => _db = db;

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    /// <summary>
    /// 充值记录报表（ST_SupplyFund）：
    ///   Supply_Date 充值时间、Supply_Fund 充值金额、Card_Balance 充值前余额、
    ///   Out_Trade_No 微信商户单号（空=刷卡充值，非空=微信充值）。
    /// 与 /consumptions 一致按时间区间过滤（默认近 30 天）。
    /// </summary>
    [HttpGet("rechargerecords")]
    public async Task<IActionResult> RechargeRecords(string bgn = "", string end = "", int page = 1, int size = 20)
    {
        var dtBgn = ParseDate(bgn, DateTime.Now.AddDays(-30));
        var dtEnd = ParseDate(end, DateTime.Now).AddDays(1);
        if (dtBgn > dtEnd) return Ok(new List<object>());
        if (page < 1) page = 1;
        if (size < 1) size = 20;

        // SQL Server 2008 不支持 OFFSET/FETCH，分页用 ROW_NUMBER
        var rows = await QueryAsync<RechargeRow>(@"
SELECT * FROM (
    SELECT ID_KEY, Supply_Date, Supply_Fund, Card_Balance, Out_Trade_No, Remark,
           ROW_NUMBER() OVER (ORDER BY Supply_Date DESC) AS RowNum
    FROM ST_SupplyFund
    WHERE Person_ID = @pid AND Supply_Date >= @bgn AND Supply_Date <= @end
) T
WHERE T.RowNum BETWEEN @start AND @endRow
ORDER BY T.Supply_Date DESC",
            new { pid = Pid, bgn = dtBgn, end = dtEnd, start = (page - 1) * size + 1, endRow = page * size });

        var q = rows.Select(s => new
        {
            id = s.ID_KEY,
            time = s.Supply_Date,
            amount = s.Supply_Fund,
            // Card_Balance 是充值前余额；充值后 = 充值前 + 本次充值
            beforeFund = s.Card_Balance ?? 0,
            balance = (s.Card_Balance ?? 0) + s.Supply_Fund,
            channel = string.IsNullOrEmpty(s.Out_Trade_No) ? "刷卡充值" : "微信充值",
            outTradeNo = s.Out_Trade_No,
            remark = s.Remark
        }).ToList();
        return Ok(q);
    }

    /// <summary>
    /// 消费记录报表（XF_Consume）：Consume_Time / Consume_Fund / Card_Balance（消费后余额）/
    /// Repast_No（名称取 SF_Repast）/ COM_No / MOC_No（名称取 ST_MOC）。
    /// </summary>
    [HttpGet("consumptions")]
    public async Task<IActionResult> Consumptions(string bgn = "", string end = "", int page = 1, int size = 20)
    {
        var dtBgn = ParseDate(bgn, DateTime.Now.AddDays(-30));
        var dtEnd = ParseDate(end, DateTime.Now).AddDays(1);
        if (dtBgn > dtEnd) return Ok(new List<object>());
        if (page < 1) page = 1;
        if (size < 1) size = 20;

        var repasts = await _db.SF_Repasts.AsNoTracking()
            .ToDictionaryAsync(r => r.Repast_No, r => r.Repast_Name);
        var mocNames = await _db.ST_MOCs.AsNoTracking()
            .ToDictionaryAsync(m => m.MOC_No, m => m.MOC_Name);

        var rows = await QueryAsync<ConsumeRow>(@"
SELECT * FROM (
    SELECT ID_KEY, Consume_Time, Consume_Fund, Before_Fund, Card_Balance, Repast_No, COM_No, MOC_No, System_No,
           Consume_Mode, Data_Type, Remark, OrderID,
           ROW_NUMBER() OVER (ORDER BY Consume_Time DESC) AS RowNum
    FROM XF_Consume
    WHERE Person_ID = @pid AND Consume_Time >= @bgn AND Consume_Time <= @end
) T
WHERE T.RowNum BETWEEN @start AND @endRow
ORDER BY T.Consume_Time DESC",
            new { pid = Pid, bgn = dtBgn, end = dtEnd, start = (page - 1) * size + 1, endRow = page * size });

        var q = rows.Select(x => new
        {
            id = x.ID_KEY,
            time = x.Consume_Time,
            amount = x.Consume_Fund,
            beforeFund = x.Before_Fund,
            balance = x.Card_Balance,
            repastNo = x.Repast_No,
            repastName = NameOf(x.Repast_No, repasts) ?? x.Repast_No,
            orderNo = x.COM_No,
            mocNo = x.MOC_No,
            mocName = NameOf(x.MOC_No, mocNames),
            area = x.System_No,
            consumeMode = x.Consume_Mode,
            dataType = x.Data_Type,
            remark = x.Remark
        }).ToList();
        return Ok(q);
    }

    private static string? NameOf(string? key, Dictionary<string, string> map)
    {
        if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var name)) return name;
        return null;
    }

    private static DateTime ParseDate(string s, DateTime def)
        => DateTime.TryParse(s, out var d) ? d : def;

    /// <summary>修改卡密码（校验旧密码）</summary>
    [HttpPost("changepwd")]
    public async Task<IActionResult> ChangePwd([FromBody] ChangePwdReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.newPassword) || req.newPassword.Length < 4)
            return Ok(new { success = false, msg = "新密码至少4位" });

        var c = await _db.ST_Cards.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        if (c == null) return NotFound();
        if (c.Card_Password != new EncryptDB().EncryptStr(req.oldPassword ?? ""))
            return Ok(new { success = false, msg = "原密码错误" });

        c.Card_Password = new EncryptDB().EncryptStr(req.newPassword);
        c.Update_Time = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>挂失/解挂：校验卡密码后更新 ST_Card.Card_State（0=正常 1=挂失）</summary>
    [HttpPost("lockcard")]
    public async Task<IActionResult> LockCard([FromBody] LockCardReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.password))
            return Ok(new { success = false, msg = "请输入密码" });

        var c = await _db.ST_Cards.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        if (c == null) return NotFound();
        if (c.Card_Password != new EncryptDB().EncryptStr(req.password))
            return Ok(new { success = false, msg = "密码校验失败" });

        bool lockAction = string.Equals(req.action, "lock", StringComparison.OrdinalIgnoreCase);
        c.Card_State = lockAction ? (byte)1 : (byte)0;
        c.Update_Time = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { success = true, status = lockAction ? "locked" : "normal" });
    }

    /// <summary>换绑手机号（短信验证）</summary>
    [HttpPost("changephone")]
    public async Task<IActionResult> ChangePhone([FromBody] ChangePhoneReq req)
    {
        if (req == null || !AliyunSmsService.Verify(req.mobile ?? "", req.code ?? "", "changephone"))
            return Ok(new { success = false, msg = "验证码错误" });

        var p = await _db.ST_People.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        if (p == null) return NotFound();

        p.mobile = req.mobile;
        p.update_time = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>重置卡密码：短信验证码 + 身份证号核验（匿名）</summary>
    [AllowAnonymous]
    [HttpPost("resetpwd")]
    public async Task<IActionResult> ResetPwd([FromBody] ResetPwdReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.newPassword) || req.newPassword.Length < 4)
            return Ok(new { success = false, msg = "新密码至少4位" });
        if (!AliyunSmsService.Verify(req.mobile ?? "", req.code ?? "", "resetpwd"))
            return Ok(new { success = false, msg = "验证码错误或已过期" });

        var p = await _db.ST_People.FirstOrDefaultAsync(x => x.mobile == req.mobile && !x.Is_Del);
        if (p == null) return Ok(new { success = false, msg = "该手机号未登记" });
        if (!string.Equals((p.id_number ?? "").Trim(), (req.idNumber ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return Ok(new { success = false, msg = "身份证号核验不通过" });

        var c = await _db.ST_Cards.FirstOrDefaultAsync(x => x.Person_ID == p.Person_ID);
        if (c == null) return NotFound();

        c.Card_Password = new EncryptDB().EncryptStr(req.newPassword);
        c.Update_Time = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class ResetPwdReq
{
    public string? mobile { get; set; }
    public string? code { get; set; }
    public string? idNumber { get; set; }
    public string? newPassword { get; set; }
}

public class ChangePwdReq
{
    public string? oldPassword { get; set; }
    public string? newPassword { get; set; }
}

public class LockCardReq
{
    public string? action { get; set; }
    public string? password { get; set; }
}

public class ChangePhoneReq
{
    public string? mobile { get; set; }
    public string? code { get; set; }
}

// ---------------- Dapper 辅助（EF Core 9 生成的 SQL 不兼容 SQL Server 2008，查询改走原生 SQL） ----------------

public partial class MobileAccountController
{
    private static async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        return await cn.QueryAsync<T>(sql, param);
    }
}

/// <summary>ST_SupplyFund 行（充值记录）</summary>
public class RechargeRow
{
    public int ID_KEY { get; set; }
    public DateTime Supply_Date { get; set; }
    public decimal Supply_Fund { get; set; }
    public decimal? Card_Balance { get; set; }
    public string? Out_Trade_No { get; set; }
    public string? Remark { get; set; }
}

/// <summary>XF_Consume 行（消费记录）</summary>
public class ConsumeRow
{
    public int ID_KEY { get; set; }
    public DateTime? Consume_Time { get; set; }
    public decimal? Consume_Fund { get; set; }
    public decimal? Before_Fund { get; set; }
    public decimal? Card_Balance { get; set; }
    public string? Repast_No { get; set; }
    public string? COM_No { get; set; }
    public string? MOC_No { get; set; }
    public string? System_No { get; set; }
    public byte? Consume_Mode { get; set; }
    public byte? Data_Type { get; set; }
    public string? Remark { get; set; }
    public string? OrderID { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 移动端认证（H5 服务号 / 小程序共用），与 PsnMgrService(Net) 的 MobileAuthController 等价。
/// 路由：api/m/auth/{login|smslogin|smscode|wxlogin|bind|unbind|profile}
///
/// Token 用 ST_Person.Person_No + ST_Card.Card_Password 校验，与后台管理员（ST_UserInfo）无关；
/// claims 里带 PersonId，所有 api/m/* 接口都靠它识别当前用户。
/// </summary>
[ApiController]
[Route("api/m/auth")]
public class MobileAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(AppDbContext db, ILogger<MobileAuthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ---------------- 登录 ----------------

    /// <summary>工号(Person_No) + 卡密码(Card_Password) 登录</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] MLogin req)
    {
        if (req == null || string.IsNullOrEmpty(req.PersonNo) || string.IsNullOrEmpty(req.Password))
            return BadRequest("参数错误");

        var encPwd = new EncryptDB().EncryptStr(req.Password);
        var row = await (from p in _db.ST_People
                         join c in _db.ST_Cards on p.Person_ID equals c.Person_ID
                         where p.Person_No == req.PersonNo && !p.Is_Del && c.Card_Password == encPwd
                         select new { p, c }).FirstOrDefaultAsync();

        if (row == null)
        {
            _logger.LogWarning("移动端登录失败：工号 {PersonNo} 工号或卡密码错误", req.PersonNo);
            return Ok(new { success = false, msg = "工号或卡密码错误" });
        }

        await AfterWxIdentityAsync(row.p, req.Code, req.Scene);
        return Ok(await TokenRespAsync(row.p, row.c.Card_Balance));
    }

    /// <summary>短信验证码登录</summary>
    [AllowAnonymous]
    [HttpPost("smslogin")]
    public async Task<IActionResult> SmsLogin([FromBody] MSmsLogin req)
    {
        if (req == null || !AliyunSmsService.Verify(req.Mobile ?? "", req.Code ?? "", "login"))
            return Ok(new { success = false, msg = "验证码错误或已过期" });

        var p = await _db.ST_People.FirstOrDefaultAsync(x => x.mobile == req.Mobile && !x.Is_Del);
        if (p == null) return Ok(new { success = false, msg = "该手机号未登记" });

        await AfterWxIdentityAsync(p, req.Code2, req.Scene);
        var balance = await _db.ST_Cards.Where(c => c.Person_ID == p.Person_ID)
            .Select(c => (decimal?)c.Card_Balance).FirstOrDefaultAsync() ?? 0m;
        return Ok(await TokenRespAsync(p, balance));
    }

    /// <summary>发送短信验证码</summary>
    [AllowAnonymous]
    [HttpGet("smscode")]
    public async Task<IActionResult> SendSms(string mobile, string scene)
    {
        if (string.IsNullOrEmpty(mobile) || !System.Text.RegularExpressions.Regex.IsMatch(mobile, @"^1[3-9]\d{9}$"))
            return Ok(new { success = false, msg = "手机号格式错误" });

        var code = AliyunSmsService.NewCode();
        AliyunSmsService.SmsSendResult r;
        try
        {
            r = await AliyunSmsService.SendAsync(mobile, code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送短信异常 {Mobile}", mobile);
            return Ok(new { success = false, msg = "短信发送失败，请稍后重试" });
        }

        if (r.Ok) AliyunSmsService.SaveCode(mobile, code, string.IsNullOrEmpty(scene) ? "login" : scene, 5);
        else _logger.LogWarning("短信发送失败 {Mobile} Code={Code} Message={Message}", mobile, r.Code, r.Message);

        // aliyunCode/aliyunMsg：把阿里云真实错误带回响应，便于排错（与 Net 版一致）
        return Ok(new
        {
            success = r.Ok,
            msg = r.Ok ? "已发送" : "短信发送失败",
            aliyunCode = r.Code,
            aliyunMsg = r.Ok ? null : r.Message
        });
    }

    /// <summary>微信 code 登录：Scene=h5(公众号 oauth code) / mp(小程序 wx.login code)</summary>
    [AllowAnonymous]
    [HttpPost("wxlogin")]
    public async Task<IActionResult> WxLogin([FromBody] MWxLogin req)
    {
        if (req == null || string.IsNullOrEmpty(req.Code))
            return Ok(new { success = false, msg = "缺少 code" });

        var id = await GetWxIdentityAsync(req.Code, req.Scene);
        if (id == null) return Ok(new { success = false, msg = "微信身份获取失败" });

        id.UnionId = string.IsNullOrEmpty(id.UnionId) ? id.OpenId : id.UnionId;

        // 与 PsnMgrService(Net) 的 MobileAuthController.WxLogin 逐字对齐（业务已确认：H5 分支的
        // `!string.IsNullOrEmpty(id.UnionId)` 条件属既定行为，不是 bug，移植时不要"顺手修正"）。
        ST_Person? p;
        if (req.Scene == "mp")
        {
            p = await _db.ST_People.FirstOrDefaultAsync(x => x.UnionID == id.UnionId && !x.Is_Del && !string.IsNullOrEmpty(id.UnionId));
        }
        else
        {
            p = await _db.ST_People.FirstOrDefaultAsync(x => x.OpenID == id.OpenId && !x.Is_Del && !string.IsNullOrEmpty(id.UnionId));
        }

        if (p == null)
            return Ok(new { success = false, needBind = true, openid = id.OpenId, unionid = id.UnionId });

        if (req.Scene == "mp" && string.IsNullOrEmpty(p.UnionID)) { p.UnionID = id.UnionId; await _db.SaveChangesAsync(); }
        if (req.Scene == "h5" && string.IsNullOrEmpty(p.OpenID)) { p.OpenID = id.OpenId; await _db.SaveChangesAsync(); }

        var balance = await _db.ST_Cards.Where(c => c.Person_ID == p.Person_ID)
            .Select(c => (decimal?)c.Card_Balance).FirstOrDefaultAsync() ?? 0m;
        return Ok(await TokenRespAsync(p, balance));
    }

    /// <summary>绑定：工号+卡密码 或 手机号+短信验证码，写入 OpenID(H5) / UnionID(小程序)</summary>
    [AllowAnonymous]
    [HttpPost("bind")]
    public async Task<IActionResult> Bind([FromBody] MBind req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });

        ST_Person? person;
        var hasPwd = !string.IsNullOrEmpty(req.Password);
        var hasSms = !string.IsNullOrEmpty(req.SmsCode);
        if (!hasPwd && !hasSms) return Ok(new { success = false, msg = "请提供密码或短信验证码" });

        if (hasPwd)
        {
            var encPwd = new EncryptDB().EncryptStr(req.Password!);
            person = await (from p in _db.ST_People
                            join c in _db.ST_Cards on p.Person_ID equals c.Person_ID
                            where p.Person_No == req.PersonNo && !p.Is_Del && c.Card_Password == encPwd
                            select p).FirstOrDefaultAsync();
            if (person == null) return Ok(new { success = false, msg = "工号或卡密码错误" });
        }
        else
        {
            if (!AliyunSmsService.Verify(req.Mobile ?? "", req.SmsCode ?? "", "bind"))
                return Ok(new { success = false, msg = "短信验证码错误" });
            person = await _db.ST_People.FirstOrDefaultAsync(x => x.mobile == req.Mobile && !x.Is_Del);
            if (person == null) return Ok(new { success = false, msg = "该手机号未登记" });
        }

        // 微信身份：优先用前端传来的 OpenId，兼容旧的 Code 兑换
        string? openId = req.OpenId, unionId = req.UnionId;
        if (string.IsNullOrEmpty(openId) && !string.IsNullOrEmpty(req.Code))
        {
            var id = await GetWxIdentityAsync(req.Code, req.Scene);
            if (id == null) return Ok(new { success = false, msg = "微信身份获取失败，请重新进入页面" });
            openId = id.OpenId;
            unionId = id.UnionId;
        }
        if (string.IsNullOrEmpty(openId))
            return Ok(new { success = false, msg = "微信身份获取失败，请重新进入页面" });

        _logger.LogInformation("Bind: UnionId={UnionId}, OpenId={OpenId}", unionId, openId);

        if (req.Scene == "mp") person.UnionID = unionId; else person.OpenID = openId;
        if (!string.IsNullOrEmpty(req.Mobile)) person.mobile = req.Mobile;
        person.update_time = DateTime.Now;
        await _db.SaveChangesAsync();

        var balance = await _db.ST_Cards.Where(c => c.Person_ID == person.Person_ID)
            .Select(c => (decimal?)c.Card_Balance).FirstOrDefaultAsync() ?? 0m;
        return Ok(await TokenRespAsync(person, balance));
    }

    /// <summary>解绑</summary>
    [Authorize]
    [HttpPost("unbind")]
    public async Task<IActionResult> Unbind([FromBody] MUnbind req)
    {
        var p = await _db.ST_People.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        if (p == null) return NotFound();

        if (req != null && req.Scene == "mp") p.UnionID = null; else p.OpenID = null;
        p.update_time = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>当前登录人资料</summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var p = await _db.ST_People.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        if (p == null) return NotFound();

        var c = await _db.ST_Cards.FirstOrDefaultAsync(x => x.Person_ID == Pid);
        var department = await GetDeptNameAsync(p.Dept_ID);

        return Ok(new
        {
            personId = p.Person_ID,
            personNo = p.Person_No,
            name = p.Person_Name,
            cardNo = p.Card_No,
            idNumber = p.id_number,
            mobile = p.mobile,
            department,
            title = p.Title ?? "",
            balance = c == null ? 0m : c.Card_Balance,
            cardState = c == null ? 0 : (c.Card_State ?? (byte)0),
            cardNewDate = c == null ? "" : c.Card_NewDate.ToString("yyyy-MM-dd"),
            cardValidDate = c == null ? "" : c.Card_ValidDate.ToString("yyyy-MM-dd"),
            openIdBound = !string.IsNullOrEmpty(p.OpenID),
            unionIdBound = !string.IsNullOrEmpty(p.UnionID),
            updateTime = p.update_time.HasValue ? p.update_time.Value.ToString("yyyy-MM-dd HH:mm") : ""
        });
    }

    // ---------------- 内部方法 ----------------

    /// <summary>登录后顺带把微信身份落到人员档案（失败不影响登录）</summary>
    private async Task AfterWxIdentityAsync(ST_Person p, string? code, string? scene)
    {
        if (string.IsNullOrEmpty(code)) return;
        try
        {
            var t = await GetWxIdentityAsync(code, scene);
            if (t == null) return;
            if (scene == "mp") p.UnionID = t.UnionId; else p.OpenID = t.OpenId;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "登录后写入微信身份失败 PersonId={PersonId}", p.Person_ID);
        }
    }

    private async Task<WxIdentity?> GetWxIdentityAsync(string? code, string? scene)
    {
        if (string.IsNullOrEmpty(code)) return null;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            if (scene == "mp")
            {
                var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={AppConfig.WxMpAppId}&secret={AppConfig.WxMpAppSecret}&js_code={code}&grant_type=authorization_code";
                var rlt = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(rlt);
                if (!doc.RootElement.TryGetProperty("openid", out var o)) return null;
                var openid = o.GetString();
                var unionid = doc.RootElement.TryGetProperty("unionid", out var u) ? u.GetString() : null;
                // 小程序拿不到 unionid 时用 openid 兜底（与 Net 版一致）
                return new WxIdentity { OpenId = openid, UnionId = string.IsNullOrEmpty(unionid) ? openid : unionid };
            }
            else
            {
                var url = $"https://api.weixin.qq.com/sns/oauth2/access_token?appid={AppConfig.WxAppId}&secret={AppConfig.WxAppSecret}&code={code}&grant_type=authorization_code";
                var rlt = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(rlt);
                if (!doc.RootElement.TryGetProperty("openid", out var o)) return null;
                // H5 只按 OpenID 识别人员；不再额外请求 cgi-bin/token + user/info 取 UnionID
                //（那会多 2 次往返，既慢又容易触发频率限制）
                return new WxIdentity { OpenId = o.GetString(), UnionId = null };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取微信身份失败 scene={Scene}", scene);
            return null;
        }
    }

    private async Task<string> GetDeptNameAsync(int? deptId)
    {
        if (!deptId.HasValue) return "";
        var d = await _db.ST_Departments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Dept_ID == deptId.Value && !(x.Is_Del ?? false));
        return d?.Dept_Name ?? "";
    }

    private async Task<object> TokenRespAsync(ST_Person p, decimal balance)
    {
        var expMin = AppConfig.JwtExpireMinutes <= 0 ? 60 : AppConfig.JwtExpireMinutes;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AppConfig.JwtSecret));
        var token = new JwtSecurityToken(
            issuer: AppConfig.JwtIssuer,
            audience: AppConfig.JwtAudience,
            claims: new[]
            {
                new Claim("PersonId", p.Person_ID.ToString()),
                new Claim(ClaimTypes.Name, p.Person_No ?? ""),
                new Claim("PersonName", p.Person_Name ?? ""),
                new Claim(ClaimTypes.Role, "Person")
            },
            expires: DateTime.UtcNow.AddMinutes(expMin),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature));

        var s = new JwtSecurityTokenHandler().WriteToken(token);
        var department = await GetDeptNameAsync(p.Dept_ID);

        return new
        {
            success = true,
            access_token = s,
            expires_in = expMin * 60,
            user = new
            {
                personId = p.Person_ID,
                personNo = p.Person_No,
                name = p.Person_Name,
                cardNo = p.Card_No,
                mobile = p.mobile,
                balance,
                department,
                title = p.Title ?? ""
            }
        };
    }
}

public class WxIdentity
{
    public string? OpenId { get; set; }
    public string? UnionId { get; set; }
}

public class MLogin
{
    public string? PersonNo { get; set; }
    public string? Password { get; set; }
    public string? Code { get; set; }
    public string? Scene { get; set; }
}

public class MSmsLogin
{
    public string? Mobile { get; set; }
    public string? Code { get; set; }
    public string? Code2 { get; set; }
    public string? Scene { get; set; }
}

public class MWxLogin
{
    public string? Code { get; set; }
    public string? Scene { get; set; }
}

public class MBind
{
    public string? PersonNo { get; set; }
    public string? Password { get; set; }
    public string? Code { get; set; }
    public string? Scene { get; set; }
    public string? Mobile { get; set; }
    public string? SmsCode { get; set; }
    public string? OpenId { get; set; }
    public string? UnionId { get; set; }
}

public class MUnbind
{
    public string? Scene { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.Text;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 充值（对应 Net 版 RechargeController，附带真实微信支付）。
/// 流程：创建订单 → 前端调起微信支付 → 微信异步通知入账 → 订单查询兜底。
/// 支付接口版本由 appsettings 的 WeChat:ApiVersion 控制（V2 / V3）。
/// 每笔均写 WX_RechargeLog，收发报文写 WX_ApiLog。
/// </summary>
[ApiController]
[Route("api/m/recharge")]
public class RechargeController : ControllerBase
{
    private const decimal SingleLimit = 5000m;
    private const decimal DailyLimit = 20000m;

    private readonly AppDbContext _db;
    private readonly ILogger<RechargeController> _logger;

    public RechargeController(AppDbContext db, ILogger<RechargeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    private string? ClientIp => HttpContext?.Connection?.RemoteIpAddress?.ToString();

    /// <summary>创建充值订单：scene=h5(公众号JSAPI) / mp(小程序JSAPI) / native(扫码)</summary>
    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] RechargeCreateReq req)
    {
        if (req == null || req.amount <= 0 || req.amount > SingleLimit)
            return Ok(new { success = false, msg = "金额不合法(1-5000元)" });

        string scene = (req.scene ?? "h5").Trim().ToLowerInvariant();
        if (scene != "h5" && scene != "mp" && scene != "native") scene = "h5";

        int fen = (int)Math.Round(req.amount * 100);
        if (fen <= 0) return Ok(new { success = false, msg = "金额不合法" });

        // 单日限额按「已入账的微信充值」统计，避免前端写死今日已充值金额导致误拦
        decimal today = PayOrderService.GetTodayRecharged(Pid);
        if (today + req.amount > DailyLimit)
            return Ok(new { success = false, msg = string.Format("超出单日充值限额（今日已充值 {0:0.00} 元，单日上限 {1:0.00} 元）", today, DailyLimit) });

        // 商户单号：14 位时间 + 人员 + 6 位随机，长度不超过微信 32 位限制
        string outTradeNo = DateTime.Now.ToString("yyyyMMddHHmmss") + Pid + Guid.NewGuid().ToString("N").Substring(0, 6);
        PayOrderService.UpdateLog(Pid, outTradeNo, null, req.amount, "CREATED", null, null, null, null,
            WeChatPayService.ApiVersionName, scene == "native" ? "NATIVE" : (scene == "mp" ? "MINI" : "JSAPI"));

        try
        {
            if (scene == "native")
            {
                var codeUrl = await WeChatPayService.CreateNativeOrderAsync(fen, "帐户充值", outTradeNo, ClientIp);
                return Ok(new { success = true, outTradeNo, codeUrl });
            }

            // 小程序与公众号的 openid 不同：小程序必须用当前小程序 wx.login 的 code 换取 openid，
            // 否则用 ST_Person.OpenID（公众号 openid）下单会报 openid 与 appid 不匹配。
            string? openId = null;
            if (scene == "mp" && !string.IsNullOrEmpty(req.code))
            {
                openId = await WeChatPayService.GetMiniProgramOpenIdAsync(req.code);
                if (string.IsNullOrEmpty(openId))
                {
                    PayOrderService.UpdateLog(Pid, outTradeNo, null, req.amount, "FAILED", "NO_MP_OPENID", "小程序openid获取失败");
                    return Ok(new { success = false, msg = "获取微信小程序OpenID失败，请退出后重新进入再试" });
                }
            }
            if (string.IsNullOrEmpty(openId)) openId = req.openId;
            if (string.IsNullOrEmpty(openId))
            {
                var p = await _db.ST_People.AsNoTracking().FirstOrDefaultAsync(x => x.Person_ID == Pid);
                openId = p?.OpenID;
            }
            if (string.IsNullOrEmpty(openId))
            {
                PayOrderService.UpdateLog(Pid, outTradeNo, null, req.amount, "FAILED", "NO_OPENID", "未获取到微信OpenID");
                return Ok(new { success = false, msg = "未获取到微信OpenID，请先绑定微信" });
            }

            var r = await WeChatPayService.CreateJsapiOrderAsync(openId!, fen, "帐户充值", outTradeNo, scene == "mp", ClientIp);
            return Ok(new { success = true, outTradeNo, prepayId = r.PrepayId, payParams = r.Params });
        }
        catch (Exception ex)
        {
            string em = ex.Message ?? "";
            if (em.Length > 450) em = em.Substring(0, 450);
            PayOrderService.UpdateLog(Pid, outTradeNo, null, req.amount, "FAILED", "CREATE_ERR", em);
            return Ok(new { success = false, msg = ex.Message });
        }
    }

    /// <summary>V3 支付异步通知 → 入账</summary>
    [AllowAnonymous]
    [HttpPost("notify/v3")]
    public async Task<IActionResult> NotifyV3()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        try
        {
            var txn = WeChatPayService.ParseNotifyV3(body, out var eventType);
            if (txn != null && txn.TradeState == "SUCCESS" && txn.Amount != null)
            {
                var log = PayOrderService.GetLog(txn.OutTradeNumber);
                int pid = log?.PersonId ?? 0;
                PayOrderService.MarkPaid(pid, txn.OutTradeNumber, txn.TransactionId, txn.Amount.Total / 100m, "微信支付充值");
            }
            DbApiLogger.Write("/api/m/recharge/notify/v3", "POST", body, "code=SUCCESS", null, 0, true, ClientIp);
            return Ok(new { code = "SUCCESS", message = "成功" });
        }
        catch (Exception ex)
        {
            DbApiLogger.Write("/api/m/recharge/notify/v3", "POST", body, ex.Message, null, 0, false, ClientIp);
            // 返回 500 让微信重试，避免通知处理失败后永久丢单
            return StatusCode(500, new { code = "FAIL", message = "处理失败" });
        }
    }

    /// <summary>V2 支付异步通知（XML）→ 验签 → 入账</summary>
    [AllowAnonymous]
    [HttpPost("notify/v2")]
    public async Task<IActionResult> NotifyV2()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        try
        {
            var ev = WeChatPayService.ParseNotifyV2(body);
            if (ev == null)
            {
                // 验签失败：返回 FAIL（HTTP 200），微信不会重复通知伪造报文
                return XmlResult("FAIL", "SIGN_INVALID");
            }
            if (ev.ReturnCode == "SUCCESS" && ev.ResultCode == "SUCCESS" && ev.TradeState == "SUCCESS")
            {
                var log = PayOrderService.GetLog(ev.OutTradeNumber);
                int pid = log?.PersonId ?? 0;
                PayOrderService.MarkPaid(pid, ev.OutTradeNumber, ev.TransactionId, ev.TotalFee / 100m, "微信支付充值");
            }
            DbApiLogger.Write("/api/m/recharge/notify/v2", "POST", body, "SUCCESS", null, 0, true, ClientIp);
            return XmlResult("SUCCESS", "OK");
        }
        catch (Exception ex)
        {
            DbApiLogger.Write("/api/m/recharge/notify/v2", "POST", body, ex.Message, null, 0, false, ClientIp);
            return XmlResult("FAIL", "ERROR");
        }
    }

    /// <summary>订单查询兜底（前端支付后轮询；查到 SUCCESS 即入账）</summary>
    [Authorize]
    [HttpGet("query")]
    public async Task<IActionResult> Query(string outTradeNo, string scene = "h5")
    {
        if (string.IsNullOrWhiteSpace(outTradeNo))
            return Ok(new { success = false, msg = "订单号不能为空", tradeState = "" });

        var log = PayOrderService.GetLog(outTradeNo);
        if (log == null)
            return Ok(new { success = false, msg = "订单不存在", tradeState = "" });
        // 只能查询自己的订单
        if (log.PersonId.HasValue && log.PersonId.Value != Pid)
            return Ok(new { success = false, msg = "无权查询该订单", tradeState = "" });

        // V2 查询需要下单时的 appid：小程序与公众号不同，前端把 scene 带上来
        bool mini = string.Equals(scene, "mp", StringComparison.OrdinalIgnoreCase);
        var st = await WeChatPayService.QueryTradeStateAsync(outTradeNo, mini);
        if (st == "SUCCESS" && log.Status != "SUCCESS")
        {
            PayOrderService.MarkPaid(log.PersonId ?? 0, outTradeNo, null, log.Fund ?? 0, "微信支付充值(查询入账)");
        }
        return Ok(new { success = true, tradeState = st });
    }

    /// <summary>V2 回调必须返回微信要求的 XML 报文</summary>
    private ContentResult XmlResult(string code, string message)
    {
        string xml = "<xml><return_code><![CDATA[" + code + "]]></return_code><return_msg><![CDATA[" + message + "]]></return_msg></xml>";
        return Content(xml, "text/xml", Encoding.UTF8);
    }
}

public class RechargeCreateReq
{
    public decimal amount { get; set; }
    public string? scene { get; set; }
    public string? openId { get; set; }
    /// <summary>小程序 wx.login 的 code（scene=mp 时用于实时换取小程序 openid）</summary>
    public string? code { get; set; }
}

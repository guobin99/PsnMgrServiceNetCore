using SKIT.FlurlHttpClient.Wechat.TenpayV2;   // V2 扩展方法（ExecuteCreatePayUnifiedOrderAsync 等）所在命名空间
using SKIT.FlurlHttpClient.Wechat.TenpayV3;   // V3 扩展方法（ExecuteCreatePayTransactionJsapiAsync 等）所在命名空间
using SKIT2 = SKIT.FlurlHttpClient.Wechat.TenpayV2;
using SKIT2Events = SKIT.FlurlHttpClient.Wechat.TenpayV2.Events;
using SKIT2Models = SKIT.FlurlHttpClient.Wechat.TenpayV2.Models;
using SKIT3Models = SKIT.FlurlHttpClient.Wechat.TenpayV3.Models;
using SKIT3Events = SKIT.FlurlHttpClient.Wechat.TenpayV3.Events;
using System.Text.Json;

namespace PsnMgrService.Services;

/// <summary>
/// 微信支付服务（对应 Net 版 WeChatPayService）。
/// 由 appsettings 的 WeChat:ApiVersion 决定使用哪套接口：
///   V3（默认）：SKIT.FlurlHttpClient.Wechat.TenpayV3 —— JSAPI/小程序下单、V3 回调解密、订单查询、Native
///   V2       ：SKIT.FlurlHttpClient.Wechat.TenpayV2 —— JSAPI/小程序统一下单、V2 回调验签、订单查询、Native
/// 两套实现的入参/出参保持一致，上层 RechargeController 无需关心版本。
/// </summary>
public static class WeChatPayService
{
    public static string AppId => AppConfig.WxAppId;
    public static string MpAppId => AppConfig.WxMpAppId;
    public static string MchId => AppConfig.WxMchId;
    public static string ApiV3Key => AppConfig.WxPayApiV3Key;
    public static string ApiV2Key => AppConfig.WxPayApiV2Key;
    public static string CertSerial => AppConfig.WxPayCertSerial;
    public static string PrivateKey => AppConfig.WxPayPrivateKey;
    public static string NotifyUrl => AppConfig.WxPayNotifyUrl;
    public static string NotifyUrlV2 => AppConfig.WxPayNotifyUrlV2;

    public static bool IsV2
    {
        get
        {
            var v = (AppConfig.WxPayApiVersion ?? "").Trim();
            return string.Equals(v, "V2", StringComparison.OrdinalIgnoreCase) || v == "2";
        }
    }

    public static string ApiVersionName => IsV2 ? "V2" : "V3";

    // ---------------- 客户端（按需懒加载，选中 V2 时不会触碰 V3 证书配置） ----------------

    private static readonly object _lock = new();
    private static SKIT.FlurlHttpClient.Wechat.TenpayV3.WechatTenpayClient? _v3;

    public static SKIT.FlurlHttpClient.Wechat.TenpayV3.WechatTenpayClient V3
    {
        get
        {
            if (_v3 == null)
            {
                lock (_lock)
                {
                    _v3 ??= new SKIT.FlurlHttpClient.Wechat.TenpayV3.WechatTenpayClient(
                        new SKIT.FlurlHttpClient.Wechat.TenpayV3.WechatTenpayClientOptions
                        {
                            MerchantId = MchId,
                            MerchantV3Secret = ApiV3Key,
                            MerchantCertificatePrivateKey = PrivateKey,
                            MerchantCertificateSerialNumber = CertSerial,
                            AutoEncryptRequestSensitiveProperty = false,
                            AutoDecryptResponseSensitiveProperty = false
                        });
                }
            }
            return _v3;
        }
    }

    private static SKIT2.WechatTenpayClient? _v2;

    public static SKIT2.WechatTenpayClient V2
    {
        get
        {
            if (_v2 == null)
            {
                lock (_lock)
                {
                    _v2 ??= new SKIT2.WechatTenpayClient(new SKIT2.WechatTenpayClientOptions
                    {
                        MerchantId = MchId,
                        MerchantSecret = ApiV2Key
                    });
                }
            }
            return _v2;
        }
    }

    private static string Json(object? o)
    {
        try { return JsonSerializer.Serialize(o); } catch { return ""; }
    }

    // ---------------- JSAPI / 小程序下单 ----------------

    /// <summary>JSAPI 下单（H5 服务号 / 小程序），返回 prepay_id 与前端调起支付参数</summary>
    public static Task<JsapiOrderResult> CreateJsapiOrderAsync(string openId, int fen, string desc, string outTradeNo, bool mini, string? clientIp = null)
    {
        return IsV2
            ? CreateJsapiOrderV2Async(openId, fen, desc, outTradeNo, mini, clientIp)
            : CreateJsapiOrderV3Async(openId, fen, desc, outTradeNo, mini);
    }

    private static async Task<JsapiOrderResult> CreateJsapiOrderV3Async(string openId, int fen, string desc, string outTradeNo, bool mini)
    {
        var appId = mini ? MpAppId : AppId;
        var req = new SKIT3Models.CreatePayTransactionJsapiRequest
        {
            AppId = appId,
            MerchantId = MchId,
            OutTradeNumber = outTradeNo,
            Description = desc,
            NotifyUrl = NotifyUrl,
            Amount = new SKIT3Models.CreatePayTransactionJsapiRequest.Types.Amount
            {
                Total = fen,
                Currency = "CNY"
            },
            Payer = new SKIT3Models.CreatePayTransactionJsapiRequest.Types.Payer
            {
                OpenId = openId
            }
        };

        var resp = await V3.ExecuteCreatePayTransactionJsapiAsync(req);
        DbApiLogger.Write(mini ? "tenpayV3.MiniProgram" : "tenpayV3.JSAPI", "POST", Json(req), Json(resp), null, 0, resp.IsSuccessful());

        if (!resp.IsSuccessful())
            throw new Exception((mini ? "小程序" : "JSAPI") + "下单失败:" + (resp.ErrorMessage ?? resp.ErrorCode ?? "未知错误"));
        if (string.IsNullOrEmpty(resp.PrepayId))
            throw new Exception("微信支付未返回 prepay_id");

        return new JsapiOrderResult
        {
            PrepayId = resp.PrepayId,
            Params = (IDictionary<string, string>)V3.GenerateParametersForJsapiPayRequest(appId, resp.PrepayId)
        };
    }

    private static async Task<JsapiOrderResult> CreateJsapiOrderV2Async(string openId, int fen, string desc, string outTradeNo, bool mini, string? clientIp)
    {
        var appId = mini ? MpAppId : AppId;
        var req = new SKIT2Models.CreatePayUnifiedOrderRequest
        {
            AppId = appId,
            MerchantId = MchId,
            Body = desc,
            OutTradeNumber = outTradeNo,
            TotalFee = fen,
            TradeType = "JSAPI",
            OpenId = openId,
            ClientIp = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
            NotifyUrl = NotifyUrlV2
        };

        var resp = await V2.ExecuteCreatePayUnifiedOrderAsync(req);
        DbApiLogger.Write(mini ? "tenpayV2.MiniProgram" : "tenpayV2.JSAPI", "POST", Json(req), Json(resp), null, 0, resp.IsSuccessful());

        if (!resp.IsSuccessful())
            throw new Exception((mini ? "小程序" : "JSAPI") + "下单失败:" + (resp.ErrorMessage ?? resp.ReturnMessage ?? resp.ErrorCode ?? "未知错误"));
        if (string.IsNullOrEmpty(resp.PrepayId))
            throw new Exception("微信支付未返回 prepay_id");

        var payParams = mini
            ? V2.GenerateParametersForMiniProgramRequestPayment(appId, resp.PrepayId, "MD5")
            : V2.GenerateParametersForJsapiGetBrandPayRequest(appId, resp.PrepayId, "MD5");

        return new JsapiOrderResult
        {
            PrepayId = resp.PrepayId,
            Params = (IDictionary<string, string>)payParams
        };
    }

    // ---------------- Native 扫码下单 ----------------

    /// <summary>Native 扫码下单（按配置走 V2 / V3），返回二维码链接 code_url</summary>
    public static Task<string> CreateNativeOrderAsync(int fen, string desc, string outTradeNo, string? clientIp = null)
    {
        return IsV2
            ? CreateNativeOrderV2Async(fen, desc, outTradeNo, clientIp)
            : CreateNativeOrderV3Async(fen, desc, outTradeNo);
    }

    private static async Task<string> CreateNativeOrderV2Async(int fen, string desc, string outTradeNo, string? clientIp)
    {
        var req = new SKIT2Models.CreatePayUnifiedOrderRequest
        {
            AppId = AppId,
            MerchantId = MchId,
            Body = desc,
            OutTradeNumber = outTradeNo,
            TotalFee = fen,
            TradeType = "NATIVE",
            ProductId = outTradeNo,
            ClientIp = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
            NotifyUrl = NotifyUrlV2
        };
        var resp = await V2.ExecuteCreatePayUnifiedOrderAsync(req);
        DbApiLogger.Write("tenpayV2.Native", "POST", Json(req), Json(resp), null, 0, resp.IsSuccessful());
        if (!resp.IsSuccessful()) throw new Exception("Native下单失败:" + (resp.ErrorMessage ?? resp.ReturnMessage ?? resp.ErrorCode ?? "未知错误"));
        if (string.IsNullOrEmpty(resp.CodeUrl)) throw new Exception("微信支付未返回 code_url");
        return resp.CodeUrl;
    }

    private static async Task<string> CreateNativeOrderV3Async(int fen, string desc, string outTradeNo)
    {
        var req = new SKIT3Models.CreatePayTransactionNativeRequest
        {
            AppId = AppId,
            MerchantId = MchId,
            OutTradeNumber = outTradeNo,
            Description = desc,
            NotifyUrl = NotifyUrl,
            Amount = new SKIT3Models.CreatePayTransactionNativeRequest.Types.Amount
            {
                Total = fen,
                Currency = "CNY"
            }
        };
        var resp = await V3.ExecuteCreatePayTransactionNativeAsync(req);
        DbApiLogger.Write("tenpayV3.Native", "POST", Json(req), Json(resp), null, 0, resp.IsSuccessful());
        if (!resp.IsSuccessful()) throw new Exception("Native下单失败:" + (resp.ErrorMessage ?? resp.ErrorCode ?? "未知错误"));
        if (string.IsNullOrEmpty(resp.QrcodeUrl)) throw new Exception("微信支付未返回 code_url");
        return resp.QrcodeUrl;
    }

    // ---------------- 订单查询（前端支付后轮询兜底） ----------------

    public static Task<string> QueryTradeStateAsync(string outTradeNo, bool mini = false)
    {
        return IsV2 ? QueryTradeStateV2Async(outTradeNo, mini) : QueryTradeStateV3Async(outTradeNo);
    }

    private static async Task<string> QueryTradeStateV3Async(string outTradeNo)
    {
        var resp = await V3.ExecuteGetPayTransactionByOutTradeNumberAsync(
            new SKIT3Models.GetPayTransactionByOutTradeNumberRequest { OutTradeNumber = outTradeNo });
        DbApiLogger.Write("tenpayV3.Query", "GET", outTradeNo, Json(resp), null, 0, resp.IsSuccessful());
        return resp.IsSuccessful() ? (resp.TradeState ?? "") : "";
    }

    private static async Task<string> QueryTradeStateV2Async(string outTradeNo, bool mini)
    {
        // V2 订单查询必须带下单时使用的 appid：公众号与小程序 appid 不同
        var resp = await V2.ExecuteGetPayOrderAsync(new SKIT2Models.GetPayOrderRequest
        {
            AppId = mini ? MpAppId : AppId,
            MerchantId = MchId,
            OutTradeNumber = outTradeNo
        });
        DbApiLogger.Write("tenpayV2.Query", "GET", outTradeNo, Json(resp), null, 0, resp.IsSuccessful());
        return resp.IsSuccessful() ? (resp.TradeState ?? "") : "";
    }

    // ---------------- 支付结果通知 ----------------

    /// <summary>解析 V2 支付回调（XML）：先验签，再反序列化；验签失败返回 null</summary>
    public static SKIT2Events.OrderEvent? ParseNotifyV2(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var verified = V2.VerifyEventSignature(body);
        if (!verified.Result)
        {
            DbApiLogger.Write("tenpayV2.Notify", "POST", null,
                verified.Error == null ? "SIGN_INVALID" : verified.Error.Message, null, 0, false);
            return null;
        }
        return V2.DeserializeEvent<SKIT2Events.OrderEvent>(body);
    }

    /// <summary>解析 V3 支付回调（AES-256-GCM 解密 resource），非支付成功事件返回 null</summary>
    public static SKIT3Events.TransactionResource? ParseNotifyV3(string body, out string eventType)
    {
        eventType = "";
        var ev = V3.DeserializeEvent(body);
        eventType = ev.EventType;
        if (ev.EventType != "TRANSACTION.SUCCESS") return null;

        var txn = V3.DecryptEventResource<SKIT3Events.TransactionResource>(ev);
        DbApiLogger.Write("tenpayV3.Notify", "POST", body, Json(txn), null, 0, true);
        return txn;
    }

    // ---------------- 小程序 openid ----------------

    /// <summary>
    /// 用小程序 wx.login 的 code 换取 openid。
    /// 小程序与公众号的 openid 不同，充值下单必须使用「当前小程序」的 openid，
    /// 否则微信会报 openid 与 appid 不匹配；这里每次充值用前端新取的 code 实时换取，避免额外存库。
    /// </summary>
    public static async Task<string?> GetMiniProgramOpenIdAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var url = string.Format(
                "https://api.weixin.qq.com/sns/jscode2session?appid={0}&secret={1}&js_code={2}&grant_type=authorization_code",
                MpAppId, AppConfig.WxMpAppSecret, code);
            var rlt = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(rlt);
            if (!doc.RootElement.TryGetProperty("openid", out var openidEl))
            {
                var ec = doc.RootElement.TryGetProperty("errcode", out var e1) ? e1.ToString() : "";
                var em = doc.RootElement.TryGetProperty("errmsg", out var e2) ? e2.ToString() : "";
                DbApiLogger.Write("tenpay.jscode2session", "GET", null, "errcode=" + ec + " errmsg=" + em, null, 0, false);
                return null;
            }
            return openidEl.GetString();
        }
        catch (Exception ex)
        {
            DbApiLogger.Write("tenpay.jscode2session", "GET", null, ex.Message, null, 0, false);
            return null;
        }
    }
}

public class JsapiOrderResult
{
    public string PrepayId { get; set; } = "";
    public IDictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
}

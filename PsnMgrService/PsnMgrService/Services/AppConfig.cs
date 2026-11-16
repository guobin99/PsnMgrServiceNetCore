namespace PsnMgrService.Services;

/// <summary>
/// 全局配置（对应 .NET Framework 版的 Web.config appSettings / connectionStrings）。
/// 在 Program.cs 启动时调用 Init()，供静态服务类（短信、消息推送等）读取。
/// </summary>
public static class AppConfig
{
    /// <summary>数据库连接串（对应 Net 版 PsnMgrEntities 的 ProviderConnectionString）</summary>
    public static string ConnectionString { get; private set; } = "";

    /// <summary>阿里云短信配置</summary>
    public static string AliAccessKeyId { get; private set; } = "";
    public static string AliAccessKeySecret { get; private set; } = "";
    public static string AliSmsSignName { get; private set; } = "";
    public static string AliSmsTemplateCode { get; private set; } = "";

    /// <summary>JWT（移动端 token 用，与管理员 token 共用同一套签名参数）</summary>
    public static string JwtSecret { get; private set; } = "";
    public static string JwtIssuer { get; private set; } = "";
    public static string JwtAudience { get; private set; } = "";
    public static int JwtExpireMinutes { get; private set; } = 60;

    /// <summary>微信应用与支付配置</summary>
    public static string WxAppId { get; private set; } = "";
    public static string WxAppSecret { get; private set; } = "";
    public static string WxMpAppId { get; private set; } = "";
    public static string WxMpAppSecret { get; private set; } = "";

    /// <summary>微信支付商户号</summary>
    public static string WxMchId { get; private set; } = "";
    /// <summary>APIv3 密钥（32 位，用于 V3 回调 resource 的 AES-256-GCM 解密）</summary>
    public static string WxPayApiV3Key { get; private set; } = "";
    /// <summary>APIv2 密钥（32 位，用于 V2 MD5 签名）</summary>
    public static string WxPayApiV2Key { get; private set; } = "";
    /// <summary>商户 API 证书序列号（V3 请求签名用）</summary>
    public static string WxPayCertSerial { get; private set; } = "";
    /// <summary>商户 API 私钥内容（apiclient_key.pem 正文，非路径）</summary>
    public static string WxPayPrivateKey { get; private set; } = "";
    /// <summary>V3 支付结果通知地址</summary>
    public static string WxPayNotifyUrl { get; private set; } = "";
    /// <summary>V2 支付结果通知地址；未单独配置时由 V3 地址自动推导（notify/v3 → notify/v2）</summary>
    public static string WxPayNotifyUrlV2 { get; private set; } = "";
    /// <summary>付款码/通行码是否使用一卡通原生二维码（=1 时用 JOCat 生成 CRC 码，否则用 employee;工号;时间戳）</summary>
    public static bool YktPaymentCode { get; private set; }

    /// <summary>支付接口版本：V2 / V3（缺省 V3）</summary>
    public static string WxPayApiVersion { get; private set; } = "V3";

    /// <summary>访客二维码凭证签名密钥（对应 Net 版 VisitorQrSecret，缺省回退 JWT SecretKey）</summary>
    public static string VisitorQrSecret { get; private set; } = "";

    /// <summary>原始配置（供模板ID 等按 key 动态读取）</summary>
    public static IConfiguration? Configuration { get; private set; }

    public static void Init(IConfiguration configuration)
    {
        Configuration = configuration;
        ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

        var ali = configuration.GetSection("AliyunSms");
        AliAccessKeyId = (ali["AccessKeyId"] ?? "").Trim();
        AliAccessKeySecret = (ali["AccessKeySecret"] ?? "").Trim();
        AliSmsSignName = (ali["SignName"] ?? "").Trim();
        AliSmsTemplateCode = (ali["TemplateCode"] ?? "").Trim();

        var jwt = configuration.GetSection("JwtSettings");
        JwtSecret = (jwt["SecretKey"] ?? "").Trim();
        JwtIssuer = (jwt["Issuer"] ?? "").Trim();
        JwtAudience = (jwt["Audience"] ?? "").Trim();
        if (int.TryParse(jwt["ExpiryMinutes"], out var exp) && exp > 0) JwtExpireMinutes = exp;

        var wx = configuration.GetSection("WeChat");
        WxAppId = (wx["AppId"] ?? "").Trim();
        WxAppSecret = (wx["AppSecret"] ?? "").Trim();
        WxMpAppId = (wx["MpAppId"] ?? "").Trim();
        WxMpAppSecret = (wx["MpAppSecret"] ?? "").Trim();

        WxMchId = (wx["MchId"] ?? "").Trim();
        WxPayApiV3Key = (wx["ApiV3Key"] ?? "").Trim();
        WxPayApiV2Key = (wx["ApiV2Key"] ?? "").Trim();
        WxPayCertSerial = (wx["SerialNo"] ?? "").Trim();
        WxPayApiVersion = string.IsNullOrWhiteSpace(wx["ApiVersion"]) ? "V3" : wx["ApiVersion"]!.Trim();

        // 私钥：支持「直接写 PEM 内容」或「写相对路径（相对站点根目录）」
        var keyRaw = (wx["PrivateKeyPath"] ?? "").Trim();
        if (keyRaw.Length == 0) keyRaw = (wx["PrivateKey"] ?? "").Trim();
        if (keyRaw.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
        {
            WxPayPrivateKey = keyRaw;
        }
        else if (keyRaw.Length > 0)
        {
            try
            {
                var full = Path.IsPathRooted(keyRaw)
                    ? keyRaw
                    : Path.Combine(AppContext.BaseDirectory, keyRaw);
                if (File.Exists(full)) WxPayPrivateKey = File.ReadAllText(full).Trim();
            }
            catch { /* 读不到就留空，下单时会明确报错 */ }
        }

        YktPaymentCode = (configuration["YKT_PaymentCode"] ?? configuration["WeChat:YKT_PaymentCode"] ?? "").Trim() == "1";

        WxPayNotifyUrl = (wx["NotifyUrl"] ?? configuration["WxPayNotifyUrl"] ?? "").Trim();
        WxPayNotifyUrlV2 = (wx["NotifyUrlV2"] ?? configuration["WxPayNotifyUrlV2"] ?? "").Trim();
        if (WxPayNotifyUrlV2.Length == 0 && WxPayNotifyUrl.Length > 0)
            WxPayNotifyUrlV2 = WxPayNotifyUrl.Replace("/notify/v3", "/notify/v2");

        VisitorQrSecret = (configuration["VisitorQrSecret"] ?? "").Trim();
        if (VisitorQrSecret.Length == 0) VisitorQrSecret = (configuration["JwtSettings:SecretKey"] ?? "").Trim();
    }
}

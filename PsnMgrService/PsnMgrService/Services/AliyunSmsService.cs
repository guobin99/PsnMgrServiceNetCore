using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PsnMgrService.Services;

/// <summary>
/// 阿里云短信服务（从 PsnMgrService(Net) 的 AliyunSmsService 移植）。
/// Pop RPC 原生签名实现，无需 SDK。
///
/// 签名铁律：参数必须按 **Ordinal（字节序）** 升序拼接。
/// SortedDictionary 默认用 CurrentCulture，zh-CN 下会忽略大小写把 SignatureMethod 排到 SignName 前，
/// 导致 SignatureDoesNotMatch（历史上排查很久的坑），所以这里显式指定 StringComparer.Ordinal。
/// </summary>
public static class AliyunSmsService
{
    public class SmsSendResult
    {
        public bool Ok { get; set; }
        /// <summary>阿里云业务码：OK / isv.BUSINESS_LIMIT_CONTROL / SignatureDoesNotMatch 等</summary>
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static string NewCode()
    {
        var r = new Random(Guid.NewGuid().GetHashCode());
        return r.Next(100000, 999999).ToString();
    }

    public static async Task<SmsSendResult> SendAsync(string mobile, string code)
    {
        var ps = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            { "AccessKeyId", AppConfig.AliAccessKeyId },
            { "Action", "SendSms" },
            { "Format", "JSON" },
            { "PhoneNumbers", mobile ?? "" },
            { "RegionId", "cn-hangzhou" },
            { "SignName", AppConfig.AliSmsSignName },
            { "SignatureMethod", "HMAC-SHA1" },
            { "SignatureNonce", Guid.NewGuid().ToString() },
            { "SignatureVersion", "1.0" },
            { "TemplateCode", AppConfig.AliSmsTemplateCode },
            { "TemplateParam", "{\"code\":\"" + code + "\"}" },
            { "Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
            { "Version", "2017-05-25" }
        };

        var sb = new StringBuilder();
        foreach (var kv in ps)
            sb.Append('&').Append(Percent(kv.Key)).Append('=').Append(Percent(kv.Value));

        var strToSign = "GET&%2F&" + Percent(sb.ToString().Substring(1));
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes((AppConfig.AliAccessKeySecret ?? "") + "&"));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(strToSign)));
        var url = "https://dysmsapi.aliyuncs.com/?Signature=" + Percent(sig) + sb;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        string body;
        try
        {
            // 400 也带业务错误报文（isv.* / SignatureDoesNotMatch），必须读出来再判断，
            // 不能用 GetStringAsync（非 200 直接抛异常并丢掉响应体）
            var resp = await http.GetAsync(url);
            body = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return new SmsSendResult { Ok = false, Code = "NETWORK_ERROR", Message = "短信服务网络异常：" + ex.Message };
        }

        string? bizCode = null, bizMsg = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("Code", out var c)) bizCode = c.GetString();
            if (doc.RootElement.TryGetProperty("Message", out var m)) bizMsg = m.GetString();
        }
        catch { /* 非 JSON（网关错误页），保留原文 */ }

        var ok = string.Equals(bizCode, "OK", StringComparison.OrdinalIgnoreCase);
        return new SmsSendResult
        {
            Ok = ok,
            Code = bizCode ?? "UNKNOWN",
            Message = string.IsNullOrEmpty(bizMsg) ? body : bizMsg
        };
    }

    /// <summary>验证码入库（WX_SmsCode），minutes 为有效分钟数</summary>
    public static void SaveCode(string mobile, string code, string scene, int minutes)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        using var cmd = new SqlCommand(
            "INSERT INTO WX_SmsCode(Mobile,Code,Scene,Expire_Time) VALUES(@m,@c,@s,DATEADD(mi,@mi,GETDATE()))", cn);
        cmd.Parameters.AddWithValue("@m", mobile);
        cmd.Parameters.AddWithValue("@c", code);
        cmd.Parameters.AddWithValue("@s", scene);
        cmd.Parameters.AddWithValue("@mi", minutes);
        cn.Open();
        cmd.ExecuteNonQuery();
    }

    /// <summary>校验并消费验证码（一次性）</summary>
    public static bool Verify(string mobile, string code, string scene)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        using var cmd = new SqlCommand(
            @"UPDATE TOP(1) WX_SmsCode SET Used=1
              WHERE Mobile=@m AND Code=@c AND Scene=@s AND Used=0 AND Expire_Time>GETDATE();
              SELECT @@ROWCOUNT", cn);
        cmd.Parameters.AddWithValue("@m", mobile ?? "");
        cmd.Parameters.AddWithValue("@c", code ?? "");
        cmd.Parameters.AddWithValue("@s", scene ?? "");
        cn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static string Percent(string? s)
    {
        var ub = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(s ?? ""))
        {
            if ("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~".IndexOf((char)b) >= 0)
                ub.Append((char)b);
            else
                ub.Append('%').Append(b.ToString("X2"));
        }
        return ub.ToString();
    }
}

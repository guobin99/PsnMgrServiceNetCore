using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace PsnMgrService.Services;

/// <summary>
/// 微信推送双通道（对应 Net 版 WeChatTemplateService）：
/// ① 服务号模板消息（公众号 H5 用户，凭 ST_Person.OpenID + 模板ID，cgi-bin/message/template/send）
/// ② 小程序订阅消息（凭 ST_Person.UnionID + 订阅模板ID，cgi-bin/message/subscribe/send）
/// 模板ID 配在 appsettings 的 `WeChatTemplate:{type}`（或根节点 `WxTpl_{type}` / `WxSub_{type}`）；
/// 未配置或用户未订阅时静默跳过，推送失败不影响业务。
/// </summary>
public static class WeChatTemplateService
{
    private static string GzhAppId => AppConfig.WxAppId;
    private static string GzhSecret => AppConfig.WxAppSecret;

    /// <summary>站内消息落库后异步推送（不阻塞请求线程）</summary>
    public static void PushTemplate(int personId, string type, string title, string content)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var (openId, unionId) = GetWxIdentity(personId);
                if (string.IsNullOrEmpty(openId) && string.IsNullOrEmpty(unionId)) return;

                // ① 服务号模板消息
                if (!string.IsNullOrEmpty(openId))
                    await SendGzhTemplateAsync(openId!, type, title, content);

                // ② 小程序订阅消息（Net 版用 UnionID 作小程序 openid）
                if (!string.IsNullOrEmpty(unionId))
                    await SendMpSubscribeAsync(unionId!, type, title, content);
            }
            catch { /* 推送失败不影响业务 */ }
        });
    }

    private static (string? openId, string? unionId) GetWxIdentity(int personId)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand("SELECT TOP 1 OpenID, UnionID FROM ST_Person WHERE Person_ID = @p0", cn);
            cmd.Parameters.AddWithValue("@p0", personId);
            cn.Open();
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return (null, null);
            string? openId = rd["OpenID"] == DBNull.Value ? null : Convert.ToString(rd["OpenID"]);
            string? unionId = rd["UnionID"] == DBNull.Value ? null : Convert.ToString(rd["UnionID"]);
            return (openId, unionId);
        }
        catch { return (null, null); }
    }

    /// <summary>模板ID 读取：优先 WeChatTemplate:{key}，其次根节点同名键</summary>
    private static string TemplateId(string key)
    {
        var cfg = AppConfig.Configuration;
        if (cfg == null) return "";
        string v = cfg["WeChatTemplate:" + key] ?? cfg[key] ?? "";
        return v.Trim();
    }

    private static async Task SendGzhTemplateAsync(string openId, string type, string title, string content)
    {
        string tplId = TemplateId("WxTpl_" + type);
        if (string.IsNullOrEmpty(tplId)) return;
        var token = await GetAccessTokenAsync(GzhAppId, GzhSecret);
        if (token == null) return;

        // 通用关键字结构（实际字段名需与公众平台模板一致）
        var json = "{"
            + "\"touser\":\"" + openId + "\","
            + "\"template_id\":\"" + tplId + "\","
            + "\"url\":\"\","
            + "\"data\":{"
            + "\"first\":{\"value\":\"" + Escape(title) + "\",\"color\":\"#173177\"},"
            + "\"keyword1\":{\"value\":\"" + Escape(title) + "\"},"
            + "\"keyword2\":{\"value\":\"" + Escape(content) + "\"},"
            + "\"keyword3\":{\"value\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\"},"
            + "\"remark\":{\"value\":\"详情可在应用内查看\",\"color\":\"#173177\"}"
            + "}}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var resp = await http.PostAsync(
            "https://api.weixin.qq.com/cgi-bin/message/template/send?access_token=" + token,
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        DbApiLogger.Write("wechat.template.send", "POST", json, body, null, 0, body.Contains("\"errcode\":0"));
    }

    private static async Task SendMpSubscribeAsync(string mpOpenId, string type, string title, string content)
    {
        string tplId = TemplateId("WxSub_" + type);
        if (string.IsNullOrEmpty(tplId)) return;
        var token = await GetAccessTokenAsync(AppConfig.WxMpAppId, AppConfig.WxMpAppSecret);
        if (token == null) return;

        // 订阅消息字段（thing 类字段限 20 字符）
        var t = title.Length > 20 ? title.Substring(0, 20) : title;
        var c = content.Length > 20 ? content.Substring(0, 20) : content;
        var json = "{"
            + "\"touser\":\"" + mpOpenId + "\","
            + "\"template_id\":\"" + tplId + "\","
            + "\"page\":\"pages/home/home\","
            + "\"data\":{\"thing1\":{\"value\":\"" + Escape(t) + "\"},"
            + "\"thing2\":{\"value\":\"" + Escape(c) + "\"},"
            + "\"time3\":{\"value\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\"}}"
            + "}";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var resp = await http.PostAsync(
            "https://api.weixin.qq.com/cgi-bin/message/subscribe/send?access_token=" + token,
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        DbApiLogger.Write("wechat.subscribe.send", "POST", json, body, null, 0, body.Contains("\"errcode\":0"));
    }

    private static async Task<string?> GetAccessTokenAsync(string appId, string secret)
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(secret)) return null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var json = await http.GetStringAsync(string.Format(
            "https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={0}&secret={1}", appId, secret));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var el) ? el.GetString() : null;
    }

    private static string Escape(string? s)
        => (s ?? "").Replace("\\", "\\\\").Replace("\"", "'");
}

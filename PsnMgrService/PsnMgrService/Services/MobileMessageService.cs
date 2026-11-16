using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 站内消息（WX_Message）写入，从 PsnMgrService(Net) 的 MobileMessageService 移植。
/// 纯本地 DB INSERT，不做外部调用。
/// </summary>
public static class MobileMessageService
{
    public static void Push(int personId, string type, string title, string content)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(
                "INSERT INTO WX_Message(Person_ID, Msg_Type, Title, Content) VALUES (@p,@t,@ti,@c)", cn);
            cmd.Parameters.AddWithValue("@p", personId);
            cmd.Parameters.AddWithValue("@t", (object?)type ?? "system");
            cmd.Parameters.AddWithValue("@ti", (object?)title ?? "");
            cmd.Parameters.AddWithValue("@c", (object?)content ?? "");
            cn.Open();
            cmd.ExecuteNonQuery();

            // 微信推送：服务号模板消息 + 小程序订阅消息（未配置模板ID或未订阅时静默跳过）
            WeChatTemplateService.PushTemplate(personId, type ?? "system", title ?? "", content ?? "");
        }
        catch
        {
            // 消息推送失败不影响主流程（与 Net 版一致）
        }
    }
}

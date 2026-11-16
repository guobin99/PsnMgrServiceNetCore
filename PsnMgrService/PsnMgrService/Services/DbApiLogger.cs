using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 数据库收发日志（对应 Net 版 DbApiLogger）。
/// 写 WX_ApiLog：充值/支付全流程的请求报文、响应报文、耗时、成败。
/// 日志失败一律吞掉，不阻断业务。
/// </summary>
public static class DbApiLogger
{
    public static void Write(string url, string? method, string? req, string? resp,
        int? personId, int elapsed, bool success, string? ip = null)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(
                @"INSERT INTO WX_ApiLog(Person_ID,Api_Url,Http_Method,Request_Body,Response_Body,IP,Elapsed,Success,Create_Time)
                  VALUES(@p,@u,@m,@rq,@rs,@ip,@e,@s,GETDATE())", cn);

            var u = url ?? "";
            if (u.Length > 200) u = u.Substring(0, 200);

            cmd.Parameters.AddWithValue("@p", (object?)personId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", u);
            cmd.Parameters.AddWithValue("@m", (object?)method ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rq", Trunc(req));
            cmd.Parameters.AddWithValue("@rs", Trunc(resp));
            cmd.Parameters.AddWithValue("@ip", (object?)ip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", elapsed);
            cmd.Parameters.AddWithValue("@s", success);
            cn.Open();
            cmd.ExecuteNonQuery();
        }
        catch { /* 日志失败不阻断业务 */ }
    }

    private static object Trunc(string? s)
    {
        if (s == null) return DBNull.Value;
        if (s.Length > 3500) s = s.Substring(0, 3500);
        return s;
    }
}

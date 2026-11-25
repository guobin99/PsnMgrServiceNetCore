using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 接口与集成：端点登记、密钥管理（不返回明文与密文）、连通测试、同步任务。
/// 端点编码一经创建不可修改，避免代码中的引用失效。
/// </summary>
[Route("api/system/integrations")]
[ApiController]
public class IntegrationsController : ControllerBase
{
    private static readonly Dictionary<string, DateTime> LastTest = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object TestLock = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private const string EndpointColumns =
        @"ID_KEY AS id, Endpoint_Code AS endpointCode, Endpoint_Name AS endpointName, Endpoint_Type AS endpointType,
          Base_Url AS baseUrl, Auth_Mode AS authMode, Config_Json AS configJson,
          CASE WHEN Secret_Cipher IS NULL OR Secret_Cipher = '' THEN 0 ELSE 1 END AS hasSecret,
          Secret_Updated_Time AS secretUpdatedTime, Timeout_Sec AS timeoutSec, Retry_Count AS retryCount,
          Is_Enabled AS isEnabled, Health_Status AS healthStatus, Last_Check_Time AS lastCheckTime,
          Last_Check_Message AS lastCheckMessage, Sort_No AS sortNo,
          (SELECT COUNT(1) FROM dbo.BS_SyncTask t WHERE t.Endpoint_Code = e.Endpoint_Code) AS taskCount,
          (SELECT COUNT(1) FROM dbo.BS_IntegrationSecretHistory h WHERE h.Endpoint_Code = e.Endpoint_Code) AS secretHistoryCount";

    /// <summary>GET api/system/integrations</summary>
    [HttpGet]
    [RequirePermission("system:integration:view")]
    public IActionResult List([FromQuery] string? type, [FromQuery] string? keyword)
    {
        using var db = SqlAccess.Open();
        var sql = $"SELECT {EndpointColumns} FROM dbo.BS_IntegrationEndpoint e WHERE 1 = 1";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(type))
        {
            sql += " AND e.Endpoint_Type = @type";
            parameters.Add(SqlSession.P("@type", type.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " AND (e.Endpoint_Code LIKE @key OR e.Endpoint_Name LIKE @key OR e.Base_Url LIKE @key)";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        sql += " ORDER BY ISNULL(e.Sort_No, 999), e.Endpoint_Code";
        return Api.Ok(db.Query<EndpointDto>(sql, parameters.ToArray()));
    }

    /// <summary>GET api/system/integrations/{code}：不返回密文与明文。</summary>
    [HttpGet("{code}")]
    [RequirePermission("system:integration:view")]
    public IActionResult Get(string code)
    {
        using var db = SqlAccess.Open();
        var row = LoadEndpoint(db, code);
        if (row == null) return Api.NotFound("端点不存在");
        return Api.Ok(row);
    }

    /// <summary>POST api/system/integrations</summary>
    [HttpPost]
    [RequirePermission("system:integration:update")]
    public IActionResult Create([FromBody] EndpointSaveRequest request)
    {
        var code = (request?.Endpoint_Code ?? "").Trim().ToUpperInvariant();
        var name = (request?.Endpoint_Name ?? "").Trim();
        var type = (request?.Endpoint_Type ?? "").Trim().ToLowerInvariant();
        if (code.Length == 0 || code.Length > 50) return Api.BadRequest("端点编码必填且不超过 50 个字符");
        if (name.Length == 0) return Api.BadRequest("端点名称不能为空");
        if (type.Length == 0) return Api.BadRequest("端点类型不能为空");

        using var db = SqlAccess.Open();
        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = @c", SqlSession.P("@c", code)) > 0)
            return Api.StateConflict($"端点编码 {code} 已存在");

        db.Exec(
            @"INSERT INTO dbo.BS_IntegrationEndpoint
                (Endpoint_Code, Endpoint_Name, Endpoint_Type, Base_Url, Auth_Mode, Config_Json, Timeout_Sec, Retry_Count,
                 Is_Enabled, Sort_No, Update_User, Update_Time)
              VALUES (@code, @name, @type, @url, @auth, @config, @timeout, @retry, @enabled, @sort, @user, GETDATE())",
            SqlSession.P("@code", code), SqlSession.P("@name", name), SqlSession.P("@type", type),
            SqlSession.P("@url", Trim(request!.Base_Url, 300)), SqlSession.P("@auth", Trim(request.Auth_Mode ?? "none", 20)),
            SqlSession.P("@config", request.Config_Json), SqlSession.P("@timeout", request.Timeout_Sec ?? 15),
            SqlSession.P("@retry", (byte)Math.Clamp(request.Retry_Count ?? 1, 0, 9)),
            SqlSession.P("@enabled", request.Is_Enabled ?? true), SqlSession.P("@sort", request.Sort_No ?? 99),
            SqlSession.P("@user", HttpContext.User.GetUserNo()));

        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "integration", "create", "endpoint", code, null, new { name, type, url = request.Base_Url });
        return Api.Ok(new { endpointCode = code });
    }

    /// <summary>PUT api/system/integrations/{code}：编码不可改。</summary>
    [HttpPut("{code}")]
    [RequirePermission("system:integration:update")]
    public IActionResult Update(string code, [FromBody] EndpointSaveRequest request)
    {
        using var db = SqlAccess.Open();
        var current = LoadEndpoint(db, code);
        if (current == null) return Api.NotFound("端点不存在");

        var name = (request?.Endpoint_Name ?? current.endpointName).Trim();
        if (name.Length == 0) return Api.BadRequest("端点名称不能为空");

        db.Exec(
            @"UPDATE dbo.BS_IntegrationEndpoint
                 SET Endpoint_Name = @name, Endpoint_Type = @type, Base_Url = @url, Auth_Mode = @auth,
                     Config_Json = @config, Timeout_Sec = @timeout, Retry_Count = @retry,
                     Is_Enabled = @enabled, Sort_No = @sort, Update_User = @user, Update_Time = GETDATE()
               WHERE Endpoint_Code = @code",
            SqlSession.P("@name", name), SqlSession.P("@type", request?.Endpoint_Type ?? current.endpointType),
            SqlSession.P("@url", Trim(request?.Base_Url ?? current.baseUrl, 300)),
            SqlSession.P("@auth", Trim(request?.Auth_Mode ?? current.authMode ?? "none", 20)),
            SqlSession.P("@config", request?.Config_Json ?? current.configJson),
            SqlSession.P("@timeout", request?.Timeout_Sec ?? current.timeoutSec ?? 15),
            SqlSession.P("@retry", (byte)Math.Clamp(request?.Retry_Count ?? current.retryCount ?? 1, 0, 9)),
            SqlSession.P("@enabled", request?.Is_Enabled ?? current.isEnabled),
            SqlSession.P("@sort", request?.Sort_No ?? current.sortNo ?? 99),
            SqlSession.P("@user", HttpContext.User.GetUserNo()), SqlSession.P("@code", code));

        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "integration", "update", "endpoint", code,
            new { current.endpointName, current.baseUrl, current.isEnabled },
            new { name, url = request?.Base_Url, enabled = request?.Is_Enabled });
        return Api.Ok(new { endpointCode = code });
    }

    /// <summary>PUT api/system/integrations/{code}/secret：更换密钥，旧密文进历史版本。</summary>
    [HttpPut("{code}/secret")]
    [RequirePermission("system:integration:update")]
    public IActionResult SetSecret(string code, [FromBody] EndpointSecretRequest request)
    {
        var secret = request?.Secret ?? "";
        if (secret.Trim().Length == 0) return Api.Fail(400, "40002", "密钥不能为空");
        if (!string.Equals(secret, request!.Secret_Confirm, StringComparison.Ordinal))
            return Api.BadRequest("两次输入的密钥不一致");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Api.BadRequest("请填写变更原因");

        using var db = SqlAccess.Open();
        var row = db.QuerySingle<SecretRow>(
            "SELECT ID_KEY AS id, Endpoint_Code AS endpointCode, Secret_Cipher AS secretCipher FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = @c",
            SqlSession.P("@c", code));
        if (row == null) return Api.NotFound("端点不存在");

        string cipher;
        try
        {
            cipher = SecretCipher.Protect(secret);
        }
        catch (Exception)
        {
            return Api.Fail(409, "40906", "密钥加密失败，请检查服务端密钥材料（环境变量 BS_CONFIG_KEY）");
        }

        db.Begin();
        try
        {
            if (!string.IsNullOrWhiteSpace(row.secretCipher))
            {
                db.Exec(
                    "INSERT INTO dbo.BS_IntegrationSecretHistory (Endpoint_Code, Secret_Cipher, Change_Reason, Change_User, Change_Time) VALUES (@c, @s, @r, @u, GETDATE())",
                    SqlSession.P("@c", code), SqlSession.P("@s", row.secretCipher),
                    SqlSession.P("@r", Trim(request.Reason, 200)), SqlSession.P("@u", HttpContext.User.GetUserNo()));
            }

            db.Exec(
                @"UPDATE dbo.BS_IntegrationEndpoint SET Secret_Cipher = @s, Secret_Updated_Time = GETDATE(), Update_User = @u, Update_Time = GETDATE()
                   WHERE Endpoint_Code = @c",
                SqlSession.P("@s", cipher), SqlSession.P("@u", HttpContext.User.GetUserNo()), SqlSession.P("@c", code));

            // 只保留最近三次历史
            db.Exec(
                @"DELETE FROM dbo.BS_IntegrationSecretHistory
                   WHERE Endpoint_Code = @c AND ID_KEY NOT IN (
                         SELECT TOP 3 ID_KEY FROM dbo.BS_IntegrationSecretHistory WHERE Endpoint_Code = @c ORDER BY ID_KEY DESC)",
                SqlSession.P("@c", code));

            SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "integration", "changeSecret", "endpoint", code, null, new { reason = request.Reason });
            return Api.Ok(new { endpointCode = code, hasSecret = true }, "密钥已更新");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>POST api/system/integrations/{code}/secret/rollback：回滚到上一版本。</summary>
    [HttpPost("{code}/secret/rollback")]
    [RequirePermission("system:integration:update")]
    public IActionResult RollbackSecret(string code, [FromBody] EndpointSecretRequest? request)
    {
        using var db = SqlAccess.Open();
        var latest = db.QuerySingle<SecretRow>(
            "SELECT TOP 1 ID_KEY AS id, Secret_Cipher AS secretCipher FROM dbo.BS_IntegrationSecretHistory WHERE Endpoint_Code = @c ORDER BY ID_KEY DESC",
            SqlSession.P("@c", code));
        if (latest == null) return Api.StateConflict("没有可回滚的密钥历史");

        db.Begin();
        try
        {
            db.Exec(
                "UPDATE dbo.BS_IntegrationEndpoint SET Secret_Cipher = @s, Secret_Updated_Time = GETDATE(), Update_Time = GETDATE() WHERE Endpoint_Code = @c",
                SqlSession.P("@s", latest.secretCipher), SqlSession.P("@c", code));
            db.Exec("DELETE FROM dbo.BS_IntegrationSecretHistory WHERE ID_KEY = @id", SqlSession.P("@id", latest.id));
            SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "integration", "rollbackSecret", "endpoint", code, null,
                new { reason = request?.Reason ?? "回滚到上一版本" });
            return Api.Ok(new { endpointCode = code }, "已回滚到上一版本密钥");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>PUT api/system/integrations/{code}/enabled</summary>
    [HttpPut("{code}/enabled")]
    [RequirePermission("system:integration:toggle")]
    public IActionResult Toggle(string code, [FromBody] EndpointToggleRequest request)
    {
        using var db = SqlAccess.Open();
        var affected = db.Exec(
            "UPDATE dbo.BS_IntegrationEndpoint SET Is_Enabled = @e, Update_User = @u, Update_Time = GETDATE() WHERE Endpoint_Code = @c",
            SqlSession.P("@e", request?.Enabled ?? true), SqlSession.P("@u", HttpContext.User.GetUserNo()), SqlSession.P("@c", code));
        if (affected == 0) return Api.NotFound("端点不存在");

        // 停用端点后其同步任务自动暂停，避免调用方等到超时
        if (request?.Enabled == false)
            db.Exec("UPDATE dbo.BS_SyncTask SET Is_Enabled = 0, Update_Time = GETDATE() WHERE Endpoint_Code = @c", SqlSession.P("@c", code));

        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "integration", "toggle", "endpoint", code, null, new { enabled = request?.Enabled });
        return Api.Ok(new { endpointCode = code, isEnabled = request?.Enabled ?? true });
    }

    /// <summary>POST api/system/integrations/{code}/test：连通测试，30 秒内重复调用返回 42902。</summary>
    [HttpPost("{code}/test")]
    [RequirePermission("system:integration:test")]
    public IActionResult Test(string code)
    {
        lock (TestLock)
        {
            if (LastTest.TryGetValue(code, out var last) && (DateTime.Now - last).TotalSeconds < 30)
                return Api.Fail(429, "42902", "连通测试过于频繁，请 30 秒后再试");
            LastTest[code] = DateTime.Now;
        }

        using var db = SqlAccess.Open();
        var row = LoadEndpoint(db, code);
        if (row == null) return Api.NotFound("端点不存在");

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var message = "";
        try
        {
            switch (row.endpointType)
            {
                case "database":
                    success = ProbeDatabase(out message);
                    break;
                case "wechat":
                case "payment":
                case "sms":
                    if (!row.hasSecret)
                    {
                        message = "密钥未配置";
                        break;
                    }
                    success = string.IsNullOrWhiteSpace(row.baseUrl)
                        ? SetMessage(out message, "已配置密钥（未配置地址，跳过网络探测）")
                        : ProbeHttp(row.baseUrl!, row.timeoutSec ?? 15, out message);
                    break;
                default:
                    if (string.IsNullOrWhiteSpace(row.baseUrl))
                    {
                        message = "未配置地址";
                        break;
                    }
                    success = ProbeHttp(row.baseUrl!, row.timeoutSec ?? 15, out message);
                    break;
            }
        }
        catch (Exception ex)
        {
            message = ex.Message;
        }
        stopwatch.Stop();
        var elapsed = (int)stopwatch.ElapsedMilliseconds;

        db.Exec(
            @"UPDATE dbo.BS_IntegrationEndpoint SET Health_Status = @h, Last_Check_Time = GETDATE(), Last_Check_Message = @m WHERE Endpoint_Code = @c",
            SqlSession.P("@h", success ? (byte)1 : (byte)2), SqlSession.P("@m", Trim(message, 500)), SqlSession.P("@c", code));

        db.Exec(
            @"INSERT INTO dbo.BS_SyncLog (Task_Code, Sync_Type, Direction, Target, Status, Message, Elapsed, Trace_ID, Create_Time, Finish_Time)
              VALUES (NULL, 'system', 'callback', @target, @status, @message, @elapsed, @trace, GETDATE(), GETDATE())",
            SqlSession.P("@target", code), SqlSession.P("@status", success ? (byte)1 : (byte)2),
            SqlSession.P("@message", Trim(message, 1000)), SqlSession.P("@elapsed", elapsed),
            SqlSession.P("@trace", HttpContext.TraceIdentifier));

        ConfigAudit.Write(HttpContext, "integration", "test", "endpoint", code, null,
            new { success, elapsed, message }, success, message);
        return Api.Ok(new { endpointCode = code, success, elapsed, message, healthStatus = success ? 1 : 2 });
    }

    /// <summary>GET api/system/integrations/tasks</summary>
    [HttpGet("tasks")]
    [RequirePermission("system:integration:view")]
    public IActionResult Tasks([FromQuery] string? endpointCode, [FromQuery] string? keyword)
    {
        using var db = SqlAccess.Open();
        var sql = @"SELECT Task_ID AS taskId, Task_Code AS taskCode, Task_Name AS taskName, Task_Type AS taskType,
                           Endpoint_Code AS endpointCode, Direction AS direction, Cron_Expr AS cronExpr,
                           Batch_Size AS batchSize, Timeout_Sec AS timeoutSec, Is_Enabled AS isEnabled,
                           Last_Run_Time AS lastRunTime, Last_Status AS lastStatus, Last_Message AS lastMessage,
                           Next_Run_Time AS nextRunTime
                      FROM dbo.BS_SyncTask WHERE 1 = 1";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(endpointCode))
        {
            sql += " AND Endpoint_Code = @e";
            parameters.Add(SqlSession.P("@e", endpointCode.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " AND (Task_Code LIKE @k OR Task_Name LIKE @k)";
            parameters.Add(SqlSession.P("@k", "%" + keyword.Trim() + "%"));
        }
        sql += " ORDER BY Task_Code";
        return Api.Ok(db.Query<SyncTaskDto>(sql, parameters.ToArray()));
    }

    /// <summary>PUT api/system/integrations/tasks/{code}</summary>
    [HttpPut("tasks/{code}")]
    [RequirePermission("system:integration:update")]
    public IActionResult UpdateTask(string code, [FromBody] SyncTaskSaveRequest request)
    {
        using var db = SqlAccess.Open();
        var task = db.QuerySingle<SyncTaskDto>(
            "SELECT Task_ID AS taskId, Task_Code AS taskCode, Is_Enabled AS isEnabled, Batch_Size AS batchSize, Cron_Expr AS cronExpr FROM dbo.BS_SyncTask WHERE Task_Code = @c",
            SqlSession.P("@c", code));
        if (task == null) return Api.NotFound("任务不存在");

        db.Exec(
            @"UPDATE dbo.BS_SyncTask SET Cron_Expr = @cron, Batch_Size = @batch, Timeout_Sec = @timeout,
                     Is_Enabled = @enabled, Update_User = @user, Update_Time = GETDATE()
               WHERE Task_Code = @code",
            SqlSession.P("@cron", Trim(request?.Cron_Expr ?? task.cronExpr, 50)),
            SqlSession.P("@batch", request?.Batch_Size ?? task.batchSize ?? 200),
            SqlSession.P("@timeout", request?.Timeout_Sec ?? 60),
            SqlSession.P("@enabled", request?.Is_Enabled ?? task.isEnabled),
            SqlSession.P("@user", HttpContext.User.GetUserNo()), SqlSession.P("@code", code));

        ConfigAudit.Write(HttpContext, "integration", "updateTask", "syncTask", code,
            new { cron = task.cronExpr, enabled = task.isEnabled },
            new { cron = request?.Cron_Expr, enabled = request?.Is_Enabled });
        return Api.Ok(new { taskCode = code });
    }

    /// <summary>POST api/system/integrations/tasks/{code}/run：手动执行一次。</summary>
    [HttpPost("tasks/{code}/run")]
    [RequirePermission("system:integration:test")]
    public IActionResult RunTask(string code)
    {
        using var db = SqlAccess.Open();
        var task = db.QuerySingle<SyncTaskDto>(
            "SELECT Task_ID AS taskId, Task_Code AS taskCode, Task_Name AS taskName, Task_Type AS taskType, Endpoint_Code AS endpointCode, Direction AS direction FROM dbo.BS_SyncTask WHERE Task_Code = @c",
            SqlSession.P("@c", code));
        if (task == null) return Api.NotFound("任务不存在");

        if (!string.IsNullOrWhiteSpace(task.endpointCode))
        {
            var endpoint = LoadEndpoint(db, task.endpointCode!);
            if (endpoint is { isEnabled: false })
                return Api.Fail(409, "40906", $"集成端点 {task.endpointCode} 已停用，请先启用");
        }

        // 同一 Task_Code 不允许并发执行
        var running = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.BS_SyncLog WHERE Task_Code = @c AND Status = 0", SqlSession.P("@c", code));
        if (running > 0) return Api.Fail(409, "40905", "该任务正在执行中，请稍后再试");

        var traceId = HttpContext.TraceIdentifier;
        if (string.Equals(code, "LOG_ARCHIVE", StringComparison.OrdinalIgnoreCase))
        {
            var archived = LogArchiver.Run(db);
            db.Exec("UPDATE dbo.BS_SyncTask SET Last_Run_Time = GETDATE(), Last_Status = 1, Last_Message = @m, Update_Time = GETDATE() WHERE Task_Code = @c",
                SqlSession.P("@m", $"归档 {archived} 行"), SqlSession.P("@c", code));
            ConfigAudit.Write(HttpContext, "integration", "runTask", "syncTask", code, null, new { archived });
            return Api.Ok(new { taskCode = code, archived, status = 1 }, $"已归档 {archived} 行");
        }

        db.Exec(
            @"INSERT INTO dbo.BS_SyncLog (Task_Code, Sync_Type, Direction, Target, Status, Message, Trace_ID, Create_Time)
              VALUES (@code, @type, @dir, @target, 0, N'已提交，等待调度器执行', @trace, GETDATE())",
            SqlSession.P("@code", code), SqlSession.P("@type", task.taskType),
            SqlSession.P("@dir", task.direction), SqlSession.P("@target", task.endpointCode),
            SqlSession.P("@trace", traceId));
        db.Exec("UPDATE dbo.BS_SyncTask SET Last_Run_Time = GETDATE(), Last_Status = 0, Last_Message = N'已提交', Update_Time = GETDATE() WHERE Task_Code = @c",
            SqlSession.P("@c", code));

        ConfigAudit.Write(HttpContext, "integration", "runTask", "syncTask", code, null, new { traceId });
        return Api.Ok(new { taskCode = code, status = 0, traceId }, "已提交执行");
    }

    /* ---------------- 内部 ---------------- */

    public sealed class SecretRow
    {
        public int id { get; set; }
        public string endpointCode { get; set; } = "";
        public string? secretCipher { get; set; }
    }

    private static EndpointDto? LoadEndpoint(SqlSession db, string code)
    {
        return db.QuerySingle<EndpointDto>(
            $"SELECT {EndpointColumns} FROM dbo.BS_IntegrationEndpoint e WHERE e.Endpoint_Code = @c",
            SqlSession.P("@c", code));
    }

    private static bool ProbeDatabase(out string message)
    {
        var stopwatch = Stopwatch.StartNew();
        using var db = SqlAccess.Open();
        db.ScalarInt("SELECT 1");
        stopwatch.Stop();
        message = $"数据库响应正常，{stopwatch.ElapsedMilliseconds} ms";
        return true;
    }

    private static bool ProbeHttp(string url, int timeoutSec, out string message)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(timeoutSec, 3, 60)));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = Http.SendAsync(request, cts.Token).GetAwaiter().GetResult();
            message = $"HTTP {(int)response.StatusCode}";
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            message = "请求失败：" + ex.Message;
            return false;
        }
    }

    private static bool SetMessage(out string message, string text)
    {
        message = text;
        return true;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}

/// <summary>
/// 日志归档：按 BS_LogPolicy 把超期记录复制到归档表后删除主表记录。
/// 单批一万行，归档失败即停止删除，保证不丢数据。
/// </summary>
public static class LogArchiver
{
    public static int Run(SqlSession db)
    {
        var total = 0;
        total += ArchiveOperLog(db);
        total += ArchiveLoginLog(db);
        total += ArchiveSyncLog(db);
        return total;
    }

    private static int ArchiveOperLog(SqlSession db)
    {
        var days = GetRetainDays(db, "OPER", 1095);
        var cutoff = DateTime.Now.AddDays(-days);
        var moved = 0;
        while (true)
        {
            var rows = db.Exec(
                @"INSERT INTO dbo.BS_OperLogArchive (ID_KEY, User_No, User_Name, Module, Action, Target, Target_Key,
                        Before_Value, After_Value, IP, User_Agent, Result, Message, Trace_ID, Opt_Time, Archive_Time)
                  SELECT TOP 10000 ID_KEY, User_No, User_Name, Module, Action, Target, Target_Key,
                         Before_Value, After_Value, IP, User_Agent, Result, Message, Trace_ID, Opt_Time, GETDATE()
                    FROM dbo.BS_OperLog WHERE Opt_Time < @cutoff AND ID_KEY NOT IN (SELECT ID_KEY FROM dbo.BS_OperLogArchive)",
                SqlSession.P("@cutoff", cutoff));
            if (rows == 0) break;
            db.Exec(
                @"DELETE TOP (10000) FROM dbo.BS_OperLog
                   WHERE Opt_Time < @cutoff AND ID_KEY IN (SELECT ID_KEY FROM dbo.BS_OperLogArchive)",
                SqlSession.P("@cutoff", cutoff));
            moved += rows;
            if (rows < 10000) break;
        }
        if (moved > 0) UpdateSnapshot(db, "OPER");
        return moved;
    }

    private static int ArchiveLoginLog(SqlSession db)
    {
        var days = GetRetainDays(db, "LOGIN", 365);
        var cutoff = DateTime.Now.AddDays(-days);
        var moved = 0;
        while (true)
        {
            var rows = db.Exec(
                @"INSERT INTO dbo.BS_LoginLogArchive (ID_KEY, User_No, Login_Type, Result, Fail_Reason, IP, User_Agent,
                        Login_Time, Logout_Time, Session_ID, Archive_Time)
                  SELECT TOP 10000 ID_KEY, User_No, Login_Type, Result, Fail_Reason, IP, User_Agent,
                         Login_Time, Logout_Time, Session_ID, GETDATE()
                    FROM dbo.BS_LoginLog WHERE Login_Time < @cutoff AND ID_KEY NOT IN (SELECT ID_KEY FROM dbo.BS_LoginLogArchive)",
                SqlSession.P("@cutoff", cutoff));
            if (rows == 0) break;
            db.Exec(
                @"DELETE TOP (10000) FROM dbo.BS_LoginLog
                   WHERE Login_Time < @cutoff AND ID_KEY IN (SELECT ID_KEY FROM dbo.BS_LoginLogArchive)",
                SqlSession.P("@cutoff", cutoff));
            moved += rows;
            if (rows < 10000) break;
        }
        if (moved > 0) UpdateSnapshot(db, "LOGIN");
        return moved;
    }

    private static int ArchiveSyncLog(SqlSession db)
    {
        var days = GetRetainDays(db, "SYNC", 180);
        var cutoff = DateTime.Now.AddDays(-days);
        var deleted = db.Exec("DELETE FROM dbo.BS_SyncLog WHERE Create_Time < @cutoff", SqlSession.P("@cutoff", cutoff));
        if (deleted > 0) UpdateSnapshot(db, "SYNC");
        return deleted;
    }

    private static int GetRetainDays(SqlSession db, string logType, int fallback)
    {
        var value = db.Scalar("SELECT Retain_Days FROM dbo.BS_LogPolicy WHERE Log_Type = @t", SqlSession.P("@t", logType));
        return value == null ? fallback : Convert.ToInt32(value);
    }

    private static void UpdateSnapshot(SqlSession db, string logType)
    {
        db.Exec("UPDATE dbo.BS_LogPolicy SET Last_Archive_Time = GETDATE() WHERE Log_Type = @t", SqlSession.P("@t", logType));
    }
}

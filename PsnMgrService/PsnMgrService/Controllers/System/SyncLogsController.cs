using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 同步日志：聚合 BS_SyncLog 与既有下发、微信、采集、订餐、门禁流水六类记录。
/// 各表字段差异大，不做物理合并，按统一视图适配后合并分页。
/// </summary>
[Route("api/system/logs/sync")]
[ApiController]
public class SyncLogsController : ControllerBase
{
    private const int MaxSpanDays = 31;

    private static readonly Dictionary<string, string> SourceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BS"] = "BS 任务",
        ["MOC"] = "名单下发",
        ["WX"] = "微信支付",
        ["COLLECT"] = "采集服务",
        ["ORDER"] = "订餐订单",
        ["ACCESS"] = "门禁流水",
    };

    /// <summary>GET api/system/logs/sync</summary>
    [HttpGet]
    [RequirePermission("system:log:sync")]
    public IActionResult List(
        [FromQuery] DateTime? begin, [FromQuery] DateTime? end, [FromQuery] string? source,
        [FromQuery] string? syncType, [FromQuery] string? direction, [FromQuery] string? status,
        [FromQuery] string? target, [FromQuery] string? taskCode,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);

        if (!string.IsNullOrWhiteSpace(syncType)) rows = rows.Where(r => r.syncType == syncType).ToList();
        if (!string.IsNullOrWhiteSpace(direction)) rows = rows.Where(r => r.direction == direction).ToList();
        if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(r => r.status == status).ToList();
        if (!string.IsNullOrWhiteSpace(taskCode)) rows = rows.Where(r => r.taskCode == taskCode).ToList();
        if (!string.IsNullOrWhiteSpace(target))
        {
            var key = target.Trim();
            rows = rows.Where(r => (r.target ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                   || (r.targetKey ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var items = rows.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.source, r.sourceLabel, r.id, r.time, r.taskCode, r.syncType, r.direction, r.target, r.targetKey,
                r.status, r.elapsed, r.message, r.retryCount, r.traceId,
                canRetry = r.source == "BS" && r.status == "failed",
            })
            .ToList();

        return Api.Ok(new { total = rows.Count, items, sources = SourceLabels });
    }

    /// <summary>GET api/system/logs/sync/{source}/{id}：详情，请求与响应按规则脱敏。</summary>
    [HttpGet("{source}/{id:long}")]
    [RequirePermission("system:log:sync")]
    public IActionResult Detail(string source, long id)
    {
        using var db = SqlAccess.Open();
        var row = LoadOne(db, source, id);
        if (row == null) return Api.NotFound("同步记录不存在");
        return Api.Ok(new
        {
            row = new
            {
                row.source, row.id, row.time, row.taskCode, row.syncType, row.direction, row.target, row.targetKey,
                row.status, row.elapsed, row.message, row.retryCount, row.traceId,
                requestBody = LogMask.Apply(row.requestBody),
                responseBody = LogMask.Apply(row.responseBody),
            },
            note = row.source == "BS"
                ? "BS 任务记录含请求与响应摘要，敏感字段已脱敏。"
                : "该记录由既有业务表提供，BS 只读展示。",
        });
    }

    /// <summary>POST api/system/logs/sync/{id}/retry：仅对失败且可重试的 BS 任务。</summary>
    [HttpPost("{id:long}/retry")]
    [RequirePermission("system:log:sync:retry")]
    public IActionResult Retry(long id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<SyncLogDto>(
            @"SELECT 'BS' AS source, ID_KEY AS id, Task_Code AS taskCode, Sync_Type AS syncType, Direction AS direction,
                     Target AS target, Target_Key AS targetKey, Status AS status, Message AS message
                FROM dbo.BS_SyncLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("同步记录不存在");
        if (row.status != "failed") return Api.StateConflict("只有失败的记录才允许重试");

        if (!string.IsNullOrWhiteSpace(row.taskCode))
        {
            var running = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_SyncLog WHERE Task_Code = @c AND Status = 0",
                SqlSession.P("@c", row.taskCode));
            if (running > 0) return Api.Fail(409, "40905", "该任务正在执行中，请稍后再试");
        }

        db.Exec(
            @"INSERT INTO dbo.BS_SyncLog (Task_Code, Sync_Type, Direction, Target, Target_Key, Status, Message, Retry_Count, Trace_ID, Create_Time)
              VALUES (@code, @type, @dir, @target, @key, 0, @msg, 1, @trace, GETDATE())",
            SqlSession.P("@code", row.taskCode), SqlSession.P("@type", row.syncType), SqlSession.P("@dir", row.direction),
            SqlSession.P("@target", row.target), SqlSession.P("@key", row.targetKey),
            SqlSession.P("@msg", $"由记录 {id} 重试"), SqlSession.P("@trace", HttpContext.TraceIdentifier));

        var newId = Convert.ToInt32(db.Scalar("SELECT MAX(ID_KEY) FROM dbo.BS_SyncLog"));
        db.Exec("UPDATE dbo.BS_SyncLog SET Message = @m, Retry_Count = Retry_Count + 1 WHERE ID_KEY = @id",
            SqlSession.P("@m", $"已重试，新记录 {newId}"), SqlSession.P("@id", id));

        ConfigAudit.Write(HttpContext, "log", "retry", "syncLog", id.ToString(CultureInfo.InvariantCulture), null, new { newId });
        return Api.Ok(new { retryId = newId, sourceId = id }, "已创建重试记录");
    }

    /// <summary>GET api/system/logs/sync/export</summary>
    [HttpGet("export")]
    [RequirePermission("system:log:sync:export")]
    public IActionResult Export([FromQuery] DateTime? begin, [FromQuery] DateTime? end,
        [FromQuery] string? source, [FromQuery] string? status)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);
        if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(r => r.status == status).ToList();

        var limit = SysConfigStore.GetInt("Export.MaxRows", 50000);
        var truncated = rows.Count > limit;
        var sb = new StringBuilder();
        sb.AppendLine("时间,来源,类型,方向,目标,状态,耗时(ms),说明");
        foreach (var row in rows.Take(limit))
        {
            sb.AppendLine(string.Join(",",
                row.time.ToString("yyyy-MM-dd HH:mm:ss"), row.sourceLabel, Escape(row.syncType), Escape(row.direction),
                Escape(row.targetKey ?? row.target), StatusLabel(row.status),
                row.elapsed?.ToString(CultureInfo.InvariantCulture) ?? "", Escape(LogMask.Apply(row.message))));
        }
        ConfigAudit.Write(HttpContext, "log", "export", "syncLog", null, null, new { rowCount = Math.Min(rows.Count, limit) });
        return Api.Ok(new
        {
            fileName = $"sync-log-{DateTime.Now:yyyyMMddHHmmss}.csv",
            content = sb.ToString(),
            rowCount = Math.Min(rows.Count, limit),
            truncated,
        });
    }

    /* ---------------- 内部 ---------------- */

    private static List<SyncLogDto> Query(SqlSession db, DateTime from, DateTime to, string? source)
    {
        var rows = new List<SyncLogDto>();
        bool Want(string key) => string.IsNullOrWhiteSpace(source) || string.Equals(source, key, StringComparison.OrdinalIgnoreCase);

        if (Want("BS"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'BS' AS source, ID_KEY AS id, Create_Time AS time, Task_Code AS taskCode, Sync_Type AS syncType,
                         Direction AS direction, Target AS target, Target_Key AS targetKey,
                         CASE Status WHEN 0 THEN 'pending' WHEN 1 THEN 'success' WHEN 2 THEN 'failed' WHEN 3 THEN 'partial' ELSE 'running' END AS status,
                         Elapsed AS elapsed, Message AS message, Retry_Count AS retryCount, Trace_ID AS traceId,
                         Request_Body AS requestBody, Response_Body AS responseBody
                    FROM dbo.BS_SyncLog WHERE Create_Time >= @b AND Create_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (Want("MOC"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'MOC' AS source, ID_KEY AS id, upload_time AS time, NULL AS taskCode, 'person' AS syncType,
                         'down' AS direction, moc_no AS target, CAST(person_id AS VARCHAR(50)) AS targetKey,
                         CASE state WHEN 1 THEN 'success' WHEN -1 THEN 'failed' ELSE 'running' END AS status,
                         NULL AS elapsed, err_msg AS message, 0 AS retryCount, NULL AS traceId,
                         NULL AS requestBody, NULL AS responseBody
                    FROM dbo.st_moc_upload_name_log WHERE upload_time >= @b AND upload_time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (Want("WX"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'WX' AS source, ID_KEY AS id, Create_Time AS time, NULL AS taskCode, 'wechat' AS syncType,
                         'callback' AS direction, Api_Url AS target, CAST(Person_ID AS VARCHAR(50)) AS targetKey,
                         CASE WHEN Success = 1 THEN 'success' ELSE 'failed' END AS status,
                         Elapsed AS elapsed, LEFT(Response_Body, 200) AS message, 0 AS retryCount, NULL AS traceId,
                         Request_Body AS requestBody, Response_Body AS responseBody
                    FROM dbo.WX_ApiLog WHERE Create_Time >= @b AND Create_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (Want("COLLECT"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'COLLECT' AS source, Id_Key AS id, Log_DateTime AS time, NULL AS taskCode, 'collect' AS syncType,
                         'collect' AS direction, NULL AS target, NULL AS targetKey, 'unknown' AS status,
                         NULL AS elapsed, Log_Msg AS message, 0 AS retryCount, NULL AS traceId,
                         NULL AS requestBody, NULL AS responseBody
                    FROM dbo.XF_CollectLog WHERE Log_DateTime >= @b AND Log_DateTime < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (Want("ORDER"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'ORDER' AS source, ID_KEY AS id, Order_Time AS time, NULL AS taskCode, 'order' AS syncType,
                         'down' AS direction, Moc_No_DC AS target, Order_No AS targetKey,
                         CASE WHEN Data_Type >= 2 THEN 'success' ELSE 'running' END AS status,
                         NULL AS elapsed, Remark AS message, 0 AS retryCount, NULL AS traceId,
                         NULL AS requestBody, NULL AS responseBody
                    FROM dbo.DC_OrderDataLog WHERE Order_Time >= @b AND Order_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (Want("ACCESS"))
        {
            rows.AddRange(db.Query<SyncLogDto>(
                @"SELECT 'ACCESS' AS source, ID_Key AS id, Create_Date AS time, NULL AS taskCode, 'access' AS syncType,
                         'auth' AS direction, Ctrl_No + '-' + CAST(Door_No AS VARCHAR(10)) AS target,
                         Person_No AS targetKey,
                         CASE WHEN ResultCode = 3 THEN 'success' ELSE 'failed' END AS status,
                         NULL AS elapsed, EventType AS message, 0 AS retryCount, NULL AS traceId,
                         NULL AS requestBody, NULL AS responseBody
                    FROM dbo.ST_TrafficLog2201 WHERE Create_Date >= @b AND Create_Date < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }

        foreach (var row in rows) row.sourceLabel = SourceLabels.TryGetValue(row.source, out var label) ? label : row.source;

        return rows.OrderByDescending(r => r.time).ThenByDescending(r => r.id).ToList();
    }

    private static SyncLogDto? LoadOne(SqlSession db, string source, long id)
    {
        var key = (source ?? "").Trim().ToUpperInvariant();
        switch (key)
        {
            case "BS":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'BS' AS source, ID_KEY AS id, Create_Time AS time, Task_Code AS taskCode, Sync_Type AS syncType,
                             Direction AS direction, Target AS target, Target_Key AS targetKey,
                             CASE Status WHEN 0 THEN 'pending' WHEN 1 THEN 'success' WHEN 2 THEN 'failed' WHEN 3 THEN 'partial' ELSE 'running' END AS status,
                             Elapsed AS elapsed, Message AS message, Retry_Count AS retryCount, Trace_ID AS traceId,
                             Request_Body AS requestBody, Response_Body AS responseBody
                        FROM dbo.BS_SyncLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
            case "WX":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'WX' AS source, ID_KEY AS id, Create_Time AS time, NULL AS taskCode, 'wechat' AS syncType,
                             'callback' AS direction, Api_Url AS target, CAST(Person_ID AS VARCHAR(50)) AS targetKey,
                             CASE WHEN Success = 1 THEN 'success' ELSE 'failed' END AS status,
                             Elapsed AS elapsed, LEFT(Response_Body, 200) AS message, 0 AS retryCount, NULL AS traceId,
                             Request_Body AS requestBody, Response_Body AS responseBody
                        FROM dbo.WX_ApiLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
            case "MOC":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'MOC' AS source, ID_KEY AS id, upload_time AS time, NULL AS taskCode, 'person' AS syncType,
                             'down' AS direction, moc_no AS target, CAST(person_id AS VARCHAR(50)) AS targetKey,
                             CASE state WHEN 1 THEN 'success' WHEN -1 THEN 'failed' ELSE 'running' END AS status,
                             NULL AS elapsed, err_msg AS message, 0 AS retryCount, NULL AS traceId,
                             NULL AS requestBody, NULL AS responseBody
                        FROM dbo.st_moc_upload_name_log WHERE ID_KEY = @id", SqlSession.P("@id", id));
            case "COLLECT":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'COLLECT' AS source, Id_Key AS id, Log_DateTime AS time, NULL AS taskCode, 'collect' AS syncType,
                             'collect' AS direction, NULL AS target, NULL AS targetKey, 'unknown' AS status,
                             NULL AS elapsed, Log_Msg AS message, 0 AS retryCount, NULL AS traceId,
                             NULL AS requestBody, NULL AS responseBody
                        FROM dbo.XF_CollectLog WHERE Id_Key = @id", SqlSession.P("@id", id));
            case "ORDER":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'ORDER' AS source, ID_KEY AS id, Order_Time AS time, NULL AS taskCode, 'order' AS syncType,
                             'down' AS direction, Moc_No_DC AS target, Order_No AS targetKey,
                             CASE WHEN Data_Type >= 2 THEN 'success' ELSE 'running' END AS status,
                             NULL AS elapsed, Remark AS message, 0 AS retryCount, NULL AS traceId,
                             NULL AS requestBody, NULL AS responseBody
                        FROM dbo.DC_OrderDataLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
            case "ACCESS":
                return db.QuerySingle<SyncLogDto>(
                    @"SELECT 'ACCESS' AS source, ID_Key AS id, Create_Date AS time, NULL AS taskCode, 'access' AS syncType,
                             'auth' AS direction, Ctrl_No + '-' + CAST(Door_No AS VARCHAR(10)) AS target,
                             Person_No AS targetKey, CASE WHEN ResultCode = 3 THEN 'success' ELSE 'failed' END AS status,
                             NULL AS elapsed, EventType AS message, 0 AS retryCount, NULL AS traceId,
                             NULL AS requestBody, NULL AS responseBody
                        FROM dbo.ST_TrafficLog2201 WHERE ID_Key = @id", SqlSession.P("@id", id));
            default:
                return null;
        }
    }

    private static string StatusLabel(string status) => status switch
    {
        "pending" => "待执行",
        "success" => "成功",
        "failed" => "失败",
        "running" => "进行中",
        "partial" => "部分成功",
        _ => "未知",
    };

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}

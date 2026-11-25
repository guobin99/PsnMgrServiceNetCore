using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 系统日志：CS 的 ST_SysLog 与 BS 的系统级事件合并展示，只读，不提供删除。
/// </summary>
[Route("api/system/logs/system")]
[ApiController]
public class SystemLogsController : ControllerBase
{
    private const int MaxSpanDays = 31;

    /// <summary>GET api/system/logs/system</summary>
    [HttpGet]
    [RequirePermission("system:log:system")]
    public IActionResult List(
        [FromQuery] DateTime? begin, [FromQuery] DateTime? end, [FromQuery] string? user,
        [FromQuery] string? computer, [FromQuery] string? systemNo, [FromQuery] string? source,
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);

        if (!string.IsNullOrWhiteSpace(systemNo)) rows = rows.Where(r => r.systemNo == systemNo).ToList();
        if (!string.IsNullOrWhiteSpace(user))
        {
            var key = user.Trim();
            rows = rows.Where(r => (r.userName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(computer))
        {
            var key = computer.Trim();
            rows = rows.Where(r => (r.computer ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            rows = rows.Where(r => (r.eventDesc ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                   || (r.userName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Api.Ok(new { total = rows.Count, items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList() });
    }

    /// <summary>GET api/system/logs/system/{id}?source=CS|BS</summary>
    [HttpGet("{id:long}")]
    [RequirePermission("system:log:system")]
    public IActionResult Detail(long id, [FromQuery] string? source)
    {
        using var db = SqlAccess.Open();
        var isBs = string.Equals(source, "BS", StringComparison.OrdinalIgnoreCase);
        var row = isBs
            ? db.QuerySingle<SystemLogDto>(
                @"SELECT 'BS' AS source, ID_KEY AS id, Opt_Time AS time, ISNULL(User_Name, User_No) AS userName,
                         IP AS computer, 'BS' AS systemNo, Action AS eventDesc, Trace_ID AS traceId, Module AS module, Action AS action
                    FROM dbo.BS_OperLog WHERE ID_KEY = @id", SqlSession.P("@id", id))
            : db.QuerySingle<SystemLogDto>(
                @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, User_Name AS userName, Opt_Computer AS computer,
                         System_No AS systemNo, Opt_log AS eventDesc, NULL AS traceId, NULL AS module, NULL AS action
                    FROM dbo.ST_SysLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("日志不存在");
        return Api.Ok(new
        {
            row,
            origin = isBs ? "BS" : "CS",
            note = isBs
                ? "该记录由 BS 写入，可按 traceId 在操作审计中查看完整前后值。"
                : "该记录由 CS 子系统写入，BS 只读展示；CS 端可删除该表记录。",
        });
    }

    /// <summary>GET api/system/logs/system/export</summary>
    [HttpGet("export")]
    [RequirePermission("system:log:system:export")]
    public IActionResult Export([FromQuery] DateTime? begin, [FromQuery] DateTime? end,
        [FromQuery] string? systemNo, [FromQuery] string? source, [FromQuery] string? keyword)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);
        if (!string.IsNullOrWhiteSpace(systemNo)) rows = rows.Where(r => r.systemNo == systemNo).ToList();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            rows = rows.Where(r => (r.eventDesc ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var limit = SysConfigStore.GetInt("Export.MaxRows", 50000);
        var truncated = rows.Count > limit;
        var sb = new StringBuilder();
        sb.AppendLine("时间,用户,机器名,子系统,事件描述,来源");
        foreach (var row in rows.Take(limit))
        {
            sb.AppendLine(string.Join(",",
                row.time.ToString("yyyy-MM-dd HH:mm:ss"), Escape(row.userName), Escape(row.computer),
                Escape(row.systemNo), Escape(row.eventDesc), row.source));
        }
        ConfigAudit.Write(HttpContext, "log", "export", "systemLog", null, null, new { rowCount = Math.Min(rows.Count, limit) });
        return Api.Ok(new
        {
            fileName = $"system-log-{DateTime.Now:yyyyMMddHHmmss}.csv",
            content = sb.ToString(),
            rowCount = Math.Min(rows.Count, limit),
            truncated,
            message = truncated ? $"导出已达上限 {limit} 行，请缩小时间范围" : null,
        });
    }

    private static List<SystemLogDto> Query(SqlSession db, DateTime from, DateTime to, string? source)
    {
        var rows = new List<SystemLogDto>();
        var wantCs = string.IsNullOrWhiteSpace(source) || string.Equals(source, "CS", StringComparison.OrdinalIgnoreCase);
        var wantBs = string.IsNullOrWhiteSpace(source) || string.Equals(source, "BS", StringComparison.OrdinalIgnoreCase);

        if (wantCs)
        {
            rows.AddRange(db.Query<SystemLogDto>(
                @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, User_Name AS userName, Opt_Computer AS computer,
                         System_No AS systemNo, Opt_log AS eventDesc, NULL AS traceId, NULL AS module, NULL AS action
                    FROM dbo.ST_SysLog WHERE Opt_Time >= @b AND Opt_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (wantBs)
        {
            rows.AddRange(db.Query<SystemLogDto>(
                @"SELECT 'BS' AS source, ID_KEY AS id, Opt_Time AS time, ISNULL(User_Name, User_No) AS userName,
                         LEFT(ISNULL(User_Agent, IP), 40) AS computer, 'BS' AS systemNo, Action AS eventDesc,
                         Trace_ID AS traceId, Module AS module, Action AS action
                    FROM dbo.BS_OperLog WHERE Module = 'system' AND Opt_Time >= @b AND Opt_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        return rows.OrderByDescending(r => r.time).ThenByDescending(r => r.id).ToList();
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}

/// <summary>
/// 登录日志：以 BS_LoginLog 为主，补充 ST_SysLog 中的进入/退出记录。
/// 同一账号、同一分钟、同一来源、同一类型合并为一条并计数。
/// </summary>
[Route("api/system/logs/login")]
[ApiController]
public class LoginLogsController : ControllerBase
{
    private const int MaxSpanDays = 31;

    /// <summary>GET api/system/logs/login</summary>
    [HttpGet]
    [RequirePermission("system:log:login")]
    public IActionResult List(
        [FromQuery] DateTime? begin, [FromQuery] DateTime? end, [FromQuery] string? userNo,
        [FromQuery] string? userName, [FromQuery] string? source, [FromQuery] string? loginType,
        [FromQuery] int? result, [FromQuery] string? ip, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        using var db = SqlAccess.Open();
        var rows = Merge(db, from, to);

        if (!string.IsNullOrWhiteSpace(userNo))
        {
            var key = userNo.Trim();
            rows = rows.Where(r => (r.userNo ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var key = userName.Trim();
            rows = rows.Where(r => (r.userName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(source))
        {
            var key = source.Trim().ToUpperInvariant();
            rows = rows.Where(r => r.source == key).ToList();
        }
        if (!string.IsNullOrWhiteSpace(loginType))
        {
            var key = loginType.Trim().ToLowerInvariant();
            rows = rows.Where(r => r.loginType == key).ToList();
        }
        if (result.HasValue) rows = rows.Where(r => r.result == result.Value).ToList();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            var key = ip.Trim();
            rows = rows.Where(r => (r.ip ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Api.Ok(new { total = rows.Count, items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList() });
    }

    /// <summary>GET api/system/logs/login/{id}?source=BS|CS</summary>
    [HttpGet("{id:long}")]
    [RequirePermission("system:log:login")]
    public IActionResult Detail(long id, [FromQuery] string? source)
    {
        using var db = SqlAccess.Open();
        if (string.Equals(source, "CS", StringComparison.OrdinalIgnoreCase))
        {
            var cs = db.QuerySingle<SystemLogDto>(
                @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, User_Name AS userName, NULL AS computer,
                         System_No AS systemNo, Opt_log AS eventDesc FROM dbo.ST_SysLog WHERE ID_KEY = @id",
                SqlSession.P("@id", id));
            if (cs == null) return Api.NotFound("日志不存在");
            return Api.Ok(new
            {
                source = "CS",
                time = cs.time,
                account = cs.userName,
                userName = cs.userName,
                loginType = "enter",
                result = 0,
                failReason = (string?)null,
                ip = (string?)null,
                userAgent = (string?)null,
                logoutTime = (DateTime?)null,
                sessionId = (string?)null,
                note = "CS 记录没有账号编号、IP 与客户端信息，账号列显示为姓名。",
            });
        }

        var row = db.QuerySingle<LoginLogDto>(
            @"SELECT 'BS' AS source, ID_KEY AS id, Login_Time AS time, User_No AS userNo, NULL AS userName,
                     Login_Type AS loginType, Result AS result, Fail_Reason AS failReason, IP AS ip,
                     User_Agent AS userAgent, Logout_Time AS logoutTime, Session_ID AS sessionId
                FROM dbo.BS_LoginLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("日志不存在");
        return Api.Ok(new
        {
            source = "BS",
            time = row.time,
            account = row.userNo,
            userName = row.userNo,
            loginType = row.loginType,
            result = row.result,
            failReason = row.failReason,
            ip = row.ip,
            userAgent = row.userAgent,
            logoutTime = row.logoutTime,
            sessionId = row.sessionId,
            note = "BS 登录、登出、失败与锁定均记录在本表；管理员强制吊销令牌不单独留痕。",
        });
    }

    /// <summary>GET api/system/logs/login/statistics：顶部卡片与按小时分布。</summary>
    [HttpGet("statistics")]
    [RequirePermission("system:log:login")]
    public IActionResult Statistics()
    {
        using var db = SqlAccess.Open();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todaySuccess = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.BS_LoginLog WHERE Result = 0 AND Login_Time >= @b AND Login_Time < @e",
            SqlSession.P("@b", today), SqlSession.P("@e", tomorrow));
        var todayFail = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.BS_LoginLog WHERE Result = 1 AND Login_Time >= @b AND Login_Time < @e",
            SqlSession.P("@b", today), SqlSession.P("@e", tomorrow));
        var locked = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_UserInfo WHERE BS_Lock_Until IS NOT NULL AND BS_Lock_Until > GETDATE()");
        var abnormalIps = db.Query<CountRow>(
            @"SELECT IP AS keyName, COUNT(1) AS cnt FROM dbo.BS_LoginLog
               WHERE Result = 1 AND Login_Time >= DATEADD(minute, -10, GETDATE()) AND IP IS NOT NULL
               GROUP BY IP HAVING COUNT(1) > 10");

        var hourly = db.Query<CountRow>(
            @"SELECT CAST(DATEPART(hour, Login_Time) AS VARCHAR(2)) AS keyName,
                     SUM(CASE WHEN Result = 0 THEN 1 ELSE 0 END) AS cnt
                FROM dbo.BS_LoginLog WHERE Login_Time >= @b AND Login_Time < @e
               GROUP BY DATEPART(hour, Login_Time) ORDER BY DATEPART(hour, Login_Time)",
            SqlSession.P("@b", today), SqlSession.P("@e", tomorrow));

        var series = Enumerable.Range(0, 24)
            .Select(hour => new
            {
                hour,
                count = hourly.FirstOrDefault(h => h.keyName == hour.ToString(CultureInfo.InvariantCulture))?.cnt ?? 0,
            })
            .ToList();

        return Api.Ok(new
        {
            todaySuccess,
            todayFail,
            lockedAccounts = locked,
            abnormalIps = abnormalIps.Count,
            abnormalIpList = abnormalIps.Select(i => new { ip = i.keyName, count = i.cnt }).ToList(),
            hourly = series,
        });
    }

    /// <summary>GET api/system/logs/login/export</summary>
    [HttpGet("export")]
    [RequirePermission("system:log:login:export")]
    public IActionResult Export([FromQuery] DateTime? begin, [FromQuery] DateTime? end,
        [FromQuery] string? result, [FromQuery] string? source)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);

        using var db = SqlAccess.Open();
        var rows = Merge(db, from, to);
        if (!string.IsNullOrWhiteSpace(source))
        {
            var key = source.Trim().ToUpperInvariant();
            rows = rows.Where(r => r.source == key).ToList();
        }
        if (int.TryParse(result, out var resultValue)) rows = rows.Where(r => r.result == resultValue).ToList();

        var limit = SysConfigStore.GetInt("Export.MaxRows", 50000);
        var truncated = rows.Count > limit;
        var sb = new StringBuilder();
        sb.AppendLine("时间,账号,姓名,来源,类型,结果,IP,客户端,说明");
        foreach (var row in rows.Take(limit))
        {
            sb.AppendLine(string.Join(",",
                row.time.ToString("yyyy-MM-dd HH:mm:ss"), Escape(row.userNo), Escape(row.userName), row.source,
                row.loginType, row.result == 0 ? "成功" : "失败", Escape(row.ip), Escape(row.userAgent),
                Escape(row.failReason)));
        }
        ConfigAudit.Write(HttpContext, "log", "export", "loginLog", null, null, new { rowCount = Math.Min(rows.Count, limit) });
        return Api.Ok(new
        {
            fileName = $"login-log-{DateTime.Now:yyyyMMddHHmmss}.csv",
            content = sb.ToString(),
            rowCount = Math.Min(rows.Count, limit),
            truncated,
        });
    }

    private static List<LoginLogDto> Merge(SqlSession db, DateTime from, DateTime to)
    {
        var rows = new List<LoginLogDto>();
        rows.AddRange(db.Query<LoginLogDto>(
            @"SELECT 'BS' AS source, ID_KEY AS id, Login_Time AS time, User_No AS userNo, NULL AS userName,
                     Login_Type AS loginType, Result AS result, Fail_Reason AS failReason, IP AS ip,
                     User_Agent AS userAgent, Logout_Time AS logoutTime, Session_ID AS sessionId
                FROM dbo.BS_LoginLog WHERE Login_Time >= @b AND Login_Time < @e",
            SqlSession.P("@b", from), SqlSession.P("@e", to)));

        rows.AddRange(db.Query<LoginLogDto>(
            @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, NULL AS userNo, User_Name AS userName,
                     CASE WHEN Opt_log LIKE '%退出%' THEN 'logout' ELSE 'login' END AS loginType,
                     CAST(0 AS TINYINT) AS result, NULL AS failReason, NULL AS ip, NULL AS userAgent,
                     NULL AS logoutTime, NULL AS sessionId
                FROM dbo.ST_SysLog
               WHERE Opt_Time >= @b AND Opt_Time < @e
                 AND (Opt_log LIKE '%进入%' OR Opt_log LIKE '%登录%' OR Opt_log LIKE '%退出%')",
            SqlSession.P("@b", from), SqlSession.P("@e", to)));

        // 同账号 + 同分钟 + 同来源 + 同类型去重合并，计数加一
        var merged = rows
            .GroupBy(r => new
            {
                account = r.userNo ?? r.userName ?? "",
                minute = new DateTime(r.time.Year, r.time.Month, r.time.Day, r.time.Hour, r.time.Minute, 0),
                r.source,
                r.loginType,
            })
            .Select(g =>
            {
                var first = g.OrderBy(r => r.time).First();
                first.repeatCount = g.Count();
                return first;
            })
            .OrderByDescending(r => r.time)
            .ToList();
        return merged;
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}

/// <summary>
/// 操作审计：合并 ST_OptLog 与 BS_OperLog，如实标注来源；
/// CS 记录没有前后值，前端据此隐藏对比。
/// </summary>
[Route("api/system/logs/oper")]
[ApiController]
public class OperLogsController : ControllerBase
{
    private const int MaxSpanDays = 31;

    private static readonly string[] HighRiskActions =
    {
        "assign", "deleteRole", "update", "cleanup", "add", "changeSecret", "rollbackSecret", "resetPwd", "status",
    };

    /// <summary>GET api/system/logs/oper</summary>
    [HttpGet]
    [RequirePermission("system:log:oper")]
    public IActionResult List(
        [FromQuery] DateTime? begin, [FromQuery] DateTime? end, [FromQuery] string? userNo,
        [FromQuery] string? userName, [FromQuery] string? source, [FromQuery] string? module,
        [FromQuery] string? action, [FromQuery] string? targetKey, [FromQuery] int? result,
        [FromQuery] string? keyword, [FromQuery] bool? highRiskOnly,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);

        if (!string.IsNullOrWhiteSpace(userNo))
        {
            var key = userNo.Trim();
            rows = rows.Where(r => (r.userNo ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var key = userName.Trim();
            rows = rows.Where(r => (r.userName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(module)) rows = rows.Where(r => r.module == module).ToList();
        if (!string.IsNullOrWhiteSpace(action)) rows = rows.Where(r => r.action == action).ToList();
        if (!string.IsNullOrWhiteSpace(targetKey))
        {
            var key = targetKey.Trim();
            rows = rows.Where(r => (r.targetKey ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (result.HasValue) rows = rows.Where(r => r.result == result.Value).ToList();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            rows = rows.Where(r => (r.message ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                   || (r.targetKey ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (highRiskOnly == true)
            rows = rows.Where(r => HighRiskActions.Contains(r.action ?? "", StringComparer.OrdinalIgnoreCase)).ToList();

        var items = rows.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.source, r.id, r.time, r.userNo, r.userName, r.module, r.action, r.target, r.targetKey,
                r.result, r.message, r.ip, r.traceId,
                hasDiff = !string.IsNullOrWhiteSpace(r.beforeValue) || !string.IsNullOrWhiteSpace(r.afterValue),
                highRisk = HighRiskActions.Contains(r.action ?? "", StringComparer.OrdinalIgnoreCase),
            })
            .ToList();

        return Api.Ok(new { total = rows.Count, items });
    }

    /// <summary>GET api/system/logs/oper/{id}?source=BS|CS</summary>
    [HttpGet("{id:long}")]
    [RequirePermission("system:log:oper")]
    public IActionResult Detail(long id, [FromQuery] string? source)
    {
        using var db = SqlAccess.Open();
        if (string.Equals(source, "CS", StringComparison.OrdinalIgnoreCase))
        {
            var cs = db.QuerySingle<OperLogDto>(
                @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, NULL AS userNo, User_Name AS userName,
                         System_No AS module, Opt_Type AS action, Opt_Table AS target, NULL AS targetKey,
                         CAST(0 AS TINYINT) AS result, Opt_Info AS message, Opt_Computer AS ip, NULL AS traceId
                    FROM dbo.ST_OptLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
            if (cs == null) return Api.NotFound("日志不存在");
            return Api.Ok(new
            {
                row = cs,
                note = "CS 记录不含前后值，如需追查请结合业务单据；该表记录可能被 CS 端删除。",
            });
        }

        var row = db.QuerySingle<OperLogDto>(
            @"SELECT 'BS' AS source, ID_KEY AS id, Opt_Time AS time, User_No AS userNo, User_Name AS userName,
                     Module AS module, Action AS action, Target AS target, Target_Key AS targetKey,
                     Before_Value AS beforeValue, After_Value AS afterValue, Result AS result,
                     Message AS message, IP AS ip, Trace_ID AS traceId
                FROM dbo.BS_OperLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("日志不存在");
        return Api.Ok(new { row, note = "BS 记录含完整前后值与请求链路，可用于精确追溯。" });
    }

    /// <summary>GET api/system/logs/oper/diff?id=&source=：前后值差异。</summary>
    [HttpGet("diff")]
    [RequirePermission("system:log:oper")]
    public IActionResult Diff([FromQuery] long id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<OperLogDto>(
            @"SELECT 'BS' AS source, ID_KEY AS id, Before_Value AS beforeValue, After_Value AS afterValue, Trace_ID AS traceId
                FROM dbo.BS_OperLog WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("日志不存在");

        return Api.Ok(new
        {
            id,
            before = ParseJson(row.beforeValue),
            after = ParseJson(row.afterValue),
            traceId = row.traceId,
        });
    }

    /// <summary>GET api/system/logs/oper/export</summary>
    [HttpGet("export")]
    [RequirePermission("system:log:oper:export")]
    public IActionResult Export([FromQuery] DateTime? begin, [FromQuery] DateTime? end,
        [FromQuery] string? module, [FromQuery] string? source)
    {
        var (from, to) = QueryRange.Normalize(begin, end, 7, MaxSpanDays, out var error);
        if (error != null) return Api.BadRequest(error);

        using var db = SqlAccess.Open();
        var rows = Query(db, from, to, source);
        if (!string.IsNullOrWhiteSpace(module)) rows = rows.Where(r => r.module == module).ToList();

        var limit = SysConfigStore.GetInt("Export.MaxRows", 50000);
        var truncated = rows.Count > limit;
        var sb = new StringBuilder();
        sb.AppendLine("时间,账号,姓名,来源,模块,动作,目标,结果,IP,说明");
        foreach (var row in rows.Take(limit))
        {
            sb.AppendLine(string.Join(",",
                row.time.ToString("yyyy-MM-dd HH:mm:ss"), Escape(row.userNo), Escape(row.userName), row.source,
                Escape(row.module), Escape(row.action), Escape(row.targetKey ?? row.target),
                row.result == 0 ? "成功" : "失败", Escape(row.ip), Escape(row.message)));
        }
        ConfigAudit.Write(HttpContext, "log", "export", "operLog", null, null, new { rowCount = Math.Min(rows.Count, limit) });
        return Api.Ok(new
        {
            fileName = $"oper-log-{DateTime.Now:yyyyMMddHHmmss}.csv",
            content = sb.ToString(),
            rowCount = Math.Min(rows.Count, limit),
            truncated,
        });
    }

    private static List<OperLogDto> Query(SqlSession db, DateTime from, DateTime to, string? source)
    {
        var rows = new List<OperLogDto>();
        var wantCs = string.IsNullOrWhiteSpace(source) || string.Equals(source, "CS", StringComparison.OrdinalIgnoreCase);
        var wantBs = string.IsNullOrWhiteSpace(source) || string.Equals(source, "BS", StringComparison.OrdinalIgnoreCase);

        if (wantCs)
        {
            rows.AddRange(db.Query<OperLogDto>(
                @"SELECT 'CS' AS source, ID_KEY AS id, Opt_Time AS time, NULL AS userNo, User_Name AS userName,
                         System_No AS module, Opt_Type AS action, Opt_Table AS target, NULL AS targetKey,
                         CAST(0 AS TINYINT) AS result, Opt_Info AS message, Opt_Computer AS ip, NULL AS traceId,
                         NULL AS beforeValue, NULL AS afterValue
                    FROM dbo.ST_OptLog WHERE Opt_Time >= @b AND Opt_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        if (wantBs)
        {
            rows.AddRange(db.Query<OperLogDto>(
                @"SELECT 'BS' AS source, ID_KEY AS id, Opt_Time AS time, User_No AS userNo, User_Name AS userName,
                         Module AS module, Action AS action, Target AS target, Target_Key AS targetKey,
                         Result AS result, Message AS message, IP AS ip, Trace_ID AS traceId,
                         Before_Value AS beforeValue, After_Value AS afterValue
                    FROM dbo.BS_OperLog WHERE Opt_Time >= @b AND Opt_Time < @e",
                SqlSession.P("@b", from), SqlSession.P("@e", to)));
        }
        return rows.OrderByDescending(r => r.time).ThenByDescending(r => r.id).ToList();
    }

    private static object? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return json;
        }
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}

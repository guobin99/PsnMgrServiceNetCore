using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 数据维护：备份、整理、清理与结算、上传下载。
/// 高危作业（清理/结算）必须维护模式 + 已完成备份 + 确认短语，且无 CS 活跃会话。
/// </summary>
[Route("api/system/maintenance")]
[ApiController]
public class MaintenanceController : ControllerBase
{
    private const byte StatusPending = 0;
    private const byte StatusRunning = 1;
    private const byte StatusSuccess = 2;
    private const byte StatusFailed = 3;
    private const byte StatusCancelled = 4;

    /* ---------------- 作业 ---------------- */

    /// <summary>GET api/system/maintenance/jobs</summary>
    [HttpGet("jobs")]
    [RequirePermission("system:maintain:view")]
    public IActionResult Jobs([FromQuery] string? jobType, [FromQuery] int? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        using var db = SqlAccess.Open();
        var sql = @"SELECT Job_ID AS jobId, Job_Type AS jobType, Job_Name AS jobName, Params_Json AS paramsJson,
                           Status AS status, Progress AS progress, Message AS message, Backup_Job_ID AS backupJobId,
                           Create_User AS createUser, Create_Time AS createTime, Start_Time AS startTime,
                           Finish_Time AS finishTime, Trace_ID AS traceId
                      FROM dbo.BS_MaintJob WHERE 1 = 1";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(jobType))
        {
            sql += " AND Job_Type = @t";
            parameters.Add(SqlSession.P("@t", jobType.Trim()));
        }
        if (status.HasValue)
        {
            sql += " AND Status = @s";
            parameters.Add(SqlSession.P("@s", (byte)status.Value));
        }
        sql += " ORDER BY Job_ID DESC";

        var all = db.Query<MaintJobDto>(sql, parameters.ToArray());
        return Api.Ok(new { total = all.Count, items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList() });
    }

    /// <summary>GET api/system/maintenance/jobs/{id}：作业详情与步骤日志。</summary>
    [HttpGet("jobs/{id:int}")]
    [RequirePermission("system:maintain:view")]
    public IActionResult JobDetail(int id)
    {
        using var db = SqlAccess.Open();
        var job = db.QuerySingle<MaintJobDto>(
            @"SELECT Job_ID AS jobId, Job_Type AS jobType, Job_Name AS jobName, Params_Json AS paramsJson,
                     Status AS status, Progress AS progress, Message AS message, Backup_Job_ID AS backupJobId,
                     Create_User AS createUser, Create_Time AS createTime, Start_Time AS startTime,
                     Finish_Time AS finishTime, Trace_ID AS traceId
                FROM dbo.BS_MaintJob WHERE Job_ID = @id", SqlSession.P("@id", id));
        if (job == null) return Api.NotFound("作业不存在");

        var steps = db.Query<MaintStepDto>(
            @"SELECT ID_KEY AS idKey, Job_ID AS jobId, Step AS step, Message AS message, Affected AS affected, Log_Time AS logTime
                FROM dbo.BS_MaintLog WHERE Job_ID = @id ORDER BY ID_KEY", SqlSession.P("@id", id));
        return Api.Ok(new { job, steps });
    }

    /* ---------------- 维护模式 ---------------- */

    /// <summary>GET api/system/maintenance/mode</summary>
    [HttpGet("mode")]
    [RequirePermission("system:maintain:view")]
    public IActionResult GetMode()
    {
        using var db = SqlAccess.Open();
        var enabled = SysConfigStore.IsMaintenanceMode(db);
        var until = SysConfigStore.Get(db, SysConfigStore.MaintenanceUntilKey);
        var running = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_MaintJob WHERE Status = 1");
        var activeSessions = ActiveSessions(db);
        return Api.Ok(new { enabled, until, runningJobs = running, activeSessions, sessionBlocked = activeSessions > 0 });
    }

    /// <summary>PUT api/system/maintenance/mode：开启需无运行中作业且无 CS 活跃会话，默认两小时自动关闭。</summary>
    [HttpPut("mode")]
    [RequirePermission("system:maintain:execute")]
    public IActionResult SetMode([FromBody] MaintModeRequest request)
    {
        using var db = SqlAccess.Open();
        var running = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_MaintJob WHERE Status = 1");

        if (request?.Enabled == true)
        {
            if (running > 0) return Api.Fail(409, "40905", $"存在运行中的维护作业（{running} 个），请先等待完成");
            if (ActiveSessions(db) > 0)
                return Api.StateConflict("CS 端仍有 24 小时内的活跃会话，请先让相关人员退出后再开启维护模式");
            var hours = Math.Clamp(request.Hours is > 0 ? request.Hours!.Value : 2, 1, 12);
            var until = DateTime.Now.AddHours(hours);
            SysConfigStore.Set(db, SysConfigStore.MaintenanceModeKey, "1");
            SysConfigStore.Set(db, SysConfigStore.MaintenanceUntilKey, until.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            ConfigAudit.Write(HttpContext, "system", "maintenanceOn", "mode", null, new { enabled = false }, new { enabled = true, until });
            return Api.Ok(new { enabled = true, until = until.ToString("yyyy-MM-dd HH:mm:ss") }, "维护模式已开启，将自动关闭");
        }

        if (running > 0) return Api.Fail(409, "40905", "存在运行中的维护作业，暂不能关闭维护模式");
        SysConfigStore.Set(db, SysConfigStore.MaintenanceModeKey, "0", "维护模式：0 关闭、1 开启");
        SysConfigStore.Set(db, SysConfigStore.MaintenanceUntilKey, "");
        ConfigAudit.Write(HttpContext, "system", "maintenanceOff", "mode", null, new { enabled = true }, new { enabled = false });
        return Api.Ok(new { enabled = false }, "维护模式已关闭");
    }

    /* ---------------- 备份 ---------------- */

    /// <summary>POST api/system/maintenance/backups：创建并立即执行完整备份。</summary>
    [HttpPost("backups")]
    [RequirePermission("system:maintain:backup")]
    public IActionResult CreateBackup([FromBody] BackupRequest request)
    {
        using var db = SqlAccess.Open();
        var database = (db.Scalar("SELECT DB_NAME()") ?? "").ToString()!;
        var whitelist = (SysConfigStore.Get(db, "Backup.Path.Whitelist") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

        var kind = (request?.BackupType ?? "full").Trim().ToLowerInvariant();
        if (kind != "full" && kind != "diff") return Api.BadRequest("备份类型只支持 full 或 diff");

        var path = (request?.TargetPath ?? "").Trim().Replace('/', '\\');
        var defaultName = $"{database}_{DateTime.Now:yyyyMMdd_HHmmss}_{kind.ToUpperInvariant()}.bak";
        if (path.Length == 0)
        {
            var root = whitelist.FirstOrDefault() ?? Path.GetTempPath();
            path = Path.Combine(root, defaultName);
        }
        else
        {
            // 填入目录（已存在、以分隔符结尾或末段无扩展名）时补默认文件名，
            // 保证 BACKUP TO DISK 收到的始终是完整文件路径，避免把目录当文件导致 Msg 3201。
            var leaf = Path.GetFileName(path.TrimEnd('\\'));
            var looksLikeDir = Directory.Exists(path)
                               || path.EndsWith("\\", StringComparison.Ordinal)
                               || !leaf.Contains(".");
            if (looksLikeDir) path = Path.Combine(path, defaultName);
            var dir = Path.GetDirectoryName(path) ?? "";
            if (!Directory.Exists(dir)) return Api.BadRequest("备份目录不存在：" + dir);
        }

        var dbPath = path.Replace('/', '\\');
        if (!whitelist.Any(prefix => dbPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return Api.BadRequest("备份路径不在白名单内，请先在系统参数中配置 Backup.Path.Whitelist");

        var jobId = CreateJob(db, "backup", Path.GetFileName(path), new { path, kind, compress = request?.Compression ?? true });
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 1, Start_Time = GETDATE(), Progress = 5 WHERE Job_ID = @id", SqlSession.P("@id", jobId));

        var name = $"{database}_{DateTime.Now:yyyyMMdd_HHmmss}_{kind.ToUpperInvariant()}";
        var options = (request?.Compression ?? true) ? "COMPRESSION, " : "";
        if (request?.Verify == true) options += "CHECKSUM, ";
        var sql = kind == "diff"
            ? $"BACKUP DATABASE [{database}] TO DISK = @path WITH DIFFERENTIAL, {options}STATS = 25, NAME = @name"
            : $"BACKUP DATABASE [{database}] TO DISK = @path WITH {options}STATS = 25, NAME = @name";

        try
        {
            db.Exec(sql, SqlSession.P("@path", dbPath), SqlSession.P("@name", name));
            var size = System.IO.File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
            db.Exec("UPDATE dbo.BS_MaintJob SET Status = 2, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
                SqlSession.P("@m", $"备份成功，{size / 1024 / 1024} MB"), SqlSession.P("@id", jobId));
            WriteStep(db, jobId, "BACKUP", $"文件：{dbPath}，大小 {size / 1024 / 1024} MB", null);
            ConfigAudit.Write(HttpContext, "maintain", "backup", "database", database, null, new { jobId, path = dbPath, kind });
            return Api.Ok(new { jobId, path = dbPath, sizeBytes = size, backupType = kind }, "备份已完成");
        }
        catch (Exception ex)
        {
            db.Exec("UPDATE dbo.BS_MaintJob SET Status = 3, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
                SqlSession.P("@m", Truncate(ex.Message, 1000)), SqlSession.P("@id", jobId));
            WriteStep(db, jobId, "BACKUP", ex.Message, null);
            ConfigAudit.Write(HttpContext, "maintain", "backup", "database", database, null,
                new { jobId, path = dbPath, error = ex.Message }, false, ex.Message);
            return Api.Fail(500, "50002", "备份失败：" + ex.Message);
        }
    }

    /// <summary>GET api/system/maintenance/backups：备份历史（备份类作业）。</summary>
    [HttpGet("backups")]
    [RequirePermission("system:maintain:view")]
    public IActionResult Backups([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        using var db = SqlAccess.Open();
        var all = db.Query<MaintJobDto>(
            @"SELECT Job_ID AS jobId, Job_Type AS jobType, Job_Name AS jobName, Params_Json AS paramsJson,
                     Status AS status, Progress AS progress, Message AS message, Create_User AS createUser,
                     Create_Time AS createTime, Start_Time AS startTime, Finish_Time AS finishTime
                FROM dbo.BS_MaintJob WHERE Job_Type = 'backup' ORDER BY Job_ID DESC");
        return Api.Ok(new { total = all.Count, items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList() });
    }

    /* ---------------- 整理 ---------------- */

    /// <summary>GET api/system/maintenance/fragmentation：全部索引碎片率；tableName 支持模糊匹配（含 schema 前缀）。</summary>
    [HttpGet("fragmentation")]
    [RequirePermission("system:maintain:view")]
    public IActionResult Fragmentation([FromQuery] string? tableName)
    {
        using var db = SqlAccess.Open();
        // 不再用 TOP 500 / 碎片阈值裁剪：否则小碎片表、低页数表以及筛选结果都会消失。
        // 返回当前库全部用户索引（heap 无索引，不参与重建），由前端按需筛选/排序。
        var sql = @"SELECT s.name AS schemaName, t.name AS tableName, i.name AS indexName,
                           CAST(ps.avg_fragmentation_in_percent AS FLOAT) AS avgFragmentation, ps.page_count AS pageCount
                      FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') ps
                      JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                      JOIN sys.tables t ON t.object_id = ps.object_id
                      JOIN sys.schemas s ON s.schema_id = t.schema_id
                     WHERE i.index_id > 0";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            // 模糊匹配并转义 LIKE 通配符，保证 ST_Person 之类的下划线按字面匹配。
            var like = "%" + tableName.Trim().Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
            sql += " AND (t.name LIKE @t ESCAPE '!' OR s.name + '.' + t.name LIKE @t ESCAPE '!')";
            parameters.Add(SqlSession.P("@t", like));
        }
        sql += " ORDER BY ps.avg_fragmentation_in_percent DESC, t.name, i.name";
        return Api.Ok(db.Query<FragmentationDto>(sql, parameters.ToArray()));
    }

    /// <summary>POST api/system/maintenance/rebuild：重建指定索引。</summary>
    [HttpPost("rebuild")]
    [RequirePermission("system:maintain:rebuild")]
    public IActionResult Rebuild([FromBody] MaintObjectRequest request)
    {
        var targets = request?.Items ?? new List<MaintObject>();
        if (targets.Count == 0) return Api.BadRequest("请选择要重建的索引");

        using var db = SqlAccess.Open();
        var jobId = CreateJob(db, "rebuild", $"索引重建 {targets.Count} 项", new { items = targets });
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 1, Start_Time = GETDATE() WHERE Job_ID = @id", SqlSession.P("@id", jobId));

        var succeeded = 0;
        var failed = 0;
        var messages = new List<string>();
        var index = 0;
        foreach (var item in targets)
        {
            index++;
            var table = Sanitize(item.TableName);
            var indexName = Sanitize(item.IndexName);
            if (table.Length == 0 || indexName.Length == 0) { failed++; continue; }
            try
            {
                var started = DateTime.Now;
                db.Exec($"ALTER INDEX [{indexName}] ON [dbo].[{table}] REBUILD");
                var elapsed = (int)(DateTime.Now - started).TotalMilliseconds;
                WriteStep(db, jobId, $"REBUILD {table}.{indexName}", $"耗时 {elapsed} ms", null);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"{table}.{indexName}：{ex.Message}");
                WriteStep(db, jobId, $"REBUILD {table}.{indexName}", ex.Message, null);
            }
            db.Exec("UPDATE dbo.BS_MaintJob SET Progress = @p WHERE Job_ID = @id",
                SqlSession.P("@p", (int)(index * 100.0 / targets.Count)), SqlSession.P("@id", jobId));
        }

        var status = failed == 0 ? StatusSuccess : (succeeded > 0 ? StatusSuccess : StatusFailed);
        var message = $"成功 {succeeded} 项，失败 {failed} 项";
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = @s, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
            SqlSession.P("@s", status), SqlSession.P("@m", message), SqlSession.P("@id", jobId));
        ConfigAudit.Write(HttpContext, "maintain", "rebuild", "index", null, null, new { jobId, succeeded, failed });
        return Api.Ok(new { jobId, succeeded, failed, messages }, message);
    }

    /// <summary>POST api/system/maintenance/stats：更新统计信息。</summary>
    [HttpPost("stats")]
    [RequirePermission("system:maintain:rebuild")]
    public IActionResult UpdateStats([FromBody] MaintObjectRequest request)
    {
        var tables = (request?.Tables ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        if (tables.Count == 0) return Api.BadRequest("请选择要更新统计信息的表");

        using var db = SqlAccess.Open();
        var jobId = CreateJob(db, "stats", $"更新统计信息 {tables.Count} 张表", new { tables });
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 1, Start_Time = GETDATE() WHERE Job_ID = @id", SqlSession.P("@id", jobId));

        var succeeded = 0;
        var failed = 0;
        foreach (var table in tables)
        {
            var name = Sanitize(table);
            if (name.Length == 0) { failed++; continue; }
            try
            {
                db.Exec($"UPDATE STATISTICS [dbo].[{name}]");
                WriteStep(db, jobId, $"STATS {name}", "已更新", null);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                WriteStep(db, jobId, $"STATS {name}", ex.Message, null);
            }
        }

        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 2, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
            SqlSession.P("@m", $"成功 {succeeded} 张，失败 {failed} 张"), SqlSession.P("@id", jobId));
        ConfigAudit.Write(HttpContext, "maintain", "stats", "table", null, null, new { jobId, succeeded, failed });
        return Api.Ok(new { jobId, succeeded, failed }, $"成功 {succeeded} 张，失败 {failed} 张");
    }

    /* ---------------- 清理与结算 ---------------- */

    /// <summary>GET api/system/maintenance/cleanup/preview：只统计不写库。</summary>
    [HttpGet("cleanup/preview")]
    [RequirePermission("system:maintain:view")]
    public IActionResult CleanupPreview([FromQuery] string? cutoffDate)
    {
        if (!DateTime.TryParse(cutoffDate, out var cutoff))
            return Api.BadRequest("请指定截止日期（yyyy-MM-dd）");

        using var db = SqlAccess.Open();
        var tables = LoadClearTables(db);
        var rows = new List<object>();
        var totalRows = 0;
        foreach (var table in tables)
        {
            var tableName = Sanitize(table.tableName);
            var dateField = Sanitize(table.dateField);
            if (tableName.Length == 0 || dateField.Length == 0) continue;
            var exists = db.ScalarInt("SELECT CASE WHEN OBJECT_ID(@t,'U') IS NULL THEN 0 ELSE 1 END",
                SqlSession.P("@t", "dbo." + tableName)) == 1;
            var count = exists
                ? db.ScalarInt($"SELECT COUNT(1) FROM [dbo].[{tableName}] WHERE [{dateField}] < @cutoff", SqlSession.P("@cutoff", cutoff))
                : 0;
            totalRows += count;
            rows.Add(new
            {
                tableName,
                dateField,
                rows = count,
                historyTable = tableName + "History",
                historyExists = db.ScalarInt("SELECT CASE WHEN OBJECT_ID(@t,'U') IS NULL THEN 0 ELSE 1 END",
                    SqlSession.P("@t", "dbo." + tableName + "History")) == 1,
            });
        }

        var affectedPersons = db.ScalarInt(
            "SELECT COUNT(DISTINCT Person_ID) FROM dbo.XF_Consume WHERE Consume_Time < @cutoff", SqlSession.P("@cutoff", cutoff));

        return Api.Ok(new
        {
            cutoffDate = cutoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            tables = rows,
            totalRows,
            affectedPersons,
            activeSessions = ActiveSessions(db),
            maintenanceMode = SysConfigStore.IsMaintenanceMode(db),
            confirmPhrase = $"确认清理至 {cutoff:yyyy-MM-dd}",
        });
    }

    /// <summary>POST api/system/maintenance/cleanup：事务内按 Delete_Index 顺序结转并删除。</summary>
    [HttpPost("cleanup")]
    [RequirePermission("system:maintain:cleanup")]
    public IActionResult Cleanup([FromBody] CleanupRequest request)
    {
        if (!DateTime.TryParse(request?.CutoffDate, out var cutoff))
            return Api.BadRequest("请指定截止日期（yyyy-MM-dd）");
        var phrase = $"确认清理至 {cutoff:yyyy-MM-dd}";
        if (!string.Equals(request!.ConfirmPhrase?.Trim(), phrase, StringComparison.Ordinal))
            return Api.BadRequest($"确认短语不正确，应输入：{phrase}");
        if (request.BackupDone != true) return Api.BadRequest("执行清理前必须完成数据库备份");
        if (request.CsExited != true) return Api.BadRequest("请确认 CS 端已全部退出");

        using var db = SqlAccess.Open();
        if (!SysConfigStore.IsMaintenanceMode(db)) return Api.Fail(409, "40904", "请先开启维护模式");

        var running = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_MaintJob WHERE Status = 1");
        if (running > 0) return Api.Fail(409, "40905", "存在运行中的维护作业");
        if (ActiveSessions(db) > 0) return Api.StateConflict("CS 端仍有 24 小时内的活跃会话，请先退出");

        var tables = LoadClearTables(db);
        var jobId = CreateJob(db, "cleanup", $"数据清理至 {cutoff:yyyy-MM-dd}",
            new { cutoff = cutoff.ToString("yyyy-MM-dd"), phrase });
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 1, Start_Time = GETDATE() WHERE Job_ID = @id", SqlSession.P("@id", jobId));

        var details = new List<object>();
        db.Begin();
        try
        {
            var index = 0;
            foreach (var table in tables)
            {
                index++;
                var tableName = Sanitize(table.tableName);
                var dateField = Sanitize(table.dateField);
                var history = tableName + "History";
                if (tableName.Length == 0 || dateField.Length == 0) continue;

                var exists = db.ScalarInt("SELECT CASE WHEN OBJECT_ID(@t,'U') IS NULL THEN 0 ELSE 1 END",
                    SqlSession.P("@t", "dbo." + tableName)) == 1;
                if (!exists) continue;
                var historyExists = db.ScalarInt("SELECT CASE WHEN OBJECT_ID(@t,'U') IS NULL THEN 0 ELSE 1 END",
                    SqlSession.P("@t", "dbo." + history)) == 1;
                if (!historyExists)
                    throw new InvalidOperationException($"缺少历史表 {history}，已整体回滚");

                var copied = db.Exec(
                    $"INSERT INTO [dbo].[{history}] SELECT * FROM [dbo].[{tableName}] WHERE [{dateField}] < @cutoff",
                    SqlSession.P("@cutoff", cutoff));
                var deleted = db.Exec(
                    $"DELETE FROM [dbo].[{tableName}] WHERE [{dateField}] < @cutoff",
                    SqlSession.P("@cutoff", cutoff));
                WriteStep(db, jobId, tableName, $"结转 {copied} 行、删除 {deleted} 行", deleted);
                details.Add(new { tableName, copied, deleted });
                db.Exec("UPDATE dbo.BS_MaintJob SET Progress = @p WHERE Job_ID = @id",
                    SqlSession.P("@p", (int)(index * 100.0 / Math.Max(1, tables.Count))), SqlSession.P("@id", jobId));
            }
            db.Exec("UPDATE dbo.BS_MaintJob SET Status = 2, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
                SqlSession.P("@m", $"清理完成，共 {details.Count} 张表"), SqlSession.P("@id", jobId));
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            db.Exec("UPDATE dbo.BS_MaintJob SET Status = 3, Progress = 100, Finish_Time = GETDATE(), Message = @m WHERE Job_ID = @id",
                SqlSession.P("@m", Truncate(ex.Message, 1000)), SqlSession.P("@id", jobId));
            WriteStep(db, jobId, "ROLLBACK", ex.Message, null);
            ConfigAudit.Write(HttpContext, "maintain", "cleanup", "database", null, null,
                new { jobId, error = ex.Message }, false, ex.Message);
            return Api.Fail(500, "50002", "清理失败，已整体回滚：" + ex.Message);
        }

        ConfigAudit.Write(HttpContext, "maintain", "cleanup", "database", null,
            new { cutoff = cutoff.ToString("yyyy-MM-dd") }, new { jobId, tables = details });
        return Api.Ok(new { jobId, tables = details }, "清理已完成");
    }

    /// <summary>POST api/system/maintenance/settle：结算任务交由 CS 结算程序执行，BS 只建单与留痕。</summary>
    [HttpPost("settle")]
    [RequirePermission("system:maintain:execute")]
    public IActionResult Settle([FromBody] CleanupRequest request)
    {
        if (!DateTime.TryParse(request?.CutoffDate, out var cutoff))
            return Api.BadRequest("请指定结算截止日期");

        using var db = SqlAccess.Open();
        if (!SysConfigStore.IsMaintenanceMode(db)) return Api.Fail(409, "40904", "请先开启维护模式");
        if (request!.BackupDone != true) return Api.BadRequest("执行结算前必须完成数据库备份");

        var jobId = CreateJob(db, "settle", $"财务结算至 {cutoff:yyyy-MM-dd}",
            new { cutoff = cutoff.ToString("yyyy-MM-dd"), note = "结算计算口径复用 CS uFinancialSettle" });
        db.Exec(@"UPDATE dbo.BS_MaintJob SET Status = 0, Finish_Time = GETDATE(),
                         Message = N'已创建结算任务，请在 CS 结算程序执行，执行完成后回填结果'
                   WHERE Job_ID = @id", SqlSession.P("@id", jobId));
        WriteStep(db, jobId, "CREATE", "结算任务已创建，等待 CS 结算程序执行", null);
        ConfigAudit.Write(HttpContext, "maintain", "settle", "database", null, null, new { jobId, cutoff = cutoff.ToString("yyyy-MM-dd") });
        return Api.Ok(new { jobId, status = 0 }, "结算任务已创建，请在 CS 结算程序执行");
    }

    /* ---------------- 上传下载 ---------------- */

    /// <summary>POST api/system/maintenance/transfer：创建上传/下载任务（由调度器或 5501 服务执行）。</summary>
    [HttpPost("transfer")]
    [RequirePermission("system:maintain:execute")]
    public IActionResult Transfer([FromBody] TransferRequest request)
    {
        var direction = (request?.Direction ?? "down").Trim().ToLowerInvariant();
        if (direction != "up" && direction != "down") return Api.BadRequest("方向只支持 up 或 down");
        var taskCode = (request?.TaskCode ?? "").Trim();
        if (taskCode.Length == 0) return Api.BadRequest("请选择同步任务");

        using var db = SqlAccess.Open();
        var task = db.QuerySingle<SyncTaskDto>(
            "SELECT Task_ID AS taskId, Task_Code AS taskCode, Task_Type AS taskType FROM dbo.BS_SyncTask WHERE Task_Code = @c",
            SqlSession.P("@c", taskCode));
        if (task == null) return Api.NotFound("同步任务不存在");

        var running = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_SyncLog WHERE Task_Code = @c AND Status = 0", SqlSession.P("@c", taskCode));
        if (running > 0) return Api.Fail(409, "40905", "该任务正在执行中，请稍后再试");

        // 先入队拿到同步记录号，再建作业并把 syncLogId 写进参数，执行器据此回写作业进度
        var syncId = Convert.ToInt64(db.Scalar(
            @"INSERT INTO dbo.BS_SyncLog (Task_Code, Sync_Type, Direction, Target, Target_Key, Status, Message, Trace_ID, Create_Time)
              OUTPUT INSERTED.ID_KEY
              VALUES (@code, @type, @dir, @target, @key, 0, N'已提交，等待执行', @trace, GETDATE())",
            SqlSession.P("@code", taskCode), SqlSession.P("@type", task.taskType), SqlSession.P("@dir", direction),
            SqlSession.P("@target", request.Target), SqlSession.P("@key", request.TargetKey),
            SqlSession.P("@trace", HttpContext.TraceIdentifier)));

        var jobId = CreateJob(db, "transfer", $"{taskCode} {(direction == "up" ? "上传" : "下载")}",
            new { taskCode, direction, target = request!.TargetKey, syncLogId = syncId });
        db.Exec("UPDATE dbo.BS_MaintJob SET Status = 0, Message = N'任务已提交' WHERE Job_ID = @id", SqlSession.P("@id", jobId));

        ConfigAudit.Write(HttpContext, "maintain", "transfer", "syncTask", taskCode, null, new { jobId, direction });
        return Api.Ok(new { jobId, direction, taskCode }, "任务已提交");
    }

    /* ---------------- 内部 ---------------- */

    public sealed class ClearTableRow
    {
        public string tableName { get; set; } = "";
        public string dateField { get; set; } = "";
        public int deleteIndex { get; set; }
    }

    private int CreateJob(SqlSession db, string jobType, string jobName, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        db.Exec(
            @"INSERT INTO dbo.BS_MaintJob (Job_Type, Job_Name, Params_Json, Status, Progress, Create_User, Create_Time, Trace_ID)
              VALUES (@type, @name, @json, 0, 0, @user, GETDATE(), @trace)",
            SqlSession.P("@type", jobType), SqlSession.P("@name", Truncate(jobName, 100)),
            SqlSession.P("@json", json), SqlSession.P("@user", HttpContext.User.GetUserNo()),
            SqlSession.P("@trace", Guid.NewGuid().ToString("N")));
        return Convert.ToInt32(db.Scalar("SELECT MAX(Job_ID) FROM dbo.BS_MaintJob"));
    }

    private void WriteStep(SqlSession db, int jobId, string step, string message, int? affected)
    {
        db.Exec(
            "INSERT INTO dbo.BS_MaintLog (Job_ID, Step, Message, Affected, Log_Time) VALUES (@id, @step, @msg, @affected, GETDATE())",
            SqlSession.P("@id", jobId), SqlSession.P("@step", Truncate(step, 100)),
            SqlSession.P("@msg", Truncate(message, 1000)), SqlSession.P("@affected", (object?)affected));
    }

    private static List<ClearTableRow> LoadClearTables(SqlSession db)
    {
        return db.Query<ClearTableRow>(
            @"SELECT Table_Name AS tableName, Date_Field AS dateField, ISNULL(Delete_Index, 0) AS deleteIndex
                FROM dbo.ST_ClearTable ORDER BY ISNULL(Delete_Index, 0)");
    }

    private static int ActiveSessions(SqlSession db)
    {
        return db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_UserInfo WHERE Is_Using = 1 AND Login_Time > DATEADD(hour, -24, GETDATE())");
    }

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }

    private static string Truncate(string? value, int max)
    {
        if (value == null) return "";
        return value.Length <= max ? value : value[..max];
    }
}

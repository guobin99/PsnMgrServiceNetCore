using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 系统参数：九类作用域的参数查询、保存、恢复默认、备份、导入导出。
/// CS 作用域只允许改值，不允许新增或删除参数行；BS 作用域写入 BS_SysConfig。
/// </summary>
[Route("api/system/params")]
[ApiController]
public class ParamsController : ControllerBase
{

    /* ---------------- 查询 ---------------- */

    /// <summary>GET api/system/params</summary>
    [HttpGet]
    [RequirePermission("system:param:view")]
    public IActionResult List([FromQuery] string? scope, [FromQuery] string? groupName, [FromQuery] string? keyword)
    {
        var def = ConfigScopes.Find(scope);
        if (def == null) return Api.BadRequest("未知的参数作用域");

        using var db = SqlAccess.Open();
        var (items, warning) = LoadScope(db, def);

        if (!string.IsNullOrWhiteSpace(groupName))
            items = items.Where(i => string.Equals(i.groupName, groupName, StringComparison.Ordinal)).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            items = items.Where(i =>
                i.code.Contains(key, StringComparison.OrdinalIgnoreCase)
                || (i.name ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var payload = new
        {
            scope = def.Scope,
            scopeName = def.Name,
            items = items.Select(i => new
            {
                i.code,
                i.name,
                i.value,
                i.defaultValue,
                i.type,
                i.options,
                i.minValue,
                i.maxValue,
                i.groupName,
                i.restartRequired,
                i.readOnly,
                i.remark,
                i.updatedTime,
                modified = !string.IsNullOrEmpty(i.defaultValue) && !string.Equals(i.value, i.defaultValue, StringComparison.Ordinal),
            }).ToList(),
            warning,
        };
        return Api.Ok(payload);
    }

    /// <summary>GET api/system/params/groups：左栏作用域与分组树。</summary>
    [HttpGet("groups")]
    [RequirePermission("system:param:view")]
    public IActionResult Groups()
    {
        using var db = SqlAccess.Open();
        var nodes = new List<ParamScopeNodeDto>();
        foreach (var def in ConfigScopes.List)
        {
            var (items, _) = LoadScope(db, def);
            var groups = items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.groupName) ? "未分组" : i.groupName!)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new ParamGroupNodeDto { groupName = g.Key, count = g.Count() })
                .ToList();
            nodes.Add(new ParamScopeNodeDto { scope = def.Scope, scopeName = def.Name, groups = groups });
        }
        return Api.Ok(nodes);
    }

    /* ---------------- 保存 ---------------- */

    /// <summary>PUT api/system/params：逐行条件更新，实现乐观并发。</summary>
    [HttpPut]
    [RequirePermission("system:param:update")]
    public IActionResult Save([FromBody] ParamSaveRequest request)
    {
        var def = ConfigScopes.Find(request?.Scope);
        if (def == null) return Api.BadRequest("未知的参数作用域");
        if (request!.Items.Count == 0) return Api.BadRequest("没有需要保存的参数");

        using var db = SqlAccess.Open();
        if (SysConfigStore.IsMaintenanceMode(db) && !def.IsBsConfig)
            return Api.Fail(409, "40904", "系统处于维护模式，暂停参数修改");

        var (items, warning) = LoadScope(db, def);
        var map = items.ToDictionary(i => i.code, StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var pending = new List<(ParamItemDto item, string oldValue, string newValue)>();

        foreach (var row in request.Items)
        {
            var code = (row.ParamCode ?? "").Trim();
            if (code.Length == 0) continue;
            if (!map.TryGetValue(code, out var item))
            {
                errors.Add($"参数 {code} 不存在于作用域 {def.Scope}");
                continue;
            }
            if (item.readOnly)
            {
                errors.Add($"参数 {code} 为只读，不允许修改");
                continue;
            }
            var newValue = (row.Value ?? "").Trim();
            var current = item.value ?? "";
            if (!string.IsNullOrEmpty(row.OldValue) && !string.Equals(row.OldValue, current, StringComparison.Ordinal))
            {
                return Api.Fail(409, "40903", $"参数 {code} 已被其他用户修改，请刷新后重试");
            }
            if (string.Equals(current, newValue, StringComparison.Ordinal)) continue;

            var error = ParamRules.Validate(code, newValue, item.type, item.options, item.minValue, item.maxValue);
            if (error != null)
            {
                errors.Add(error);
                continue;
            }
            pending.Add((item, current, newValue));
        }

        if (errors.Count > 0)
            return Api.Fail(400, "40002", string.Join("；", errors.Take(5)));

        // 跨参数校验：清理日期不得晚于结算日期
        var crossError = CheckCrossRules(map, pending);
        if (crossError != null) return Api.Fail(400, "40002", crossError);

        var needRestart = new List<string>();
        var savedCount = 0;
        db.Begin();
        try
        {
            foreach (var (item, oldValue, newValue) in pending)
            {
                var affected = UpdateValue(db, def, item.code, newValue, oldValue);
                if (affected == 0)
                {
                    db.Rollback();
                    return Api.Fail(409, "40903", $"参数 {item.code} 已被其他用户修改，请刷新后重试");
                }
                if (item.restartRequired) needRestart.Add(item.code);
                savedCount++;
            }

            var version = SysConfigStore.BumpConfigVersion(db);
            db.Commit();

            ConfigAudit.Write(HttpContext, "param", "update", "param", def.Scope,
                pending.ToDictionary(p => p.item.code, p => p.oldValue),
                pending.ToDictionary(p => p.item.code, p => p.newValue));

            var message = needRestart.Count > 0
                ? "已保存；以下参数需要重启相关服务后生效：" + string.Join("、", needRestart)
                : "已保存；CS 端需重新进入子系统或重启后生效";
            return Api.Ok(new { savedCount, skippedCount = request.Items.Count - savedCount, needRestart, version, warning }, message);
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>POST api/system/params/reset：按元数据默认值恢复。</summary>
    [HttpPost("reset")]
    [RequirePermission("system:param:restore")]
    public IActionResult Reset([FromBody] ParamResetRequest request)
    {
        var def = ConfigScopes.Find(request?.Scope);
        if (def == null) return Api.BadRequest("未知的参数作用域");

        using var db = SqlAccess.Open();
        var (items, _) = LoadScope(db, def);
        var codes = request!.Codes.Count > 0 ? request.Codes : items.Select(i => i.code).ToList();

        var changed = new List<(ParamItemDto item, string oldValue, string newValue)>();
        var skipped = new List<string>();
        foreach (var code in codes)
        {
            var item = items.FirstOrDefault(i => string.Equals(i.code, code, StringComparison.OrdinalIgnoreCase));
            if (item == null) { skipped.Add(code); continue; }
            if (string.IsNullOrEmpty(item.defaultValue)) { skipped.Add(code); continue; }
            if (string.Equals(item.value, item.defaultValue, StringComparison.Ordinal)) continue;
            changed.Add((item, item.value ?? "", item.defaultValue!));
        }

        if (changed.Count == 0)
            return Api.Ok(new { changedCount = 0, skippedCount = skipped.Count, needRestart = new List<string>() }, "没有需要恢复的参数");

        var needRestart = changed.Where(c => c.item.restartRequired).Select(c => c.item.code).ToList();
        db.Begin();
        try
        {
            foreach (var (item, oldValue, newValue) in changed)
                UpdateValue(db, def, item.code, newValue, oldValue);
            var version = SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "param", "reset", def.Scope, null,
                changed.ToDictionary(c => c.item.code, c => c.oldValue),
                changed.ToDictionary(c => c.item.code, c => c.newValue));
            return Api.Ok(new { changedCount = changed.Count, skippedCount = skipped.Count, needRestart, version },
                $"已恢复 {changed.Count} 项，跳过 {skipped.Count} 项");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /* ---------------- 备份 ---------------- */

    /// <summary>GET api/system/params/backups</summary>
    [HttpGet("backups")]
    [RequirePermission("system:param:view")]
    public IActionResult BackupList([FromQuery] string? scope)
    {
        using var db = SqlAccess.Open();
        var rows = string.IsNullOrWhiteSpace(scope)
            ? db.Query<ParamBackupDto>(
                @"SELECT Backup_ID AS backupId, Scope AS scope, Backup_Name AS backupName, Item_Count AS itemCount,
                         Hash AS hash, Create_User AS createUser, Create_Time AS createTime, Remark AS remark
                    FROM dbo.BS_ParamBackup ORDER BY Backup_ID DESC")
            : db.Query<ParamBackupDto>(
                @"SELECT Backup_ID AS backupId, Scope AS scope, Backup_Name AS backupName, Item_Count AS itemCount,
                         Hash AS hash, Create_User AS createUser, Create_Time AS createTime, Remark AS remark
                    FROM dbo.BS_ParamBackup WHERE Scope = @scope ORDER BY Backup_ID DESC",
                SqlSession.P("@scope", scope!.Trim().ToUpperInvariant()));
        return Api.Ok(rows);
    }

    /// <summary>POST api/system/params/backups：生成全作用域快照。</summary>
    [HttpPost("backups")]
    [RequirePermission("system:param:backup")]
    public IActionResult BackupCreate([FromBody] ParamBackupRequest request)
    {
        var def = ConfigScopes.Find(request?.Scope);
        if (def == null) return Api.BadRequest("未知的参数作用域");

        using var db = SqlAccess.Open();
        var (items, _) = LoadScope(db, def);
        var snapshot = items.Select(i => new { code = i.code, name = i.name, value = i.value }).ToList();
        var json = JsonSerializer.Serialize(snapshot, JsonOpts);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var name = string.IsNullOrWhiteSpace(request!.Name)
            ? $"{def.Scope}-{DateTime.Now:yyyyMMdd-HHmmss}"
            : request.Name!.Trim();

        db.Exec(
            @"INSERT INTO dbo.BS_ParamBackup (Scope, Backup_Name, Snapshot_Json, Item_Count, Hash, Create_User, Remark)
              VALUES (@scope, @name, @json, @count, @hash, @user, @remark)",
            SqlSession.P("@scope", def.Scope), SqlSession.P("@name", name), SqlSession.P("@json", json),
            SqlSession.P("@count", snapshot.Count), SqlSession.P("@hash", hash),
            SqlSession.P("@user", HttpContext.User.GetUserNo()), SqlSession.P("@remark", request.Remark));

        var id = Convert.ToInt32(db.Scalar("SELECT MAX(Backup_ID) FROM dbo.BS_ParamBackup"));
        ConfigAudit.Write(HttpContext, "param", "backup", def.Scope, null, null, new { backupId = id, itemCount = snapshot.Count });
        return Api.Ok(new { backupId = id, itemCount = snapshot.Count, hash }, "备份已完成");
    }

    /// <summary>POST api/system/params/backups/{id}/restore</summary>
    [HttpPost("backups/{id:int}/restore")]
    [RequirePermission("system:param:restore")]
    public IActionResult BackupRestore(int id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<ParamBackupSnapshotRow>(
            "SELECT Backup_ID AS backupId, Scope AS scope, Snapshot_Json AS snapshotJson FROM dbo.BS_ParamBackup WHERE Backup_ID = @id",
            SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("备份不存在");

        var def = ConfigScopes.Find(row.scope);
        if (def == null) return Api.BadRequest("备份的作用域无效");

        var snapshot = JsonSerializer.Deserialize<List<ParamSnapshotItem>>(row.snapshotJson) ?? new List<ParamSnapshotItem>();
        var (items, _) = LoadScope(db, def);
        var map = items.ToDictionary(i => i.code, StringComparer.OrdinalIgnoreCase);

        var changed = new List<(ParamItemDto item, string oldValue, string newValue)>();
        foreach (var entry in snapshot)
        {
            if (entry.code == null || entry.value == null) continue;
            if (!map.TryGetValue(entry.code, out var item)) continue;
            if (string.Equals(item.value, entry.value, StringComparison.Ordinal)) continue;
            var error = ParamRules.Validate(item.code, entry.value, item.type, item.options, item.minValue, item.maxValue);
            if (error != null) continue;
            changed.Add((item, item.value ?? "", entry.value));
        }

        db.Begin();
        try
        {
            foreach (var (item, oldValue, newValue) in changed)
                UpdateValue(db, def, item.code, newValue, oldValue);
            var version = SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "param", "restoreBackup", def.Scope, null, null, new { backupId = id, changedCount = changed.Count });
            return Api.Ok(new { changedCount = changed.Count, version }, $"已按备份恢复 {changed.Count} 项");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>DELETE api/system/params/backups/{id}</summary>
    [HttpDelete("backups/{id:int}")]
    [RequirePermission("system:param:backup")]
    public IActionResult BackupDelete(int id)
    {
        using var db = SqlAccess.Open();
        var affected = db.Exec("DELETE FROM dbo.BS_ParamBackup WHERE Backup_ID = @id", SqlSession.P("@id", id));
        if (affected == 0) return Api.NotFound("备份不存在");
        ConfigAudit.Write(HttpContext, "param", "deleteBackup", null, null, null, new { backupId = id });
        return Api.Ok(new { backupId = id });
    }

    /* ---------------- 导入导出 ---------------- */

    /// <summary>POST api/system/params/import/preview：差异预演，不写库。</summary>
    [HttpPost("import/preview")]
    [RequirePermission("system:param:import")]
    public IActionResult ImportPreview([FromBody] ParamImportRequest request)
    {
        var (result, error) = BuildImportPlan(request);
        if (error != null) return Api.BadRequest(error);
        return Api.Ok(result);
    }

    /// <summary>POST api/system/params/import：事务内应用差异。</summary>
    [HttpPost("import")]
    [RequirePermission("system:param:import")]
    public IActionResult Import([FromBody] ParamImportRequest request)
    {
        var (plan, error) = BuildImportPlan(request);
        if (error != null) return Api.BadRequest(error);
        if (plan == null) return Api.BadRequest("导入数据无效");
        if (plan.conflicts.Count > 0)
            return Api.Fail(400, "40003", "存在冲突项，请先处理：" + string.Join("；", plan.conflicts.Take(5)));

        var def = ConfigScopes.Find(request!.Scope)!;
        using var db = SqlAccess.Open();
        db.Begin();
        try
        {
            var saved = 0;
            foreach (var change in plan.changes)
            {
                if (change.isNew)
                {
                    db.Exec(
                        "INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Update_User) VALUES (@s, @c, @n, 'string', NULL, N'导入', @u)",
                        SqlSession.P("@s", def.Scope), SqlSession.P("@c", change.code), SqlSession.P("@n", change.code),
                        SqlSession.P("@u", HttpContext.User.GetUserNo()));
                    db.Exec("INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark, Update_Time) VALUES (@k, @v, N'参数导入', GETDATE())",
                        SqlSession.P("@k", change.code), SqlSession.P("@v", change.value));
                }
                else
                {
                    var affected = UpdateValue(db, def, change.code, change.value!, change.oldValue ?? "");
                    if (affected == 0) { db.Rollback(); return Api.Fail(409, "40903", $"参数 {change.code} 已被其他用户修改，请重新预演"); }
                }
                saved++;
            }
            var version = SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "param", "import", def.Scope, null, null, new { savedCount = saved, version });
            return Api.Ok(new { savedCount = saved, skippedCount = plan.unchanged.Count, version }, $"已导入 {saved} 项");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>GET api/system/params/export：导出当前作用域 JSON 快照。</summary>
    [HttpGet("export")]
    [RequirePermission("system:param:export")]
    public IActionResult Export([FromQuery] string? scope)
    {
        var def = ConfigScopes.Find(scope);
        if (def == null) return Api.BadRequest("未知的参数作用域");
        using var db = SqlAccess.Open();
        var (items, _) = LoadScope(db, def);
        var snapshot = items.Select(i => new { code = i.code, name = i.name, value = i.value, type = i.type }).ToList();
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        ConfigAudit.Write(HttpContext, "param", "export", def.Scope, null, null, new { itemCount = snapshot.Count });
        return Api.Ok(new { fileName = $"{def.Scope}-params-{DateTime.Now:yyyyMMddHHmmss}.json", content = json, itemCount = snapshot.Count });
    }

    /* ---------------- 内部实现 ---------------- */

    public sealed class ParamBackupSnapshotRow
    {
        public int backupId { get; set; }
        public string scope { get; set; } = "";
        public string snapshotJson { get; set; } = "";
    }

    public sealed class ImportChange
    {
        public string code { get; set; } = "";
        public string? value { get; set; }
        public string? oldValue { get; set; }
        public bool isNew { get; set; }
    }

    public sealed class ImportPlan
    {
        public List<ImportChange> changes { get; set; } = new();
        public List<string> unchanged { get; set; } = new();
        public List<string> conflicts { get; set; } = new();
        public int newCount => changes.Count(c => c.isNew);
        public int modifiedCount => changes.Count(c => !c.isNew);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>按作用域加载参数值并与元数据合并。</summary>
    private static (List<ParamItemDto> items, string? warning) LoadScope(SqlSession db, ConfigScopes.ScopeDef def)
    {
        var metas = db.Query<ParamItemDto>(
            @"SELECT Param_Code AS code, Param_Name AS name, Param_Type AS type, Param_Options AS options,
                     Default_Value AS defaultValue, Min_Value AS minValue, Max_Value AS maxValue,
                     Group_Name AS groupName, Is_ReadOnly AS readOnly, Restart_Required AS restartRequired,
                     Remark AS remark, Update_Time AS updatedTime
                FROM dbo.BS_ParamMeta WHERE Scope = @scope",
            SqlSession.P("@scope", def.Scope));

        var metaMap = metas.ToDictionary(m => m.code, StringComparer.OrdinalIgnoreCase);
        var items = new List<ParamItemDto>();

        if (def.IsBsConfig)
        {
            var rows = db.Query<ParamItemDto>(
                @"SELECT m.Param_Code AS code, m.Param_Name AS name, ISNULL(c.Config_Value, '') AS value
                    FROM dbo.BS_ParamMeta m
                    LEFT JOIN dbo.BS_SysConfig c ON c.Config_Key = m.Param_Code
                   WHERE m.Scope = 'BS'
                   ORDER BY m.Param_Code");
            foreach (var row in rows)
            {
                if (metaMap.TryGetValue(row.code, out var meta)) Merge(meta, row.value, null);
                else { row.type = "string"; items.Add(row); continue; }
                items.Add(meta);
            }
            return (items.OrderBy(i => i.code, StringComparer.Ordinal).ToList(), null);
        }

        var tableExists = db.ScalarInt("SELECT CASE WHEN OBJECT_ID(@t, 'U') IS NULL THEN 0 ELSE 1 END",
            SqlSession.P("@t", def.Table));
        if (tableExists == 0)
            return (items, $"当前数据库不存在参数表 {def.Table}，该作用域参数暂不可维护");

        var sql = $"SELECT Param_Code AS code, Param_Name AS name, Param_Value AS value FROM {def.Table}";
        if (!string.IsNullOrEmpty(def.CodeFilter))
            sql += " WHERE Param_Code LIKE @filter";
        sql += " ORDER BY Param_Code";

        var rows2 = string.IsNullOrEmpty(def.CodeFilter)
            ? db.Query<ParamItemDto>(sql)
            : db.Query<ParamItemDto>(sql, SqlSession.P("@filter", def.CodeFilter + "%"));

        foreach (var row in rows2)
        {
            if (metaMap.TryGetValue(row.code, out var meta))
            {
                Merge(meta, row.value, row.name);
                items.Add(meta);
            }
            else
            {
                row.type = GuessType(row.value);
                var codeName = ResolveCodeName(row.code);
                if (!string.IsNullOrWhiteSpace(codeName))
                {
                    row.name = codeName;
                    row.groupName = "授权与注册";
                }
                else
                {
                    row.groupName = "未分组";
                }
                items.Add(row);
            }
        }
        return (items, null);
    }

    private static void Merge(ParamItemDto meta, string? value, string? nameFromTable)
    {
        meta.value = value ?? "";
        if (string.IsNullOrWhiteSpace(meta.name) && !string.IsNullOrWhiteSpace(nameFromTable)) meta.name = nameFromTable;
        if (string.IsNullOrWhiteSpace(meta.name)) meta.name = ResolveCodeName(meta.code);
        if (string.IsNullOrWhiteSpace(meta.name)) meta.name = meta.code;
    }

    /// <summary>
    /// 「子系统代码+CODE」类注册码参数的兜底命名：以 _CODE 结尾时显示「XX子系统注册码」。
    /// 例如 KQ_CODE -> KQ子系统注册码、MJ_CODE -> MJ子系统注册码。
    /// </summary>
    private static string ResolveCodeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.EndsWith("_CODE", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        var prefix = code.Substring(0, code.Length - 5);
        if (prefix.Length == 0) return string.Empty;
        var def = ConfigScopes.Find(prefix);
        var subName = string.IsNullOrWhiteSpace(def?.Name) ? prefix : def!.Name;
        return subName + "注册码";
    }

    /// <summary>无元数据的参数按值形态推断类型，仅用于展示控件选择。</summary>
    private static string GuessType(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) return "string";
        if (text is "0" or "1") return "bool";
        if (long.TryParse(text, out _)) return "int";
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return "decimal";
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return "date";
        return "string";
    }

    /// <summary>条件更新：CS 作用域同时匹配旧值（乐观并发），BS 作用域写 BS_SysConfig。</summary>
    private static int UpdateValue(SqlSession db, ConfigScopes.ScopeDef def, string code, string newValue, string oldValue)
    {
        if (def.IsBsConfig)
        {
            var affected = db.Exec(
                "UPDATE dbo.BS_SysConfig SET Config_Value = @v, Update_Time = GETDATE() WHERE Config_Key = @k AND ISNULL(Config_Value, '') = @old",
                SqlSession.P("@v", newValue), SqlSession.P("@k", code), SqlSession.P("@old", oldValue));
            if (affected > 0) return affected;
            var exists = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_SysConfig WHERE Config_Key = @k", SqlSession.P("@k", code));
            if (exists == 0)
            {
                db.Exec("INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark, Update_Time) VALUES (@k, @v, N'BS参数', GETDATE())",
                    SqlSession.P("@k", code), SqlSession.P("@v", newValue));
                return 1;
            }
            return 0;
        }

        return db.Exec(
            $"UPDATE {def.Table} SET Param_Value = @v WHERE Param_Code = @c AND ISNULL(Param_Value, '') = @old",
            SqlSession.P("@v", newValue), SqlSession.P("@c", code), SqlSession.P("@old", oldValue));
    }

    /// <summary>跨参数校验：ClearDate 不得晚于 SettleDate。</summary>
    private static string? CheckCrossRules(Dictionary<string, ParamItemDto> map,
        List<(ParamItemDto item, string oldValue, string newValue)> pending)
    {
        string? ValueOf(string code)
        {
            var change = pending.FirstOrDefault(p => string.Equals(p.item.code, code, StringComparison.OrdinalIgnoreCase));
            if (change.item != null) return change.newValue;
            return map.TryGetValue(code, out var item) ? item.value : null;
        }

        var settle = ValueOf("SettleDate");
        var clear = ValueOf("ClearDate");
        if (DateTime.TryParse(settle, out var settleDate) && DateTime.TryParse(clear, out var clearDate)
            && clearDate.Date > settleDate.Date)
        {
            return "清理日期不得晚于结算日期";
        }
        return null;
    }

    private (ImportPlan? plan, string? error) BuildImportPlan(ParamImportRequest? request)
    {
        var def = ConfigScopes.Find(request?.Scope);
        if (def == null) return (null, "未知的参数作用域");
        if (request!.Items.Count == 0) return (null, "导入内容为空");

        using var db = SqlAccess.Open();
        var (items, _) = LoadScope(db, def);
        var map = items.ToDictionary(i => i.code, StringComparer.OrdinalIgnoreCase);
        var plan = new ImportPlan();

        foreach (var entry in request.Items)
        {
            var code = (entry.code ?? "").Trim();
            if (code.Length == 0) continue;
            var value = (entry.value ?? "").Trim();

            if (!map.TryGetValue(code, out var item))
            {
                if (!def.IsBsConfig)
                {
                    plan.conflicts.Add($"{code} 不存在（CS 作用域不允许新增参数编码）");
                    continue;
                }
                if (ParamRules.Validate(code, value, "string", null, null, null) is { } stringError)
                {
                    plan.conflicts.Add(stringError);
                    continue;
                }
                plan.changes.Add(new ImportChange { code = code, value = value, isNew = true });
                continue;
            }

            var error = ParamRules.Validate(code, value, item.type, item.options, item.minValue, item.maxValue);
            if (error != null) { plan.conflicts.Add(error); continue; }
            if (string.Equals(item.value, value, StringComparison.Ordinal)) { plan.unchanged.Add(code); continue; }
            plan.changes.Add(new ImportChange { code = code, value = value, oldValue = item.value, isNew = false });
        }

        return (plan, null);
    }
}

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 数据字典：编码字典（ST_Dictionary）、元数据（ST_Dict_*）与一致性检查。
/// ST_Dict_Table / ST_Dict_Field / ST_Dict_Column 只在 CS 升级脚本中出现，
/// 部分库缺失，缺失时元数据页签隐藏并提示执行升级脚本，绝不自动建表。
/// </summary>
[Route("api/system/dict")]
[ApiController]
public class DictController : ControllerBase
{
    /* ---------------- 编码字典 ---------------- */

    /// <summary>GET api/system/dict/tables：编码表清单。</summary>
    [HttpGet("tables")]
    [RequirePermission("system:dict:view")]
    public IActionResult Tables([FromQuery] string? keyword)
    {
        using var db = SqlAccess.Open();
        var sql = @"SELECT Table_Name AS tableName,
                           MAX(Table_ChsName) AS tableChsName,
                           COUNT(1) AS itemCount
                      FROM dbo.ST_Dictionary";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " WHERE Table_Name LIKE @key OR Table_ChsName LIKE @key";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        sql += " GROUP BY Table_Name ORDER BY MAX(Table_ChsName)";

        var rows = db.Query<DictTableDto>(sql, parameters.ToArray());
        return Api.Ok(rows);
    }

    /// <summary>GET api/system/dict/items：编码明细。</summary>
    [HttpGet("items")]
    [RequirePermission("system:dict:view")]
    public IActionResult Items([FromQuery] string tableName, [FromQuery] string? keyword,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return Api.BadRequest("请指定编码表");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        using var db = SqlAccess.Open();
        var sql = @"SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue,
                           Is_SysCol AS sysCol, Remark AS remark
                      FROM dbo.ST_Dictionary WHERE Table_Name = @table";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter> { SqlSession.P("@table", tableName.Trim()) };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " AND (Dict_Id LIKE @key OR Dict_Value LIKE @key OR Remark LIKE @key)";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        sql += " ORDER BY Dict_Id";

        var all = db.Query<DictItemDto>(sql, parameters.ToArray());
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Api.Ok(new { total = all.Count, items });
    }

    /// <summary>POST api/system/dict/items：新增编码，编码取同表最大值加一。</summary>
    [HttpPost("items")]
    [RequirePermission("system:dict:create")]
    public IActionResult Create([FromBody] DictItemSaveRequest request)
    {
        var tableName = (request?.TableName ?? "").Trim();
        var dictValue = (request?.Dict_Value ?? "").Trim();
        if (tableName.Length == 0) return Api.BadRequest("请指定编码表");
        if (dictValue.Length == 0) return Api.BadRequest("名称不能为空");
        if (dictValue.Length > 40) return Api.BadRequest("名称不得超过 40 个字符");

        using var db = SqlAccess.Open();
        var dictId = (request!.Dict_Id ?? "").Trim();
        if (dictId.Length == 0)
        {
            var max = db.Scalar(
                "SELECT MAX(CAST(Dict_Id AS INT)) FROM dbo.ST_Dictionary WHERE Table_Name = @t AND ISNUMERIC(Dict_Id) = 1",
                SqlSession.P("@t", tableName));
            if (max == null)
                return Api.Fail(422, "42202", "该编码表存在非数字编码，无法自动生成，请手工录入");
            dictId = (Convert.ToInt32(max, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        }
        if (!int.TryParse(dictId, out _)) return Api.Fail(400, "40003", "编码必须为数字");
        if (dictId.Length > 20) return Api.BadRequest("编码长度不得超过 20");

        var exists = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_Name = @t AND Dict_Id = @id",
            SqlSession.P("@t", tableName), SqlSession.P("@id", dictId));
        if (exists > 0) return Api.Fail(400, "40003", $"编码 {dictId} 在该编码表中已存在");

        db.Exec(
            @"INSERT INTO dbo.ST_Dictionary (Table_Name, Table_ChsName, Dict_Id, Dict_Value, Is_SysCol, Remark)
              SELECT @t, MAX(Table_ChsName), @id, @v, @sys, @remark FROM dbo.ST_Dictionary WHERE Table_Name = @t",
            SqlSession.P("@t", tableName), SqlSession.P("@id", dictId), SqlSession.P("@v", dictValue),
            SqlSession.P("@sys", request.Is_SysCol ?? false), SqlSession.P("@remark", request.Remark));

        var id = Convert.ToInt32(db.Scalar(
            "SELECT MAX(ID_KEY) FROM dbo.ST_Dictionary WHERE Table_Name = @t AND Dict_Id = @id",
            SqlSession.P("@t", tableName), SqlSession.P("@id", dictId)));
        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "dict", "create", "Dictionary", $"{tableName}.{dictId}", null,
            new { dictId, dictValue, sysCol = request.Is_SysCol ?? false });
        return Api.Ok(new { id, tableName, dictId, dictValue });
    }

    /// <summary>PUT api/system/dict/items/{id}：条件更新，防止并发覆盖。</summary>
    [HttpPut("items/{id:int}")]
    [RequirePermission("system:dict:update")]
    public IActionResult Update(int id, [FromBody] DictItemSaveRequest request)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<DictItemDto>(
            "SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark FROM dbo.ST_Dictionary WHERE ID_KEY = @id",
            SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("编码不存在");

        var dictId = (request?.Dict_Id ?? current.dictId ?? "").Trim();
        var dictValue = (request?.Dict_Value ?? current.dictValue ?? "").Trim();
        if (dictId.Length == 0) return Api.BadRequest("编码不能为空");
        if (!int.TryParse(dictId, out _)) return Api.Fail(400, "40003", "编码必须为数字");
        if (dictValue.Length == 0 || dictValue.Length > 40) return Api.BadRequest("名称必填且不超过 40 个字符");

        if (current.sysCol && !string.Equals(dictId, current.dictId, StringComparison.Ordinal))
            return Api.StateConflict("系统内置编码不可修改编码值");

        var duplicate = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_Name = @t AND Dict_Id = @id AND ID_KEY <> @key",
            SqlSession.P("@t", current.tableName), SqlSession.P("@id", dictId), SqlSession.P("@key", id));
        if (duplicate > 0) return Api.Fail(400, "40003", $"编码 {dictId} 在该编码表中已存在");

        var oldId = request?.OldDict_Id ?? current.dictId;
        var oldValue = request?.OldDict_Value ?? current.dictValue;
        var affected = db.Exec(
            @"UPDATE dbo.ST_Dictionary
                 SET Dict_Id = @id, Dict_Value = @v, Remark = @remark
               WHERE ID_KEY = @key AND Dict_Id = @oldId AND Dict_Value = @oldValue",
            SqlSession.P("@id", dictId), SqlSession.P("@v", dictValue), SqlSession.P("@remark", request?.Remark),
            SqlSession.P("@key", id), SqlSession.P("@oldId", oldId), SqlSession.P("@oldValue", oldValue));
        if (affected == 0) return Api.Fail(409, "40903", "该编码已被他人修改，请刷新后重试");

        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "dict", "update", "Dictionary", $"{current.tableName}.{dictId}",
            new { dictId = current.dictId, dictValue = current.dictValue, remark = current.remark },
            new { dictId, dictValue, remark = request?.Remark });
        return Api.Ok(new { id, dictId, dictValue });
    }

    /// <summary>DELETE api/system/dict/items/{id}：系统列禁止删除。</summary>
    [HttpDelete("items/{id:int}")]
    [RequirePermission("system:dict:delete")]
    public IActionResult Delete(int id)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<DictItemDto>(
            "SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark FROM dbo.ST_Dictionary WHERE ID_KEY = @id",
            SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("编码不存在");
        if (current.sysCol) return Api.StateConflict("系统内置编码不可删除");

        db.Exec("DELETE FROM dbo.ST_Dictionary WHERE ID_KEY = @id", SqlSession.P("@id", id));
        SysConfigStore.BumpConfigVersion(db);
        ConfigAudit.Write(HttpContext, "dict", "delete", "Dictionary", $"{current.tableName}.{current.dictId}",
            new { current.dictId, current.dictValue, current.remark }, null);
        return Api.Ok(new { id });
    }

    /// <summary>GET api/system/dict/items/export?tableName=：导出两列 CSV。</summary>
    [HttpGet("items/export")]
    [RequirePermission("system:dict:view")]
    public IActionResult Export([FromQuery] string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return Api.BadRequest("请指定编码表");
        using var db = SqlAccess.Open();
        var rows = db.Query<DictItemDto>(
            @"SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark
                FROM dbo.ST_Dictionary WHERE Table_Name = @t ORDER BY Dict_Id",
            SqlSession.P("@t", tableName.Trim()));

        var sb = new StringBuilder();
        sb.AppendLine("编码,名称,备注");
        foreach (var row in rows)
            sb.AppendLine($"{row.dictId},{Escape(row.dictValue)},{Escape(row.remark)}");

        ConfigAudit.Write(HttpContext, "dict", "export", "Dictionary", tableName.Trim(), null, new { itemCount = rows.Count });
        return Api.Ok(new { fileName = $"dict-{tableName.Trim()}-{DateTime.Now:yyyyMMddHHmmss}.csv", content = sb.ToString(), itemCount = rows.Count });
    }

    /// <summary>POST api/system/dict/items/import：按编码覆盖或跳过。</summary>
    [HttpPost("items/import")]
    [RequirePermission("system:dict:create")]
    public IActionResult Import([FromBody] DictImportRequest request)
    {
        var tableName = (request?.TableName ?? "").Trim();
        if (tableName.Length == 0) return Api.BadRequest("请指定编码表");
        if (request!.Items.Count == 0) return Api.BadRequest("导入内容为空");

        using var db = SqlAccess.Open();
        var existing = db.Query<DictItemDto>(
            "SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark FROM dbo.ST_Dictionary WHERE Table_Name = @t",
            SqlSession.P("@t", tableName));
        var map = existing.GroupBy(e => e.dictId ?? "").ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        db.Begin();
        try
        {
            foreach (var item in request.Items)
            {
                var dictId = (item.dictId ?? "").Trim();
                var dictValue = (item.dictValue ?? "").Trim();
                if (dictId.Length == 0 || dictValue.Length == 0) { skipped++; continue; }
                if (dictValue.Length > 40) { skipped++; continue; }

                if (map.TryGetValue(dictId, out var exists))
                {
                    if (!request.Overwrite) { skipped++; continue; }
                    db.Exec("UPDATE dbo.ST_Dictionary SET Dict_Value = @v WHERE ID_KEY = @id",
                        SqlSession.P("@v", dictValue), SqlSession.P("@id", exists.id));
                    updated++;
                }
                else
                {
                    db.Exec(
                        @"INSERT INTO dbo.ST_Dictionary (Table_Name, Table_ChsName, Dict_Id, Dict_Value, Is_SysCol, Remark)
                          SELECT @t, MAX(Table_ChsName), @id, @v, 0, NULL FROM dbo.ST_Dictionary WHERE Table_Name = @t",
                        SqlSession.P("@t", tableName), SqlSession.P("@id", dictId), SqlSession.P("@v", dictValue));
                    inserted++;
                }
            }
            SysConfigStore.BumpConfigVersion(db);
            db.Commit();
            ConfigAudit.Write(HttpContext, "dict", "import", "Dictionary", tableName, null,
                new { inserted, updated, skipped, overwrite = request.Overwrite });
            return Api.Ok(new { inserted, updated, skipped }, $"新增 {inserted} 条、覆盖 {updated} 条、跳过 {skipped} 条");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /* ---------------- 元数据 ---------------- */

    private static bool MetaAvailable(SqlSession db)
    {
        return db.ScalarInt(
            @"SELECT CASE WHEN OBJECT_ID('dbo.ST_Dict_Table','U') IS NOT NULL
                            AND OBJECT_ID('dbo.ST_Dict_Field','U') IS NOT NULL
                            AND OBJECT_ID('dbo.ST_Dict_Column','U') IS NOT NULL THEN 1 ELSE 0 END") == 1;
    }

    /// <summary>GET api/system/dict/meta/tables</summary>
    [HttpGet("meta/tables")]
    [RequirePermission("system:dict:view")]
    public IActionResult MetaTables()
    {
        using var db = SqlAccess.Open();
        if (!MetaAvailable(db))
            return Api.Ok(new { available = false, items = Array.Empty<object>(), message = MetaMissingMessage });
        var rows = db.Query<DictTableDto>(
            @"SELECT t.Table_Name AS tableName, t.Table_ChsName AS tableChsName,
                     (SELECT COUNT(1) FROM dbo.ST_Dict_Field f WHERE f.Table_Name = t.Table_Name) AS itemCount
                FROM dbo.ST_Dict_Table t ORDER BY t.Table_ChsName, t.Table_Name");
        return Api.Ok(new { available = true, items = rows, message = (string?)null });
    }

    /// <summary>GET api/system/dict/meta/fields?tableName=</summary>
    [HttpGet("meta/fields")]
    [RequirePermission("system:dict:view")]
    public IActionResult MetaFields([FromQuery] string? tableName)
    {
        using var db = SqlAccess.Open();
        if (!MetaAvailable(db)) return Api.Ok(new { available = false, items = Array.Empty<object>(), message = MetaMissingMessage });

        var sql = @"SELECT ID_KEY AS id, Table_Name AS tableName, Field_Name AS fieldName, Field_Note AS fieldNote,
                           Field_No AS fieldNo, Is_PK AS isPk, Field_Type AS fieldType, Field_Length AS fieldLength,
                           BaseTable AS baseTable
                      FROM dbo.ST_Dict_Field";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            sql += " WHERE Table_Name = @t";
            parameters.Add(SqlSession.P("@t", tableName.Trim()));
        }
        sql += " ORDER BY Table_Name, Field_No";
        return Api.Ok(new { available = true, items = db.Query<DictMetaFieldDto>(sql, parameters.ToArray()), message = (string?)null });
    }

    /// <summary>GET api/system/dict/meta/columns?tableName=</summary>
    [HttpGet("meta/columns")]
    [RequirePermission("system:dict:view")]
    public IActionResult MetaColumns([FromQuery] string? tableName)
    {
        using var db = SqlAccess.Open();
        if (!MetaAvailable(db)) return Api.Ok(new { available = false, items = Array.Empty<object>(), message = MetaMissingMessage });

        var sql = @"SELECT ID_KEY AS id, Grid_Name AS gridName, Field_Name AS fieldName, Display_Name AS displayName,
                           Display_Len AS displayLen, Field_Index AS fieldIndex, Is_Show AS isShow, Is_Sum AS isSum
                      FROM dbo.ST_Dict_Column";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            sql += " WHERE Grid_Name = @t OR Field_Name IN (SELECT Field_Name FROM dbo.ST_Dict_Field WHERE Table_Name = @t)";
            parameters.Add(SqlSession.P("@t", tableName.Trim()));
        }
        sql += " ORDER BY Grid_Name, Field_Index";
        return Api.Ok(new { available = true, items = db.Query<DictMetaColumnDto>(sql, parameters.ToArray()), message = (string?)null });
    }

    /// <summary>POST api/system/dict/meta/sync：按 INFORMATION_SCHEMA 同步字段登记。</summary>
    [HttpPost("meta/sync")]
    [RequirePermission("system:dict:sync")]
    public IActionResult MetaSync([FromBody] DictMetaSyncRequest request)
    {
        var tableName = (request?.TableName ?? "").Trim();
        if (tableName.Length == 0) return Api.BadRequest("请指定要同步的表");

        using var db = SqlAccess.Open();
        if (!MetaAvailable(db)) return Api.Fail(422, "42201", MetaMissingMessage);

        var dbColumns = db.Query<DbColumnRow>(
            @"SELECT c.name AS name, t.name AS typeName, c.max_length AS maxLength, c.is_nullable AS nullable,
                     CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic
                                        JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                                       WHERE ic.object_id = c.object_id AND ic.column_id = c.column_id AND i.is_primary_key = 1)
                          THEN 1 ELSE 0 END AS isPk
                FROM sys.columns c
                JOIN sys.types t ON t.user_type_id = c.user_type_id
               WHERE c.object_id = OBJECT_ID(@t)
               ORDER BY c.column_id",
            SqlSession.P("@t", "dbo." + tableName));
        if (dbColumns.Count == 0) return Api.NotFound("数据表不存在或没有字段");

        var metaFields = db.Query<DictMetaFieldDto>(
            "SELECT ID_KEY AS id, Field_Name AS fieldName, Field_Type AS fieldType, Field_Length AS fieldLength FROM dbo.ST_Dict_Field WHERE Table_Name = @t",
            SqlSession.P("@t", tableName));
        var metaMap = metaFields.ToDictionary(f => f.fieldName, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var updated = 0;
        var pending = 0;
        db.Begin();
        try
        {
            var index = 0;
            foreach (var column in dbColumns)
            {
                index++;
                var length = column.maxLength < 0 ? 0 : column.maxLength;
                if (metaMap.TryGetValue(column.name, out var meta))
                {
                    if (!string.Equals(meta.fieldType, column.typeName, StringComparison.OrdinalIgnoreCase)
                        || (meta.fieldLength ?? 0) != length)
                    {
                        db.Exec("UPDATE dbo.ST_Dict_Field SET Field_Type = @type, Field_Length = @len WHERE ID_KEY = @id",
                            SqlSession.P("@type", column.typeName), SqlSession.P("@len", length), SqlSession.P("@id", meta.id));
                        updated++;
                    }
                }
                else
                {
                    db.Exec(
                        @"INSERT INTO dbo.ST_Dict_Field (Table_Name, Field_Name, Field_Note, Field_No, Is_PK, Field_Type, Field_Length, BaseTable)
                          VALUES (@t, @f, @f, @no, @pk, @type, @len, @t)",
                        SqlSession.P("@t", tableName), SqlSession.P("@f", column.name), SqlSession.P("@no", index),
                        SqlSession.P("@pk", column.isPk == 1), SqlSession.P("@type", column.typeName), SqlSession.P("@len", length));
                    inserted++;
                }
            }

            // 数据库中已删除的字段只标记待确认，不自动删除
            var dbNames = dbColumns.Select(c => c.name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var meta in metaFields.Where(m => !dbNames.Contains(m.fieldName)))
            {
                db.Exec("UPDATE dbo.ST_Dict_Field SET Field_Note = N'待确认：数据库已无此字段' WHERE ID_KEY = @id",
                    SqlSession.P("@id", meta.id));
                pending++;
            }
            db.Commit();
            ConfigAudit.Write(HttpContext, "dictMeta", "sync", tableName, null, null, new { inserted, updated, pending });
            return Api.Ok(new { inserted, updated, pending }, $"新增 {inserted}、更新 {updated}、待确认 {pending}");
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /* ---------------- 一致性检查 ---------------- */

    /// <summary>GET api/system/dict/consistency：三类结构差异 + 编码质量警告。</summary>
    [HttpGet("consistency")]
    [RequirePermission("system:dict:view")]
    public IActionResult Consistency([FromQuery] string? tableName)
    {
        using var db = SqlAccess.Open();
        var result = new List<ConsistencyItemDto>();

        if (MetaAvailable(db))
        {
            var metaFields = db.Query<DictMetaFieldDto>(
                @"SELECT ID_KEY AS id, Table_Name AS tableName, Field_Name AS fieldName, Field_Type AS fieldType, Field_Length AS fieldLength
                    FROM dbo.ST_Dict_Field" + (string.IsNullOrWhiteSpace(tableName) ? "" : " WHERE Table_Name = @t"),
                string.IsNullOrWhiteSpace(tableName)
                    ? Array.Empty<Microsoft.Data.SqlClient.SqlParameter>()
                    : new[] { SqlSession.P("@t", tableName!.Trim()) });

            foreach (var group in metaFields.GroupBy(f => f.tableName))
            {
                var columns = db.Query<DbColumnRow>(
                    @"SELECT c.name AS name, t.name AS typeName, c.max_length AS maxLength
                        FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
                       WHERE c.object_id = OBJECT_ID(@t)",
                    SqlSession.P("@t", "dbo." + group.Key));
                if (columns.Count == 0)
                {
                    result.Add(new ConsistencyItemDto
                    {
                        issueType = "metaOnly",
                        tableName = group.Key,
                        message = "元数据登记了该表，但数据库中不存在",
                    });
                    continue;
                }

                var columnMap = columns.GroupBy(c => c.name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var field in group)
                {
                    if (!columnMap.TryGetValue(field.fieldName, out var column))
                    {
                        result.Add(new ConsistencyItemDto
                        {
                            issueType = "metaOnly",
                            tableName = group.Key,
                            fieldName = field.fieldName,
                            metaValue = field.fieldType,
                            message = "元数据显示存在，数据库无此字段",
                        });
                        continue;
                    }
                    var length = column.maxLength < 0 ? 0 : column.maxLength;
                    if (!string.Equals(field.fieldType, column.typeName, StringComparison.OrdinalIgnoreCase)
                        || (field.fieldLength ?? 0) != length)
                    {
                        result.Add(new ConsistencyItemDto
                        {
                            issueType = "typeMismatch",
                            tableName = group.Key,
                            fieldName = field.fieldName,
                            metaValue = $"{field.fieldType}({field.fieldLength})",
                            dbValue = $"{column.typeName}({length})",
                            message = "类型或长度不一致",
                        });
                    }
                }
                foreach (var column in columns.Where(c => !group.Any(f => string.Equals(f.fieldName, c.name, StringComparison.OrdinalIgnoreCase))))
                {
                    result.Add(new ConsistencyItemDto
                    {
                        issueType = "dbOnly",
                        tableName = group.Key,
                        fieldName = column.name,
                        dbValue = column.typeName,
                        message = "数据库有字段，元数据未登记",
                    });
                }
            }
        }

        // 编码质量检查（不依赖元数据表）
        var badIds = db.Query<CountRow>(
            @"SELECT Table_Name AS keyName, COUNT(1) AS cnt FROM dbo.ST_Dictionary
               WHERE ISNUMERIC(Dict_Id) = 0
               GROUP BY Table_Name");
        foreach (var row in badIds)
        {
            result.Add(new ConsistencyItemDto
            {
                issueType = "warning",
                tableName = row.keyName ?? "",
                message = $"存在 {row.cnt} 条非数字编码，无法自动生成新编码",
            });
        }

        var dups = db.Query<CountRow>(
            @"SELECT Table_Name + ':' + Dict_Id AS keyName, COUNT(1) AS cnt FROM dbo.ST_Dictionary
               GROUP BY Table_Name, Dict_Id HAVING COUNT(1) > 1");
        foreach (var row in dups)
        {
            result.Add(new ConsistencyItemDto
            {
                issueType = "warning",
                tableName = row.keyName ?? "",
                message = "同表内编码重复",
            });
        }

        var emptyNames = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_ChsName IS NULL OR LTRIM(RTRIM(Table_ChsName)) = ''");
        if (emptyNames > 0)
        {
            result.Add(new ConsistencyItemDto
            {
                issueType = "warning",
                tableName = "-",
                message = $"有 {emptyNames} 条编码的名称（Table_ChsName）为空",
            });
        }

        return Api.Ok(new { metaAvailable = MetaAvailable(db), items = result });
    }

    /// <summary>POST api/system/dict/consistency/fix-script：只生成新增与更新的修复脚本。</summary>
    [HttpPost("consistency/fix-script")]
    [RequirePermission("system:dict:sync")]
    public IActionResult FixScript()
    {
        using var db = SqlAccess.Open();
        if (!MetaAvailable(db)) return Api.Fail(422, "42201", MetaMissingMessage);

        var tables = db.Query<DictTableDto>("SELECT Table_Name AS tableName FROM dbo.ST_Dict_Table");
        var script = new StringBuilder();
        script.AppendLine("-- 数据字典一致性修复脚本（仅新增与更新，不含删除）");
        script.AppendLine("-- 由 BS 数据字典生成，请 DBA 审核后在数据库端执行");
        script.AppendLine();

        foreach (var table in tables)
        {
            var columns = db.Query<DbColumnRow>(
                @"SELECT c.name AS name, t.name AS typeName, c.max_length AS maxLength
                    FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
                   WHERE c.object_id = OBJECT_ID(@t)",
                SqlSession.P("@t", "dbo." + table.tableName));
            if (columns.Count == 0) continue;

            var metaFields = db.Query<DictMetaFieldDto>(
                "SELECT ID_KEY AS id, Field_Name AS fieldName, Field_Type AS fieldType, Field_Length AS fieldLength FROM dbo.ST_Dict_Field WHERE Table_Name = @t",
                SqlSession.P("@t", table.tableName));
            var metaMap = metaFields.ToDictionary(f => f.fieldName, StringComparer.OrdinalIgnoreCase);

            var index = 0;
            foreach (var column in columns)
            {
                index++;
                var length = column.maxLength < 0 ? 0 : column.maxLength;
                if (!metaMap.TryGetValue(column.name, out var meta))
                {
                    script.AppendLine(
                        $"IF NOT EXISTS (SELECT 1 FROM dbo.ST_Dict_Field WHERE Table_Name = '{Esc(table.tableName)}' AND Field_Name = '{Esc(column.name)}')");
                    script.AppendLine(
                        $"    INSERT INTO dbo.ST_Dict_Field (Table_Name, Field_Name, Field_Note, Field_No, Is_PK, Field_Type, Field_Length, BaseTable) VALUES ('{Esc(table.tableName)}', '{Esc(column.name)}', '{Esc(column.name)}', {index}, 0, '{Esc(column.typeName)}', {length}, '{Esc(table.tableName)}');");
                }
                else if (!string.Equals(meta.fieldType, column.typeName, StringComparison.OrdinalIgnoreCase) || (meta.fieldLength ?? 0) != length)
                {
                    script.AppendLine(
                        $"UPDATE dbo.ST_Dict_Field SET Field_Type = '{Esc(column.typeName)}', Field_Length = {length} WHERE Table_Name = '{Esc(table.tableName)}' AND Field_Name = '{Esc(column.name)}';");
                }
            }
        }

        ConfigAudit.Write(HttpContext, "dictMeta", "fixScript", "consistency", null, null, new { length = script.Length });
        return Api.Ok(new { fileName = $"dict-fix-{DateTime.Now:yyyyMMddHHmmss}.sql", content = script.ToString() });
    }

    /* ---------------- 内部 ---------------- */

    private const string MetaMissingMessage = "当前数据库未包含字典元数据表，请执行 CS 4.2.1 升级脚本后再使用本功能";

    public sealed class DbColumnRow
    {
        public string name { get; set; } = "";
        public string typeName { get; set; } = "";
        public int maxLength { get; set; }
        public bool nullable { get; set; }
        public int isPk { get; set; }
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private static string Esc(string? text) => (text ?? "").Replace("'", "''");
}

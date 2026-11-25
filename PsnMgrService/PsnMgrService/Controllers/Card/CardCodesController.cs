using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 卡务编码管理：ST_Dictionary 的业务视图，固定人员与组织相关编码表白名单，
/// 并在系统配置字典页的基础上增加“被人员引用数”。编码规则与 CS 一致。
/// </summary>
[Route("api/card/codes")]
[ApiController]
public class CardCodesController : ControllerBase
{
    private readonly AuditService _audit;

    public CardCodesController(AuditService audit)
    {
        _audit = audit;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;

    private static readonly Dictionary<string, string> TableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BM_NATIONALITY"] = "民族",
        ["BM_DUTY"] = "职务",
        ["BM_UNIT"] = "单位",
        ["BM_KAOQIN"] = "考勤类别",
        ["BM_WAREUNIT"] = "商品单位",
        ["BM_MJDOORAPBNo"] = "门禁时段编号",
        ["BM_MJDOORHideBackNo"] = "门禁隐藏备用编号",
        ["BM_PERSONTYPE"] = "人员类型",
    };

    /// <summary>GET api/card/codes/tables：编码表清单，默认只返回卡务白名单。</summary>
    [HttpGet("tables")]
    [RequirePermission("card:code:view")]
    public IActionResult Tables([FromQuery] bool all = false, [FromQuery] string? keyword = null)
    {
        using var db = SqlAccess.Open();
        var sql = @"
SELECT Table_Name AS tableName, MAX(Table_ChsName) AS tableChsName, COUNT(1) AS itemCount
  FROM dbo.ST_Dictionary";
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " WHERE (Table_Name LIKE @key OR Table_ChsName LIKE @key)";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        sql += " GROUP BY Table_Name ORDER BY MAX(Table_ChsName), Table_Name";

        var rows = db.Query<CardCodeTable>(sql, parameters.ToArray());
        if (!all)
        {
            rows = rows.Where(r => TableNames.ContainsKey(r.tableName ?? string.Empty)).ToList();
        }

        foreach (var row in rows)
        {
            var name = row.tableName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(row.tableChsName) && TableNames.TryGetValue(name, out var chs)) row.tableChsName = chs;
            row.refSupported = CardLookup.RefField(name) != null;
            row.isDefault = TableNames.ContainsKey(name);
        }

        // 人员类型未建编码表时给出提示，便于页面引导新建
        var hasPersonType = rows.Any(r => string.Equals(r.tableName, "BM_PERSONTYPE", StringComparison.OrdinalIgnoreCase));
        return Api.Ok(new
        {
            items = rows,
            missingPersonType = !hasPersonType,
            hint = hasPersonType ? null : "该字段尚未建立编码表，可在此新建 BM_PERSONTYPE",
        });
    }

    /// <summary>GET api/card/codes/items：编码明细，附带被人员引用数。</summary>
    [HttpGet("items")]
    [RequirePermission("card:code:view")]
    public IActionResult Items([FromQuery] string? tableName, [FromQuery] string? keyword = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var table = CardRules.Trim(tableName);
        if (table.Length == 0) return Api.BadRequest("请指定编码表");
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 200);

        using var db = SqlAccess.Open();
        var sql = @"
SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue,
       Is_SysCol AS sysCol, Remark AS remark
  FROM dbo.ST_Dictionary WHERE Table_Name = @table";
        var parameters = new List<SqlParameter> { SqlSession.P("@table", table) };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " AND (Dict_Id LIKE @key OR Dict_Value LIKE @key OR Remark LIKE @key)";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        sql += " ORDER BY Dict_Id";

        var all = db.Query<CardDictItem>(sql, parameters.ToArray());
        var refCounts = LoadRefCounts(db, table);
        foreach (var item in all)
        {
            item.refCount = item.dictId != null && refCounts.TryGetValue(item.dictId, out var count) ? count : 0;
        }

        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var chsName = db.Scalar("SELECT TOP 1 Table_ChsName FROM dbo.ST_Dictionary WHERE Table_Name = @table AND Table_ChsName IS NOT NULL",
            SqlSession.P("@table", table)) as string;
        if (string.IsNullOrWhiteSpace(chsName) && TableNames.TryGetValue(table, out var mappedName)) chsName = mappedName;

        return Api.Ok(new
        {
            total = all.Count,
            items,
            tableName = table,
            tableChsName = chsName,
            refSupported = CardLookup.RefField(table) != null,
        });
    }

    /// <summary>POST api/card/codes/items：新增编码，编码默认取同表整数最大值加一。</summary>
    [HttpPost("items")]
    [RequirePermission("card:code:create")]
    public IActionResult Create([FromBody] CardCodeSaveRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        var table = CardRules.Trim(request.TableName);
        if (table.Length == 0) return Api.BadRequest("请指定编码表");
        if (!TableNames.ContainsKey(table) && !request.AllowAnyTable)
            return Api.BadRequest("卡务编码管理只允许维护人员与组织相关编码表");
        if (table.Length > 20) return Api.BadRequest("编码表名不得超过 20 个字符");

        var value = CardRules.Trim(request.DictValue);
        if (value.Length == 0) return Api.BadRequest("名称不能为空");
        if (value.Length > 40) return Api.BadRequest("名称不得超过 40 个字符");

        using var db = SqlAccess.Open();
        var dictId = CardRules.Trim(request.DictId);
        if (dictId.Length == 0)
        {
            var max = db.ScalarInt(@"
SELECT ISNULL(MAX(TRY_CAST(Dict_Id AS INT)), 0) FROM dbo.ST_Dictionary WHERE Table_Name = @table",
                SqlSession.P("@table", table));
            dictId = (max + 1).ToString(CultureInfo.InvariantCulture);
        }
        if (dictId.Length > 20) return Api.BadRequest("编码不得超过 20 个字符");

        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_Name = @table AND Dict_Id = @id",
                SqlSession.P("@table", table), SqlSession.P("@id", dictId)) > 0)
            return Api.Fail(400, "40901", $"编码 {dictId} 在该表中已存在");

        var chsName = CardRules.Trim(request.TableChsName);
        if (chsName.Length == 0)
        {
            var existing = db.Scalar(
                "SELECT TOP 1 Table_ChsName FROM dbo.ST_Dictionary WHERE Table_Name = @table AND Table_ChsName IS NOT NULL",
                SqlSession.P("@table", table)) as string;
            if (!string.IsNullOrWhiteSpace(existing)) chsName = existing;
            else chsName = TableNames.TryGetValue(table, out var mapped) ? mapped : table;
        }

        db.Exec(@"
INSERT INTO dbo.ST_Dictionary (Table_Name, Table_ChsName, Dict_Id, Dict_Value, Is_SysCol, Remark)
VALUES (@table, @chsName, @id, @value, 0, @remark)",
            SqlSession.P("@table", table),
            SqlSession.P("@chsName", Cut(chsName, 40)),
            SqlSession.P("@id", dictId),
            SqlSession.P("@value", Cut(value, 40)),
            SqlSession.P("@remark", Cut(request.Remark, 50)));

        _audit.Log(HttpContext, "card-code", "create", "dictionary", table + "/" + dictId,
            null, new { table, dictId, value });
        return Api.Ok(new { tableName = table, dictId, dictValue = value });
    }

    /// <summary>PUT api/card/codes/items/{id}：修改编码名称与备注。</summary>
    [HttpPut("items/{id:int}")]
    [RequirePermission("card:code:update")]
    public IActionResult Update(int id, [FromBody] CardCodeSaveRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardDictItem>(
            "SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark " +
            "FROM dbo.ST_Dictionary WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("编码不存在");

        var value = CardRules.Trim(request.DictValue);
        if (value.Length == 0 || value.Length > 40) return Api.BadRequest("名称必填，且不超过 40 个字符");

        var dictId = CardRules.Trim(request.DictId);
        if (dictId.Length == 0) dictId = CardRules.Trim(current.dictId);
        if (dictId.Length > 20) return Api.BadRequest("编码不得超过 20 个字符");

        if (!string.Equals(dictId, CardRules.Trim(current.dictId), StringComparison.Ordinal))
        {
            if (current.sysCol == true) return Api.Fail(409, "40901", "系统内置编码不允许修改编码值");
            var duplicated = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_Name = @table AND Dict_Id = @id AND ID_KEY <> @key",
                SqlSession.P("@table", current.tableName), SqlSession.P("@id", dictId), SqlSession.P("@key", id));
            if (duplicated > 0) return Api.Fail(400, "40901", $"编码 {dictId} 在该表中已存在");
        }

        db.Exec("UPDATE dbo.ST_Dictionary SET Dict_Id = @id, Dict_Value = @value, Remark = @remark WHERE ID_KEY = @key",
            SqlSession.P("@id", dictId), SqlSession.P("@value", Cut(value, 40)),
            SqlSession.P("@remark", Cut(request.Remark, 50)), SqlSession.P("@key", id));

        _audit.Log(HttpContext, "card-code", "update", "dictionary", id.ToString(CultureInfo.InvariantCulture),
            current, new { dictId, value });
        return Api.Ok(new { id, dictId, dictValue = value });
    }

    /// <summary>DELETE api/card/codes/items/{id}：删除编码，系统列禁止删除，被引用时需二次确认。</summary>
    [HttpDelete("items/{id:int}")]
    [RequirePermission("card:code:delete")]
    public IActionResult Delete(int id, [FromQuery] bool confirm = false)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardDictItem>(
            "SELECT ID_KEY AS id, Table_Name AS tableName, Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol " +
            "FROM dbo.ST_Dictionary WHERE ID_KEY = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("编码不存在");
        if (current.sysCol == true) return Api.Fail(409, "40901", "系统内置编码不允许删除，避免 CS 默认值失效");

        var refCounts = LoadRefCounts(db, current.tableName ?? string.Empty);
        var used = current.dictId != null && refCounts.TryGetValue(current.dictId, out var count) ? count : 0;
        if (used > 0 && !confirm)
            return Api.Fail(409, "40902", $"该编码已被 {used} 名人员使用，删除后人员档案显示为空，是否继续？");

        db.Exec("DELETE FROM dbo.ST_Dictionary WHERE ID_KEY = @id", SqlSession.P("@id", id));
        _audit.Log(HttpContext, "card-code", "delete", "dictionary", id.ToString(CultureInfo.InvariantCulture),
            current, null);
        return Api.Ok(new { id, affectedPersons = used });
    }

    /// <summary>POST api/card/codes/items/import：按模板导入编码（首行表头，列：编码/名称/备注）。</summary>
    [HttpPost("items/import")]
    [RequirePermission("card:code:create")]
    public async Task<IActionResult> Import([FromForm] string tableName, [FromForm] IFormFile? file, [FromForm] bool allowAnyTable = false)
    {
        var table = CardRules.Trim(tableName);
        if (table.Length == 0) return Api.BadRequest("请指定编码表");
        if (!TableNames.ContainsKey(table) && !allowAnyTable)
            return Api.BadRequest("卡务编码管理只允许维护人员与组织相关编码表");
        if (file == null || file.Length == 0) return Api.BadRequest("请上传编码文件，支持 xlsx 或 csv");

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            bytes = stream.ToArray();
        }

        List<string[]> rows;
        try
        {
            rows = CardXlsx.Read(bytes, file.FileName);
        }
        catch (Exception ex)
        {
            return Api.Fail(422, "42204", "导入文件格式错误：" + ex.Message);
        }
        if (rows.Count <= 1) return Api.Fail(422, "42204", "文件没有可导入的数据行");

        using var db = SqlAccess.Open();
        var created = 0;
        var updated = 0;
        var errors = new List<object>();

        db.Begin();
        try
        {
            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var dictId = row.Length > 0 ? CardRules.Trim(row[0]) : string.Empty;
                var value = row.Length > 1 ? CardRules.Trim(row[1]) : string.Empty;
                var remark = row.Length > 2 ? CardRules.Trim(row[2]) : string.Empty;

                if (value.Length == 0)
                {
                    errors.Add(new { row = i + 1, column = "名称", message = "名称不能为空" });
                    continue;
                }
                if (dictId.Length == 0)
                {
                    var max = db.ScalarInt("SELECT ISNULL(MAX(TRY_CAST(Dict_Id AS INT)), 0) FROM dbo.ST_Dictionary WHERE Table_Name = @table",
                        SqlSession.P("@table", table));
                    dictId = (max + 1).ToString(CultureInfo.InvariantCulture);
                }

                var existing = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Dictionary WHERE Table_Name = @table AND Dict_Id = @id",
                    SqlSession.P("@table", table), SqlSession.P("@id", dictId));
                if (existing > 0)
                {
                    db.Exec("UPDATE dbo.ST_Dictionary SET Dict_Value = @value, Remark = @remark WHERE Table_Name = @table AND Dict_Id = @id",
                        SqlSession.P("@value", Cut(value, 40)), SqlSession.P("@remark", Cut(remark, 50)),
                        SqlSession.P("@table", table), SqlSession.P("@id", dictId));
                    updated++;
                }
                else
                {
                    var chsName = TableNames.TryGetValue(table, out var name) ? name : table;
                    db.Exec(@"
INSERT INTO dbo.ST_Dictionary (Table_Name, Table_ChsName, Dict_Id, Dict_Value, Is_SysCol, Remark)
VALUES (@table, @chsName, @id, @value, 0, @remark)",
                        SqlSession.P("@table", table), SqlSession.P("@chsName", Cut(chsName, 40)),
                        SqlSession.P("@id", dictId), SqlSession.P("@value", Cut(value, 40)),
                        SqlSession.P("@remark", Cut(remark, 50)));
                    created++;
                }
            }
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }

        _audit.Log(HttpContext, "card-code", "import", "dictionary", table, null, new { created, updated, fail = errors.Count });
        return Api.Ok(new { tableName = table, created, updated, fail = errors.Count, errors });
    }

    /// <summary>GET api/card/codes/items/export：导出编码表。</summary>
    [HttpGet("items/export")]
    [RequirePermission("card:code:view")]
    public IActionResult Export([FromQuery] string? tableName)
    {
        var table = CardRules.Trim(tableName);
        if (table.Length == 0) return Api.BadRequest("请指定编码表");

        using var db = SqlAccess.Open();
        var items = db.Query<CardDictItem>(@"
SELECT Dict_Id AS dictId, Dict_Value AS dictValue, Is_SysCol AS sysCol, Remark AS remark
  FROM dbo.ST_Dictionary WHERE Table_Name = @table ORDER BY Dict_Id", SqlSession.P("@table", table));

        var rows = new List<string[]> { new[] { "编码", "名称", "系统列", "备注" } };
        rows.AddRange(items.Select(item => new[]
        {
            item.dictId ?? string.Empty,
            item.dictValue ?? string.Empty,
            item.sysCol == true ? "是" : "否",
            item.remark ?? string.Empty,
        }));

        var bytes = CardXlsx.Build(string.IsNullOrWhiteSpace(table) ? "编码" : table, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"编码_{table}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>按表统计每个编码被在册人员引用的次数，一次分组查询完成。</summary>
    private static Dictionary<string, int> LoadRefCounts(SqlSession db, string tableName)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mapping = CardLookup.RefField(tableName);
        if (mapping == null) return result;

        var rows = db.Query<CardRefCount>($@"
SELECT CAST({mapping.Value.Column} AS VARCHAR(20)) AS code, COUNT(1) AS cnt
  FROM dbo.{mapping.Value.Table}
 WHERE ISNULL(Is_Del, 0) = 0 AND {mapping.Value.Column} IS NOT NULL AND {mapping.Value.Column} <> ''
 GROUP BY {mapping.Value.Column}");

        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.code)) result[row.code!] = row.cnt;
        }
        return result;
    }

    private static string Cut(string? value, int length)
    {
        var text = CardRules.Trim(value);
        return text.Length <= length ? text : text.Substring(0, length);
    }
}

public class CardCodeTable
{
    public string? tableName { get; set; }
    public string? tableChsName { get; set; }
    public int itemCount { get; set; }
    public bool refSupported { get; set; }
    public bool isDefault { get; set; }
}

public class CardRefCount
{
    public string? code { get; set; }
    public int cnt { get; set; }
}

public class CardCodeSaveRequest
{
    public string? TableName { get; set; }
    public string? TableChsName { get; set; }
    public string? DictId { get; set; }
    public string? DictValue { get; set; }
    public string? Remark { get; set; }
    public bool AllowAnyTable { get; set; }
}

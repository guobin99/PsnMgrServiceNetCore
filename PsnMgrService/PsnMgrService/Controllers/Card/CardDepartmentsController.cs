using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 部门档案：组织树、详情、新增、修改、停用/启用、排序与人员清单。
/// 层级仍由 Dept_No 两位一级推导，BS_DeptExt 四列只做缓存与排序。
/// </summary>
[Route("api/card/departments")]
[ApiController]
public class CardDepartmentsController : ControllerBase
{
    private readonly AuditService _audit;

    public CardDepartmentsController(AuditService audit)
    {
        _audit = audit;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;

    /// <summary>GET api/card/departments/tree：扁平组织树，附带本部门与含下级在册人数。</summary>
    [HttpGet("tree")]
    [RequirePermission("card:dept:view")]
    public IActionResult Tree([FromQuery] bool includeDisabled = false, [FromQuery] bool includeCount = true)
    {
        using var db = SqlAccess.Open();
        var sql = @"
SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Dept_Chief AS chief,
       Dept_Total AS personCount, Is_Del AS isDel, BS_Sort_No AS sortNo, BS_Dept_Type AS deptType,
       BS_Parent_ID AS parentIdCache, Dept_Duty AS duty, Add_Col1 AS addCol1, Add_Col2 AS addCol2, Remark AS remark
  FROM dbo.ST_Department";
        if (!includeDisabled) sql += " WHERE ISNULL(Is_Del, 0) = 0";
        sql += " ORDER BY ISNULL(BS_Sort_No, 2147483647), Dept_No";

        var rows = db.Query<CardDeptRow>(sql);

        var byNo = rows.Where(r => !string.IsNullOrWhiteSpace(r.deptNo))
            .GroupBy(r => r.deptNo!.Trim())
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var row in rows)
        {
            var parentNo = OrgPermissionHelper.GetParentNo(row.deptNo);
            row.parentId = parentNo != null && byNo.TryGetValue(parentNo, out var parent) ? parent.deptId : null;
            row.level = OrgPermissionHelper.GetLevel(row.deptNo);
        }

        var directCounts = new Dictionary<int, int>();
        if (includeCount)
        {
            var counts = db.Query<CardDeptCount>(@"
SELECT Dept_ID AS deptId, COUNT(1) AS cnt
  FROM dbo.ST_Person WHERE ISNULL(Is_Del, 0) = 0 GROUP BY Dept_ID");
            directCounts = counts.ToDictionary(c => c.deptId, c => c.cnt);

            foreach (var row in rows)
            {
                row.activeCount = 0;
                row.activeDirect = 0;
            }

            // 每个人员只累加到本部门与所有上级，避免自连接前缀匹配
            var byId = rows.GroupBy(r => r.deptId).ToDictionary(g => g.Key, g => g.First());
            foreach (var pair in directCounts)
            {
                if (!byId.TryGetValue(pair.Key, out var dept)) continue;
                dept.activeDirect += pair.Value;
                dept.activeCount += pair.Value;

                var no = dept.deptNo;
                foreach (var ancestorNo in OrgPermissionHelper.GetAncestorNos(no))
                {
                    if (byNo.TryGetValue(ancestorNo, out var ancestor)) ancestor.activeCount += pair.Value;
                }
            }
        }

        return Api.Ok(rows);
    }

    /// <summary>GET api/card/departments/{id}：部门详情与统计。</summary>
    [HttpGet("{id:int}")]
    [RequirePermission("card:dept:view")]
    public IActionResult Detail(int id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardDeptRow>(@"
SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Dept_Chief AS chief,
       Dept_Total AS personCount, Is_Del AS isDel, BS_Sort_No AS sortNo, BS_Dept_Type AS deptType,
       BS_Parent_ID AS parentIdCache, Dept_Duty AS duty, Add_Col1 AS addCol1, Add_Col2 AS addCol2,
       Remark AS remark, Dept_Telphone AS telphone, Create_Time AS createTime
  FROM dbo.ST_Department WHERE Dept_ID = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("部门不存在");

        var parentNo = OrgPermissionHelper.GetParentNo(row.deptNo);
        if (parentNo != null)
        {
            var parent = db.QuerySingle<CardDeptRow>(
                "SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName FROM dbo.ST_Department WHERE Dept_No = @no",
                SqlSession.P("@no", parentNo));
            row.parentId = parent?.deptId;
            row.parentNo = parent?.deptNo;
            row.parentName = parent?.deptName;
        }
        row.level = OrgPermissionHelper.GetLevel(row.deptNo);

        row.activeDirect = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_Person WHERE Dept_ID = @id AND ISNULL(Is_Del, 0) = 0", SqlSession.P("@id", id));
        row.activeCount = db.ScalarInt(@"
SELECT COUNT(1) FROM dbo.ST_Person p
  JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
 WHERE ISNULL(p.Is_Del, 0) = 0 AND (d.Dept_No = @no OR d.Dept_No LIKE @like)",
            SqlSession.P("@no", row.deptNo), SqlSession.P("@like", (row.deptNo ?? string.Empty) + "%"));
        row.childCount = db.ScalarInt(@"
SELECT COUNT(1) FROM dbo.ST_Department
 WHERE ISNULL(Is_Del, 0) = 0 AND Dept_No LIKE @like AND LEN(Dept_No) = LEN(@no) + 2",
            SqlSession.P("@like", (row.deptNo ?? string.Empty) + "%"), SqlSession.P("@no", row.deptNo ?? string.Empty));
        row.deletedCount = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_Person WHERE Dept_ID = @id AND ISNULL(Is_Del, 0) = 1", SqlSession.P("@id", id));

        return Api.Ok(row);
    }

    /// <summary>POST api/card/departments：新增部门，编号由存储过程或等价规则生成。</summary>
    [HttpPost]
    [RequirePermission("card:dept:create")]
    public IActionResult Create([FromBody] CardDeptSaveRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        var name = CardRules.Trim(request.DeptName);
        if (name.Length < 1 || name.Length > 30) return Api.BadRequest("部门名称必填，且不超过 30 个字符");

        using var db = SqlAccess.Open();

        string? parentNo = null;
        CardDeptRow? parent = null;
        if (request.ParentId.HasValue && request.ParentId.Value > 0)
        {
            parent = db.QuerySingle<CardDeptRow>(
                "SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Is_Del AS isDel FROM dbo.ST_Department WHERE Dept_ID = @id",
                SqlSession.P("@id", request.ParentId.Value));
            if (parent == null) return Api.Fail(400, "40005", "上级部门不存在");
            if (parent.isDel == true) return Api.Fail(400, "40005", "上级部门已停用，不能新增下级");
            parentNo = CardRules.Trim(parent.deptNo);
        }

        // 编号：显式传入则校验，未传则自动生成
        var deptNo = CardRules.Trim(request.DeptNo);
        if (deptNo.Length > 0)
        {
            if (!CardRules.IsValidDeptNo(deptNo)) return Api.Fail(400, "40005", "部门编号需为偶数长度且不小于 4 位");
            if (parentNo != null && !deptNo.StartsWith(parentNo, StringComparison.Ordinal))
                return Api.Fail(400, "40005", "部门编号必须以父部门编号为前缀");
            if (parentNo != null && deptNo.Length != parentNo.Length + 2)
                return Api.Fail(400, "40005", "部门编号需比父部门编号多两位");
            if (parentNo == null && deptNo.Length != 4)
                return Api.Fail(400, "40005", "根部门编号必须为 4 位");
        }
        else
        {
            string error;
            if (parentNo == null)
            {
                deptNo = CardDeptNo.NextRoot(db, out error) ?? string.Empty;
            }
            else
            {
                deptNo = CardDeptNo.Next(db, parentNo, out error) ?? string.Empty;
            }
            if (deptNo.Length == 0) return Api.Fail(400, "40005", error ?? "部门编号生成失败");
        }

        if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Department WHERE Dept_No = @no", SqlSession.P("@no", deptNo)) > 0)
            return Api.Fail(400, "40005", $"部门编号 {deptNo} 已存在");

        if (ExistsSiblingName(db, parentNo, name, null))
            return Api.BadRequest("同级下已存在同名部门");

        db.Begin();
        try
        {
            var deptId = NextDeptId(db);
            db.Exec(@"
INSERT INTO dbo.ST_Department
    (Dept_ID, Dept_No, Dept_Name, Dept_Chief, Dept_Total, Dept_Duty, Is_Del, Remark,
     Add_Col1, Add_Col2, BS_Parent_ID, BS_Level, BS_Sort_No, BS_Dept_Type, Create_Time)
VALUES
    (@id, @no, @name, @chief, 0, @duty, 0, @remark, @addCol1, @addCol2, @parentId, @level, @sortNo, @deptType, GETDATE())",
                SqlSession.P("@id", deptId),
                SqlSession.P("@no", deptNo),
                SqlSession.P("@name", Cut(name, 30)),
                SqlSession.P("@chief", Cut(request.Chief, 20)),
                SqlSession.P("@duty", Cut(request.Duty, 40)),
                SqlSession.P("@remark", Cut(request.Remark, 50)),
                SqlSession.P("@addCol1", Cut(request.AddCol1, 20)),
                SqlSession.P("@addCol2", Cut(request.AddCol2, 20)),
                SqlSession.P("@parentId", request.ParentId),
                SqlSession.P("@level", OrgPermissionHelper.GetLevel(deptNo)),
                SqlSession.P("@sortNo", request.SortNo),
                SqlSession.P("@deptType", Cut(request.DeptType, 20)));

            db.Commit();
            _audit.Log(HttpContext, "card-dept", "create", "department", deptId.ToString(CultureInfo.InvariantCulture),
                null, new { deptNo, name, parentId = request.ParentId });
            return Api.Ok(new { deptId, deptNo, deptName = name });
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>PUT api/card/departments/{id}：修改部门，编号不可变。</summary>
    [HttpPut("{id:int}")]
    [RequirePermission("card:dept:update")]
    public IActionResult Update(int id, [FromBody] CardDeptSaveRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardDeptRow>(
            "SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Dept_Chief AS chief, Dept_Duty AS duty, " +
            "Remark AS remark, Add_Col1 AS addCol1, Add_Col2 AS addCol2, BS_Sort_No AS sortNo, BS_Dept_Type AS deptType, Is_Del AS isDel " +
            "FROM dbo.ST_Department WHERE Dept_ID = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("部门不存在");

        var name = CardRules.Trim(request.DeptName);
        if (name.Length < 1 || name.Length > 30) return Api.BadRequest("部门名称必填，且不超过 30 个字符");

        var parentNo = OrgPermissionHelper.GetParentNo(current.deptNo);
        if (!string.Equals(name, current.deptName, StringComparison.Ordinal) && ExistsSiblingName(db, parentNo, name, id))
            return Api.BadRequest("同级下已存在同名部门");

        var sets = new List<string>();
        var parameters = new List<SqlParameter> { SqlSession.P("@id", id) };

        void Add(string column, string? value, string? original, string parameterName, int maxLength)
        {
            var text = Cut(value, maxLength);
            if (string.Equals(text, (original ?? string.Empty).Trim(), StringComparison.Ordinal)) return;
            sets.Add(column + " = " + parameterName);
            parameters.Add(SqlSession.P(parameterName, text));
        }

        Add("Dept_Name", name, current.deptName, "@name", 30);
        Add("Dept_Chief", request.Chief, current.chief, "@chief", 20);
        Add("Dept_Duty", request.Duty, current.duty, "@duty", 40);
        Add("Remark", request.Remark, current.remark, "@remark", 50);
        Add("Add_Col1", request.AddCol1, current.addCol1, "@addCol1", 20);
        Add("Add_Col2", request.AddCol2, current.addCol2, "@addCol2", 20);
        Add("BS_Dept_Type", request.DeptType, current.deptType, "@deptType", 20);

        if (request.SortNo != current.sortNo)
        {
            sets.Add("BS_Sort_No = @sortNo");
            parameters.Add(SqlSession.P("@sortNo", request.SortNo));
        }

        if (sets.Count == 0) return Api.Ok(new { deptId = id, changed = 0 }, "没有需要保存的修改");

        db.Exec("UPDATE dbo.ST_Department SET " + string.Join(", ", sets) + " WHERE Dept_ID = @id", parameters.ToArray());
        _audit.Log(HttpContext, "card-dept", "update", "department", id.ToString(CultureInfo.InvariantCulture),
            current, new { name, chief = request.Chief, sortNo = request.SortNo, deptType = request.DeptType });
        return Api.Ok(new { deptId = id, changed = sets.Count });
    }

    /// <summary>PUT api/card/departments/{id}/status：停用或启用。</summary>
    [HttpPut("{id:int}/status")]
    [RequirePermission("card:dept:delete")]
    public IActionResult ChangeStatus(int id, [FromBody] CardDeptStatusRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardDeptRow>(
            "SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Is_Del AS isDel FROM dbo.ST_Department WHERE Dept_ID = @id",
            SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("部门不存在");

        var deptNo = current.deptNo ?? string.Empty;

        if (request.Disabled)
        {
            if (current.isDel == true) return Api.StateConflict("部门已停用");

            var childCount = db.ScalarInt(@"
SELECT COUNT(1) FROM dbo.ST_Department
 WHERE ISNULL(Is_Del, 0) = 0 AND Dept_No LIKE @like AND LEN(Dept_No) = LEN(@no) + 2",
                SqlSession.P("@like", deptNo + "%"), SqlSession.P("@no", deptNo));
            var personCount = db.ScalarInt(@"
SELECT COUNT(1) FROM dbo.ST_Person p
  JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
 WHERE ISNULL(p.Is_Del, 0) = 0 AND (d.Dept_No = @no OR d.Dept_No LIKE @like)",
                SqlSession.P("@no", deptNo), SqlSession.P("@like", deptNo + "%"));

            if (childCount > 0 || personCount > 0)
                return Api.Fail(409, "40908",
                    $"该部门下仍有 {childCount} 个未停用下级部门、{personCount} 名在册人员，不能停用。停用还会清理该部门的授权记录。");

            db.Exec("UPDATE dbo.ST_Department SET Is_Del = 1 WHERE Dept_ID = @id", SqlSession.P("@id", id));
            _audit.Log(HttpContext, "card-dept", "disable", "department", id.ToString(CultureInfo.InvariantCulture),
                new { isDel = false }, new { isDel = true });
            return Api.Ok(new { deptId = id, disabled = true });
        }

        if (current.isDel != true) return Api.StateConflict("部门当前为启用状态");
        var parentNo = OrgPermissionHelper.GetParentNo(deptNo);
        if (parentNo != null)
        {
            var parentDisabled = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.ST_Department WHERE Dept_No = @no AND ISNULL(Is_Del, 0) = 1",
                SqlSession.P("@no", parentNo));
            if (parentDisabled > 0) return Api.BadRequest("上级部门已停用，请先启用上级部门");
        }

        db.Exec("UPDATE dbo.ST_Department SET Is_Del = 0 WHERE Dept_ID = @id", SqlSession.P("@id", id));
        _audit.Log(HttpContext, "card-dept", "enable", "department", id.ToString(CultureInfo.InvariantCulture),
            new { isDel = true }, new { isDel = false });
        return Api.Ok(new { deptId = id, disabled = false });
    }

    /// <summary>PUT api/card/departments/sort：保存同级排序，不改变层级。</summary>
    [HttpPut("sort")]
    [RequirePermission("card:dept:update")]
    public IActionResult Sort([FromBody] CardDeptSortRequest request)
    {
        if (request?.Items == null || request.Items.Count == 0) return Api.BadRequest("请提供排序结果");
        using var db = SqlAccess.Open();
        db.Begin();
        try
        {
            foreach (var item in request.Items)
            {
                db.Exec("UPDATE dbo.ST_Department SET BS_Sort_No = @sort WHERE Dept_ID = @id",
                    SqlSession.P("@sort", item.SortNo), SqlSession.P("@id", item.DeptId));
            }
            db.Commit();
            _audit.Log(HttpContext, "card-dept", "sort", "department", null, null, new { count = request.Items.Count });
            return Api.Ok(new { changed = request.Items.Count });
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>GET api/card/departments/{id}/persons：部门人员清单，供详情页与统计核对。</summary>
    [HttpGet("{id:int}/persons")]
    [RequirePermission("card:dept:view")]
    public IActionResult Persons(int id, [FromQuery] bool includeChildren = true, [FromQuery] string? keyword = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 10);

        using var db = SqlAccess.Open();
        var dept = db.QuerySingle<CardDeptRow>("SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName FROM dbo.ST_Department WHERE Dept_ID = @id",
            SqlSession.P("@id", id));
        if (dept == null) return Api.NotFound("部门不存在");

        var where = "ISNULL(p.Is_Del, 0) = 0";
        var parameters = new List<SqlParameter>();
        if (includeChildren)
        {
            where += " AND (d.Dept_No = @no OR d.Dept_No LIKE @like)";
            parameters.Add(SqlSession.P("@no", dept.deptNo));
            parameters.Add(SqlSession.P("@like", (dept.deptNo ?? string.Empty) + "%"));
        }
        else
        {
            where += " AND p.Dept_ID = @id";
            parameters.Add(SqlSession.P("@id", id));
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where += " AND (p.Person_No LIKE @key OR p.Person_Name LIKE @key)";
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person p LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID WHERE " + where,
            parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var select = @"SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
                              d.Dept_Name AS deptName, p.Card_No AS cardNo, p.Person_Type AS personType,
                              (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_PERSONTYPE' AND Dict_Id = p.Person_Type) AS personTypeName,
                              p.Is_Del AS isDel, p.update_time AS updateTime
                         FROM dbo.ST_Person p
                         LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
                        WHERE " + where;
        // 兼容 SQL Server 2008 R2：用 ROW_NUMBER 代替 OFFSET/FETCH，并按前端约定返回 { total, items }。
        var sql = @"SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.personNo) AS RowNum
    FROM (
" + select + @"
    ) q
) t
WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size
ORDER BY t.RowNum";

        return Api.Ok(new { total, items = db.Query<CardPersonBrief>(sql, parameters.ToArray()) });
    }

    /// <summary>GET api/card/departments/export：导出部门台账 xlsx。</summary>
    [HttpGet("export")]
    [RequirePermission("card:dept:export")]
    public IActionResult Export([FromQuery] int? deptId, [FromQuery] bool includeChildren = true,
        [FromQuery] bool includeDisabled = false)
    {
        using var db = SqlAccess.Open();
        List<string[]> table;
        try
        {
            table = CardDeptExport.BuildLedger(db, deptId, includeChildren, includeDisabled);
        }
        catch (CardDeptExportException ex)
        {
            return Api.BadRequest(ex.Message);
        }

        var bytes = CardXlsx.Build("部门台账", table);
        _audit.Log(HttpContext, "card-dept", "export", "department", null, null,
            new { total = table.Count - 1, deptId, includeChildren, includeDisabled });
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"部门台账_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /* ---------------- 内部方法 ---------------- */

    private static string Cut(string? value, int length)
    {
        var text = CardRules.Trim(value);
        return text.Length <= length ? text : text.Substring(0, length);
    }

    private static bool ExistsSiblingName(SqlSession db, string? parentNo, string name, int? excludeId)
    {
        // 同一父级 = 编号前缀相同且层级相同
        var sql = "SELECT COUNT(1) FROM dbo.ST_Department WHERE Dept_Name = @name AND ISNULL(Is_Del, 0) = 0 AND Dept_ID <> @id";
        if (string.IsNullOrEmpty(parentNo))
            sql += " AND LEN(Dept_No) = 2";
        else
        {
            sql += " AND LEN(Dept_No) = @len AND Dept_No LIKE @like";
        }
        var parameters = new List<SqlParameter>
        {
            SqlSession.P("@name", name),
            SqlSession.P("@id", excludeId ?? 0),
        };
        if (!string.IsNullOrEmpty(parentNo))
        {
            parameters.Add(SqlSession.P("@len", parentNo.Length + 2));
            parameters.Add(SqlSession.P("@like", parentNo + "%"));
        }
        return db.ScalarInt(sql, parameters.ToArray()) > 0;
    }

    private static int NextDeptId(SqlSession db)
    {
        try
        {
            var value = db.Scalar(
                "DECLARE @newId INT; " +
                "EXEC dbo.st_pd_GetMaxIDEx @TableName = 'ST_Department', @MaxTableID = @newId OUTPUT, @Increment = 1; " +
                "SELECT @newId;");
            if (value != null) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // 存储过程缺失时回退
        }

        var current = db.Scalar("SELECT Current_ID FROM dbo.ST_Identity WITH (UPDLOCK, ROWLOCK) WHERE Table_Name = 'ST_Department'");
        if (current == null)
        {
            var next = db.ScalarInt("SELECT ISNULL(MAX(Dept_ID), 0) FROM dbo.ST_Department") + 1;
            db.Exec("INSERT INTO dbo.ST_Identity (Table_Name, Current_ID) VALUES ('ST_Department', @next)", SqlSession.P("@next", next + 1));
            return next;
        }
        var id = Convert.ToInt32(current, CultureInfo.InvariantCulture);
        db.Exec("UPDATE dbo.ST_Identity SET Current_ID = @next WHERE Table_Name = 'ST_Department'", SqlSession.P("@next", id + 1));
        return id;
    }
}

public class CardDeptRow
{
    public int deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public int? parentId { get; set; }
    public int? parentIdCache { get; set; }
    public string? parentNo { get; set; }
    public string? parentName { get; set; }
    public int level { get; set; }
    public string? chief { get; set; }
    public string? duty { get; set; }
    public int? personCount { get; set; }
    public int activeCount { get; set; }
    public int activeDirect { get; set; }
    public int childCount { get; set; }
    public int deletedCount { get; set; }
    public bool? isDel { get; set; }
    public bool disabled => isDel == true;
    public int? sortNo { get; set; }
    public string? deptType { get; set; }
    public string? addCol1 { get; set; }
    public string? addCol2 { get; set; }
    public string? remark { get; set; }
    public string? telphone { get; set; }
    public DateTime? createTime { get; set; }
}

public class CardDeptCount
{
    public int deptId { get; set; }
    public int cnt { get; set; }
}

public class CardDeptSaveRequest
{
    public string? DeptNo { get; set; }
    public string? DeptName { get; set; }
    public int? ParentId { get; set; }
    public string? Chief { get; set; }
    public string? Duty { get; set; }
    public string? Remark { get; set; }
    public string? AddCol1 { get; set; }
    public string? AddCol2 { get; set; }
    public int? SortNo { get; set; }
    public string? DeptType { get; set; }
}

public class CardDeptStatusRequest
{
    public bool Disabled { get; set; }
}

public class CardDeptSortRequest
{
    public List<CardDeptSortItem> Items { get; set; } = new();
}

public class CardDeptSortItem
{
    public int DeptId { get; set; }
    public int SortNo { get; set; }
}

public class CardPersonBrief
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? deptName { get; set; }
    public string? cardNo { get; set; }
    public string? personType { get; set; }
    public string? personTypeName { get; set; }
    public bool isDel { get; set; }
    public DateTime? updateTime { get; set; }
}

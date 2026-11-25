using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>组织树：服务操作员管理、部门数据权限与出纳授权的公共上下文。</summary>
[Route("api/system/departments")]
[ApiController]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DepartmentsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>GET api/system/departments/tree?includeDisabled=false&amp;countMode=active</summary>
    [HttpGet("tree")]
    [RequirePermission("system:operator:view")]
    public async Task<IActionResult> Tree(
        [FromQuery] bool includeDisabled = false,
        [FromQuery] string? countMode = null)
    {
        var query = _context.ST_Departments.AsNoTracking();
        if (!includeDisabled) query = query.Where(d => d.Is_Del != true);

        var departments = await query.OrderBy(d => d.Dept_No).ToListAsync();
        var byNo = departments
            .Where(d => !string.IsNullOrWhiteSpace(d.Dept_No))
            .GroupBy(d => d.Dept_No!.Trim())
            .ToDictionary(g => g.Key, g => g.First());

        var items = departments.Select(dept =>
        {
            var deptNo = dept.Dept_No?.Trim();
            var parentNo = OrgPermissionHelper.GetParentNo(deptNo);
            ST_Department? parent = parentNo != null && byNo.TryGetValue(parentNo, out var p) ? p : null;
            return new DepartmentTreeNodeDto
            {
                deptId = dept.Dept_ID,
                deptNo = deptNo,
                deptName = dept.Dept_Name,
                parentId = parent?.Dept_ID,
                level = OrgPermissionHelper.GetLevel(deptNo),
                chief = dept.Dept_Chief,
                personCount = dept.Dept_Total ?? 0,
                disabled = dept.Is_Del == true,
                sortNo = dept.BS_Sort_No,
            };
        }).ToList();

        if (string.Equals(countMode, "active", StringComparison.OrdinalIgnoreCase))
        {
            // 实时统计在册人数；按 Dept_No 前缀向下汇总子部门，口径与黑名单部门筛选一致。
            // 只读统计，不回写 ST_Department.Dept_Total。
            var counts = await _context.Database.SqlQueryRaw<DeptActiveCountDto>(@"
                SELECT d.Dept_ID AS deptId, COUNT(p.Person_ID) AS activeCount
                  FROM dbo.ST_Department d
                  LEFT JOIN dbo.ST_Department child
                    ON child.Dept_No = d.Dept_No OR child.Dept_No LIKE d.Dept_No + '%'
                  LEFT JOIN dbo.ST_Person p
                    ON p.Dept_ID = child.Dept_ID AND ISNULL(p.Is_Del, 0) = 0
                 GROUP BY d.Dept_ID")
                .ToListAsync();
            var byDept = counts.ToDictionary(x => x.deptId, x => x.activeCount);
            foreach (var item in items)
                if (byDept.TryGetValue(item.deptId, out var count)) item.activeCount = count;

            // 本部门在册人数（不含下级），供部门树节点与部门档案显示同一套文案
            var directCounts = await _context.Database.SqlQueryRaw<DeptActiveCountDto>(@"
                SELECT p.Dept_ID AS deptId, COUNT(1) AS activeCount
                  FROM dbo.ST_Person p
                 WHERE ISNULL(p.Is_Del, 0) = 0
                 GROUP BY p.Dept_ID")
                .ToListAsync();
            var byDirect = directCounts.ToDictionary(x => x.deptId, x => x.activeCount);
            foreach (var item in items)
                if (byDirect.TryGetValue(item.deptId, out var direct)) item.activeDirect = direct;
        }

        return Api.Ok(items);
    }
}

public class DeptActiveCountDto
{
    public int deptId { get; set; }
    public int activeCount { get; set; }
}

public class DepartmentTreeNodeDto
{
    public int deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public int? parentId { get; set; }
    public int level { get; set; }
    public string? chief { get; set; }
    public int personCount { get; set; }
    public int? activeCount { get; set; }
    public int? activeDirect { get; set; }
    public bool disabled { get; set; }
    public int? sortNo { get; set; }
}

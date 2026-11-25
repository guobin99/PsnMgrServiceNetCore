using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>部门数据权限：ST_DeptRight 为存储，Check_State=0 显式授权，1 祖先占位。</summary>
[Route("api/system/dept-rights")]
[ApiController]
public class DeptRightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public DeptRightsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/dept-rights/{userNo}</summary>
    [HttpGet("{userNo}")]
    [RequirePermission("system:deptRight:view")]
    public async Task<IActionResult> Get(string userNo)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");

        if (IsAdminAccount(user))
        {
            return Api.Ok(new DeptRightDto
            {
                scopeType = 1,
                ownerDeptId = user.BS_Dept_ID,
                deptIds = new List<int>(),
                coveredCount = await _context.ST_Departments.CountAsync(d => d.Is_Del != true),
                readonlyScope = true,
            });
        }

        var scope = await _context.BS_UserDataScopes.AsNoTracking().FirstOrDefaultAsync(s => s.User_No == userNo);
        var explicitDepts = await _context.ST_DeptRights.AsNoTracking()
            .Where(r => r.User_No == userNo && r.Check_State == 0)
            .Select(r => r.Dept_ID)
            .ToListAsync();
        var coveredCount = await CountCoveredDepartmentsAsync(explicitDepts);
        return Api.Ok(new DeptRightDto
        {
            scopeType = scope?.Scope_Type ?? 3,
            ownerDeptId = scope?.Owner_Dept_ID ?? user.BS_Dept_ID,
            deptIds = explicitDepts,
            coveredCount = coveredCount,
            summary = scope?.Summary,
            updatedTime = scope?.Update_Time,
            readonlyScope = false,
        });
    }

    /// <summary>PUT api/system/dept-rights/{userNo}：整体替换 + 祖先补齐，同一事务。</summary>
    [HttpPut("{userNo}")]
    [RequirePermission("system:deptRight:assign")]
    public async Task<IActionResult> Save(string userNo, [FromBody] DeptRightSaveRequest request)
    {
        if (request == null || request.Scope_Type is < 1 or > 3) return Api.BadRequest("数据范围模式不合法");
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        if (user.Is_Del == true) return Api.StateConflict("操作员已注销");
        if (IsAdminAccount(user)) return Api.OutOfScope("Admin 账号的部门权限由系统维护，不允许修改");

        var departments = await _context.ST_Departments.AsNoTracking()
            .Where(d => d.Is_Del != true)
            .ToListAsync();
        var explicitDepts = request.Scope_Type switch
        {
            1 => departments.ToList(),
            2 => user.BS_Dept_ID == null
                ? new List<ST_Department>()
                : departments.Where(d => d.Dept_ID == user.BS_Dept_ID.Value).ToList(),
            _ => departments.Where(d => request.Dept_IDs.Contains(d.Dept_ID)).ToList(),
        };
        if (request.Scope_Type == 2 && explicitDepts.Count == 0) return Api.StateConflict("操作员未设置归属部门，无法使用“本部门及下级”");
        if (request.Scope_Type == 3 && explicitDepts.Count == 0) return Api.BadRequest("指定部门模式下至少选择一个部门");

        var records = BuildDeptRightRecords(userNo, explicitDepts, departments);
        var coveredCount = CountCovered(explicitDepts, departments);
        var summary = request.Scope_Type switch
        {
            1 => "全部部门",
            2 => $"本部门及下级（覆盖 {coveredCount} 个部门）",
            _ => $"指定部门（覆盖 {coveredCount} 个部门）",
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var old = await _context.ST_DeptRights.Where(r => r.User_No == userNo).ToListAsync();
        _context.ST_DeptRights.RemoveRange(old);
        _context.ST_DeptRights.AddRange(records);

        var scope = await _context.BS_UserDataScopes.FirstOrDefaultAsync(s => s.User_No == userNo);
        if (scope == null)
        {
            scope = new BS_UserDataScope
            {
                User_No = userNo,
                Scope_Type = request.Scope_Type,
                Owner_Dept_ID = user.BS_Dept_ID,
                Summary = summary,
                Update_Time = DateTime.Now,
                Update_User = User.GetUserNo(),
            };
            _context.BS_UserDataScopes.Add(scope);
        }
        else
        {
            scope.Scope_Type = request.Scope_Type;
            scope.Owner_Dept_ID = user.BS_Dept_ID;
            scope.Summary = summary;
            scope.Update_Time = DateTime.Now;
            scope.Update_User = User.GetUserNo();
        }

        _audit.Log(HttpContext, "deptRight", "save", "operator", userNo,
            new { oldCount = old.Count, oldScope = scope.Scope_Type },
            new { scopeType = request.Scope_Type, savedCount = records.Count, coveredCount });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Api.Ok(new { savedCount = records.Count, coveredCount, summary, version });
    }

    /// <summary>GET api/system/dept-rights/{userNo}/preview?scopeType=3&deptIds=1,2</summary>
    [HttpGet("{userNo}/preview")]
    [RequirePermission("system:deptRight:view")]
    public async Task<IActionResult> Preview(string userNo, [FromQuery] byte scopeType, [FromQuery] string? deptIds)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        var departments = await _context.ST_Departments.AsNoTracking().Where(d => d.Is_Del != true).ToListAsync();
        var ids = (deptIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var explicitDepts = scopeType switch
        {
            1 => departments.ToList(),
            2 => departments.Where(d => d.Dept_ID == user.BS_Dept_ID).ToList(),
            _ => departments.Where(d => ids.Contains(d.Dept_ID)).ToList(),
        };
        var covered = CountCovered(explicitDepts, departments);
        var personCount = await CountCoveredPersonsAsync(explicitDepts);
        return Api.Ok(new { coveredCount = covered, personCount });
    }

    /// <summary>POST api/system/dept-rights/recalc：按保存的模式批量重算授权。</summary>
    [HttpPost("recalc")]
    [RequirePermission("system:deptRight:assign")]
    public async Task<IActionResult> Recalculate([FromBody] DeptRightRecalcRequest request)
    {
        if (request?.User_Nos == null || request.User_Nos.Count == 0) return Api.BadRequest("请指定要重算的操作员");
        var departments = await _context.ST_Departments.AsNoTracking().Where(d => d.Is_Del != true).ToListAsync();
        var success = 0;
        var failed = 0;

        foreach (var userNo in request.User_Nos)
        {
            var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo && u.Is_Del != true);
            var scope = user == null ? null : await _context.BS_UserDataScopes.AsNoTracking().FirstOrDefaultAsync(s => s.User_No == userNo);
            if (user == null || scope == null || IsAdminAccount(user))
            {
                failed++;
                continue;
            }

            List<ST_Department> explicitDepts = scope.Scope_Type switch
            {
                1 => departments.ToList(),
                2 => departments.Where(d => d.Dept_ID == user.BS_Dept_ID).ToList(),
                _ => (await _context.ST_DeptRights.AsNoTracking()
                        .Where(r => r.User_No == userNo && r.Check_State == 0)
                        .Select(r => r.Dept_ID)
                        .ToListAsync())
                    .SelectMany(id => departments.Where(d => d.Dept_ID == id))
                    .ToList(),
            };
            var old = await _context.ST_DeptRights.Where(r => r.User_No == userNo).ToListAsync();
            _context.ST_DeptRights.RemoveRange(old);
            _context.ST_DeptRights.AddRange(BuildDeptRightRecords(userNo, explicitDepts, departments));
            success++;
        }

        _audit.Log(HttpContext, "deptRight", "recalc", "batch", null, null, new { success, failed });
        await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        return Api.Ok(new { success, failed });
    }

    private static bool IsAdminAccount(ST_UserInfo user) =>
        string.Equals(user.User_No, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(user.Role_No, "Administrators", StringComparison.OrdinalIgnoreCase);

    private static List<ST_DeptRight> BuildDeptRightRecords(
        string userNo,
        IReadOnlyCollection<ST_Department> explicitDepts,
        IReadOnlyCollection<ST_Department> departments)
    {
        var explicitIds = explicitDepts.Select(d => d.Dept_ID).ToHashSet();
        var byNo = departments
            .Where(d => !string.IsNullOrWhiteSpace(d.Dept_No))
            .GroupBy(d => d.Dept_No!.Trim())
            .ToDictionary(g => g.Key, g => g.First());
        var result = new Dictionary<int, ST_DeptRight>();
        foreach (var dept in explicitDepts)
        {
            result[dept.Dept_ID] = new ST_DeptRight { User_No = userNo, Dept_ID = dept.Dept_ID, Check_State = 0 };
        }

        foreach (var dept in explicitDepts)
        {
            foreach (var ancestorNo in OrgPermissionHelper.GetAncestorNos(dept.Dept_No))
            {
                if (!byNo.TryGetValue(ancestorNo, out var ancestor) || explicitIds.Contains(ancestor.Dept_ID)) continue;
                if (!result.ContainsKey(ancestor.Dept_ID))
                {
                    result[ancestor.Dept_ID] = new ST_DeptRight { User_No = userNo, Dept_ID = ancestor.Dept_ID, Check_State = 1 };
                }
            }
        }
        return result.Values.ToList();
    }

    private static int CountCovered(
        IReadOnlyCollection<ST_Department> explicitDepts,
        IReadOnlyCollection<ST_Department> departments)
    {
        var prefixes = explicitDepts
            .Where(d => !string.IsNullOrWhiteSpace(d.Dept_No))
            .Select(d => d.Dept_No!.Trim())
            .Distinct()
            .ToList();
        return departments.Count(d => prefixes.Any(prefix => d.Dept_No != null && d.Dept_No.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private async Task<int> CountCoveredDepartmentsAsync(IReadOnlyCollection<int> explicitDeptIds)
    {
        if (explicitDeptIds.Count == 0) return 0;
        var prefixes = await _context.ST_Departments.AsNoTracking()
            .Where(d => explicitDeptIds.Contains(d.Dept_ID) && d.Is_Del != true)
            .Select(d => d.Dept_No!)
            .ToListAsync();
        if (prefixes.Count == 0) return 0;
        // SQL Server 2008 兼容：前缀匹配在内存中完成
        var allNos = await _context.ST_Departments.AsNoTracking()
            .Where(d => d.Is_Del != true)
            .Select(d => d.Dept_No)
            .ToListAsync();
        return allNos.Count(no => no != null && prefixes.Any(p => no.StartsWith(p, StringComparison.Ordinal)));
    }

    private async Task<int> CountCoveredPersonsAsync(IReadOnlyCollection<ST_Department> explicitDepts)
    {
        var prefixes = explicitDepts
            .Where(d => !string.IsNullOrWhiteSpace(d.Dept_No))
            .Select(d => d.Dept_No!.Trim())
            .Distinct()
            .ToList();
        if (prefixes.Count == 0) return 0;
        // SQL Server 2008 兼容：先在内存中完成前缀匹配，再用 IN 统计人员数
        var allDepts = await _context.ST_Departments.AsNoTracking()
            .Where(d => d.Is_Del != true)
            .Select(d => new { d.Dept_ID, d.Dept_No })
            .ToListAsync();
        var matchedDeptIds = allDepts
            .Where(d => d.Dept_No != null && prefixes.Any(p => d.Dept_No.StartsWith(p, StringComparison.Ordinal)))
            .Select(d => d.Dept_ID)
            .ToList();
        if (matchedDeptIds.Count == 0) return 0;
        return await _context.ST_People
            .CountAsync(p => p.Is_Del == false && p.Dept_ID != null && matchedDeptIds.Contains(p.Dept_ID.Value));
    }
}

public class DeptRightDto
{
    public byte scopeType { get; set; }
    public int? ownerDeptId { get; set; }
    public List<int> deptIds { get; set; } = new();
    public int coveredCount { get; set; }
    public string? summary { get; set; }
    public DateTime? updatedTime { get; set; }
    public bool readonlyScope { get; set; }
}

public class DeptRightSaveRequest
{
    public byte Scope_Type { get; set; }
    public List<int> Dept_IDs { get; set; } = new();
    public string? Remark { get; set; }
}

public class DeptRightRecalcRequest
{
    public List<string> User_Nos { get; set; } = new();
}

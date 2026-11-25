using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.Text.RegularExpressions;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>角色管理：CS 单角色模型，角色是功能权限的唯一载体。</summary>
[Route("api/system/roles")]
[ApiController]
public partial class RolesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public RolesController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/roles</summary>
    [HttpGet]
    [RequirePermission("system:role:view")]
    public async Task<IActionResult> List([FromQuery] string? keyword)
    {
        var query = _context.ST_RoleInfos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(r => r.Role_No.Contains(key) || r.Role_Name.Contains(key));
        }

        var roles = await query.OrderBy(r => r.Role_No).ToListAsync();
        var memberCounts = await _context.ST_UserInfos
            .Where(u => u.Is_Del != true && u.Role_No != null)
            .GroupBy(u => u.Role_No)
            .Select(g => new { roleNo = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.roleNo!, x => x.count);
        var permissionCounts = await _context.ST_RoleRights
            .GroupBy(r => r.Role_No)
            .Select(g => new { roleNo = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.roleNo, x => x.count);

        var items = roles.Select(r => new RoleListItemDto
        {
            roleNo = r.Role_No,
            roleName = r.Role_Name,
            remark = r.Remark,
            builtIn = IsBuiltIn(r.Role_No),
            memberCount = memberCounts.TryGetValue(r.Role_No, out var members) ? members : 0,
            permissionCount = permissionCounts.TryGetValue(r.Role_No, out var permission) ? permission : 0,
        }).ToList();
        return Api.Ok(items);
    }

    /// <summary>GET api/system/roles/{roleNo}</summary>
    [HttpGet("{roleNo}")]
    [RequirePermission("system:role:view")]
    public async Task<IActionResult> Get(string roleNo)
    {
        var role = await _context.ST_RoleInfos.AsNoTracking().FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");
        var memberCount = await _context.ST_UserInfos.CountAsync(u => u.Is_Del != true && u.Role_No == roleNo);
        var permissionCount = await _context.ST_RoleRights.CountAsync(r => r.Role_No == roleNo);
        return Api.Ok(new RoleListItemDto
        {
            roleNo = role.Role_No,
            roleName = role.Role_Name,
            remark = role.Remark,
            builtIn = IsBuiltIn(role.Role_No),
            memberCount = memberCount,
            permissionCount = permissionCount,
        });
    }

    /// <summary>POST api/system/roles</summary>
    [HttpPost]
    [RequirePermission("system:role:create")]
    public async Task<IActionResult> Create([FromBody] RoleSaveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Role_No) || string.IsNullOrWhiteSpace(request.Role_Name))
        {
            return Api.BadRequest("角色编号与名称为必填项");
        }
        var roleNo = request.Role_No.Trim();
        if (!RoleNoRegex().IsMatch(roleNo)) return Api.BadRequest("角色编号需为 2-20 位字母、数字或下划线");
        if (request.Role_Name.Trim().Length is <= 0 or > 20) return Api.BadRequest("角色名称需为 1-20 个字符");
        if (await _context.ST_RoleInfos.AnyAsync(r => r.Role_No == roleNo)) return Api.Conflict("角色编号已存在");
        if (await _context.ST_RoleInfos.AnyAsync(r => r.Role_Name == request.Role_Name)) return Api.Conflict("角色名称已存在");

        var role = new ST_RoleInfo
        {
            Role_No = roleNo,
            Role_Name = request.Role_Name.Trim(),
            Remark = request.RemartTruncated(),
        };
        _context.ST_RoleInfos.Add(role);
        _audit.Log(HttpContext, "role", "create", "role", roleNo, null, role);
        await _context.SaveChangesAsync();
        return Api.Ok(new { roleNo });
    }

    /// <summary>PUT api/system/roles/{roleNo}</summary>
    [HttpPut("{roleNo}")]
    [RequirePermission("system:role:update")]
    public async Task<IActionResult> Update(string roleNo, [FromBody] RoleSaveRequest request)
    {
        var role = await _context.ST_RoleInfos.FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");
        if (IsBuiltIn(roleNo))
        {
            if (!string.Equals(role.Role_Name, request.Role_Name?.Trim(), StringComparison.Ordinal))
            {
                return Api.StateConflict("系统内置角色不允许改名");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Role_Name))
        {
            var roleName = request.Role_Name.Trim();
            if (await _context.ST_RoleInfos.AnyAsync(r => r.Role_Name == roleName && r.Role_No != roleNo))
            {
                return Api.Conflict("角色名称已存在");
            }
            role.Role_Name = roleName;
        }
        if (request?.Remark != null) role.Remark = request.RemartTruncated();

        _audit.Log(HttpContext, "role", "update", "role", roleNo, null, role);
        await _context.SaveChangesAsync();
        return Api.Ok(new { roleNo });
    }

    /// <summary>DELETE api/system/roles/{roleNo}：无成员时物理删除角色与授权记录，审计中保留快照。</summary>
    [HttpDelete("{roleNo}")]
    [RequirePermission("system:role:delete")]
    public async Task<IActionResult> Delete(string roleNo)
    {
        if (IsBuiltIn(roleNo)) return Api.StateConflict("Administrators 为系统内置角色，不允许删除");
        var role = await _context.ST_RoleInfos.FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");

        var members = await _context.ST_UserInfos
            .Where(u => u.Role_No == roleNo && u.Is_Del != true)
            .OrderBy(u => u.User_No)
            .Take(10)
            .Select(u => u.User_No)
            .ToListAsync();
        if (members.Count > 0)
        {
            return Api.StateConflict($"该角色仍有操作员引用：{string.Join("、", members)}");
        }

        var rights = await _context.ST_RoleRights.Where(r => r.Role_No == roleNo).ToListAsync();
        _audit.Log(HttpContext, "role", "delete", "role", roleNo, role, new { rights.Count });
        _context.ST_RoleRights.RemoveRange(rights);
        _context.ST_RoleInfos.Remove(role);
        await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        return Api.Ok(new { roleNo, deleted = true });
    }

    /// <summary>POST api/system/roles/{roleNo}/copy</summary>
    [HttpPost("{roleNo}/copy")]
    [RequirePermission("system:role:copy")]
    public async Task<IActionResult> Copy(string roleNo, [FromBody] RoleCopyRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NewRole_No) || string.IsNullOrWhiteSpace(request.NewRole_Name))
        {
            return Api.BadRequest("新角色编号与名称为必填项");
        }
        var source = await _context.ST_RoleInfos.AsNoTracking().FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (source == null) return Api.NotFound("源角色不存在");
        if (await _context.ST_RoleInfos.AnyAsync(r => r.Role_No == request.NewRole_No)) return Api.Conflict("新角色编号已存在");
        if (await _context.ST_RoleInfos.AnyAsync(r => r.Role_Name == request.NewRole_Name)) return Api.Conflict("新角色名称已存在");

        var rights = await _context.ST_RoleRights.AsNoTracking().Where(r => r.Role_No == roleNo).ToListAsync();
        var newRole = new ST_RoleInfo
        {
            Role_No = request.NewRole_No,
            Role_Name = request.NewRole_Name,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? source.Remark : request.RemartTruncated(),
        };
        _context.ST_RoleInfos.Add(newRole);
        _context.ST_RoleRights.AddRange(rights.Select(r => new ST_RoleRight
        {
            Role_No = newRole.Role_No,
            Menu_ID = r.Menu_ID,
            Right_Values = r.Right_Values,
        }));
        _audit.Log(HttpContext, "role", "copy", "role", source.Role_No, null, newRole);
        await _context.SaveChangesAsync();
        return Api.Ok(new { roleNo = newRole.Role_No, copiedRightCount = rights.Count });
    }

    /// <summary>GET api/system/roles/{roleNo}/members</summary>
    [HttpGet("{roleNo}/members")]
    [RequirePermission("system:role:view")]
    public async Task<IActionResult> Members(string roleNo)
    {
        var role = await _context.ST_RoleInfos.AsNoTracking().FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");
        var members = await _context.ST_UserInfos.AsNoTracking()
            .Where(u => u.Role_No == roleNo && u.Is_Del != true)
            .OrderBy(u => u.User_No)
            .Select(u => new { u.User_No, u.User_Name, deptId = u.BS_Dept_ID })
            .ToListAsync();
        return Api.Ok(new { roleNo, members });
    }

    private static bool IsBuiltIn(string roleNo) =>
        string.Equals(roleNo, "Administrators", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^[A-Za-z0-9_]{2,20}$")]
    private static partial Regex RoleNoRegex();
}

public class RoleListItemDto
{
    public string roleNo { get; set; } = null!;
    public string roleName { get; set; } = null!;
    public string? remark { get; set; }
    public bool builtIn { get; set; }
    public int memberCount { get; set; }
    public int permissionCount { get; set; }
}

public class RoleSaveRequest
{
    public string Role_No { get; set; } = null!;
    public string Role_Name { get; set; } = null!;
    public string? Remark { get; set; }

    public string RemartTruncated() => Remark == null ? string.Empty : Remark.Length > 50 ? Remark[..50] : Remark;
}

public class RoleCopyRequest
{
    public string NewRole_No { get; set; } = null!;
    public string NewRole_Name { get; set; } = null!;
    public string? Remark { get; set; }

    public string RemartTruncated() => Remark == null ? string.Empty : Remark.Length > 50 ? Remark[..50] : Remark;
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>菜单与操作权限：CS 权限四表的 BS 管理入口，按子系统整体替换授权。</summary>
[Route("api/system")]
[ApiController]
public class MenuRightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public MenuRightsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/subsystems</summary>
    [HttpGet("subsystems")]
    [RequirePermission("system:permission:view")]
    public async Task<IActionResult> Subsystems()
    {
        var items = await _context.ST_SubSystems.AsNoTracking()
            .OrderBy(s => s.Index_No)
            .ThenBy(s => s.System_No)
            .Select(s => new
            {
                systemNo = s.System_No,
                systemName = s.System_Name,
                indexNo = s.Index_No,
            })
            .ToListAsync();
        return Api.Ok(items);
    }

    /// <summary>
    /// GET api/system/menus/tree?systemNo=BS
    /// 节点同时带回可分配位串与位名称，矩阵页面一次请求即可渲染，无需按菜单逐个查询。
    /// </summary>
    [HttpGet("menus/tree")]
    [RequirePermission("system:permission:view")]
    public async Task<IActionResult> MenuTree([FromQuery] string systemNo = "BS")
    {
        if (string.IsNullOrWhiteSpace(systemNo)) return Api.BadRequest("子系统编号不能为空");
        var menus = await _context.ST_MenuInfos.AsNoTracking()
            .Where(m => m.System_No == systemNo)
            .OrderBy(m => m.Menu_Index)
            .ThenBy(m => m.Menu_ID)
            .ToListAsync();
        var menuIds = menus.Select(m => m.Menu_ID).ToList();
        var rightInfos = await _context.ST_RightInfos.AsNoTracking()
            .Where(r => menuIds.Contains(r.Menu_ID))
            .ToDictionaryAsync(r => r.Menu_ID);

        // ST_RightValue 主键为 (Right_ID, Form_Name)：同一编号在不同菜单下语义不同，
        // 必须按 Form_Name 取名称，不能按 Right_ID 全局取名。
        var formNames = rightInfos.Values.Select(r => r.Form_Name).Where(f => f != null).Distinct().ToList();
        var rightValues = await _context.ST_RightValues.AsNoTracking()
            .Where(v => formNames.Contains(v.Form_Name))
            .ToListAsync();
        var valueLookup = rightValues.ToLookup(v => v.Form_Name);
        var maps = await _context.BS_RightCodeMaps.AsNoTracking()
            .Where(m => menuIds.Contains(m.Menu_ID))
            .ToListAsync();
        var mapLookup = maps.ToLookup(m => m.Menu_ID);

        var items = menus.Select(m =>
        {
            rightInfos.TryGetValue(m.Menu_ID, out var info);
            var available = (info?.Right_Values ?? string.Empty).Trim();
            var rights = new List<RightDto>();
            if (info != null && !string.IsNullOrEmpty(info.Form_Name))
            {
                foreach (var ch in available.Distinct())
                {
                    var rightId = ch.ToString();
                    var value = valueLookup[info.Form_Name].FirstOrDefault(v => v.Right_ID == rightId);
                    if (value == null) continue;
                    rights.Add(new RightDto
                    {
                        rightId = rightId,
                        rightName = value.Right_Name,
                        permissionCode = mapLookup[m.Menu_ID].FirstOrDefault(x => x.Right_ID == rightId)?.Permission_Code,
                    });
                }
            }
            return new MenuTreeNodeDto
            {
                menuId = m.Menu_ID,
                systemNo = m.System_No,
                menuNo = m.Menu_No,
                menuName = m.Menu_Name,
                parentId = m.Para_ID,
                formName = m.Form_Name,
                index = m.Menu_Index,
                assignable = available.Length > 0,
                rightValues = available,
                rights = rights,
            };
        }).ToList();
        return Api.Ok(items);
    }

    /// <summary>GET api/system/menus/{menuId}/rights</summary>
    [HttpGet("menus/{menuId}/rights")]
    [RequirePermission("system:permission:view")]
    public async Task<IActionResult> MenuRights(int menuId)
    {
        var menu = await _context.ST_MenuInfos.AsNoTracking().FirstOrDefaultAsync(m => m.Menu_ID == menuId);
        if (menu == null) return Api.NotFound("菜单不存在");
        var rightInfo = await _context.ST_RightInfos.AsNoTracking().FirstOrDefaultAsync(r => r.Menu_ID == menuId);
        if (rightInfo == null) return Api.Ok(new { menuId, rights = Array.Empty<RightDto>() });

        var ids = (rightInfo.Right_Values ?? string.Empty).Trim().ToCharArray().Select(c => c.ToString()).ToList();
        var values = await _context.ST_RightValues.AsNoTracking()
            .Where(v => v.Form_Name == rightInfo.Form_Name && ids.Contains(v.Right_ID))
            .OrderBy(v => v.Right_ID)
            .ToListAsync();
        var maps = await _context.BS_RightCodeMaps.AsNoTracking()
            .Where(m => m.Menu_ID == menuId)
            .ToDictionaryAsync(m => m.Right_ID);

        var rights = values.Select(v => new RightDto
        {
            rightId = v.Right_ID,
            rightName = v.Right_Name,
            permissionCode = maps.TryGetValue(v.Right_ID, out var map) ? map.Permission_Code : null,
        }).ToList();
        return Api.Ok(new { menuId, rights });
    }

    /// <summary>GET api/system/roles/{roleNo}/rights?systemNo=BS</summary>
    [HttpGet("roles/{roleNo}/rights")]
    [RequirePermission("system:permission:view")]
    public async Task<IActionResult> RoleRights(string roleNo, [FromQuery] string systemNo = "BS")
    {
        if (string.IsNullOrWhiteSpace(systemNo)) return Api.BadRequest("子系统编号不能为空");
        var role = await _context.ST_RoleInfos.AsNoTracking().FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");

        var menus = await _context.ST_MenuInfos.AsNoTracking()
            .Where(m => m.System_No == systemNo)
            .Select(m => m.Menu_ID)
            .ToListAsync();
        var rights = await _context.ST_RoleRights.AsNoTracking()
            .Where(r => r.Role_No == roleNo && menus.Contains(r.Menu_ID))
            .ToListAsync();
        return Api.Ok(new
        {
            systemNo,
            items = rights.Select(r => new RoleRightItemDto
            {
                menuId = r.Menu_ID,
                rightIds = (r.Right_Values ?? string.Empty).Trim().ToCharArray().Select(c => c.ToString()).ToList(),
            }).ToList(),
        });
    }

    /// <summary>PUT api/system/roles/{roleNo}/rights：按子系统整体替换，失败关闭。</summary>
    [HttpPut("roles/{roleNo}/rights")]
    [RequirePermission("system:permission:assign")]
    public async Task<IActionResult> SaveRoleRights(string roleNo, [FromBody] RoleRightsSaveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.System_No)) return Api.BadRequest("子系统编号不能为空");
        var role = await _context.ST_RoleInfos.FirstOrDefaultAsync(r => r.Role_No == roleNo);
        if (role == null) return Api.NotFound("角色不存在");

        var menus = await _context.ST_MenuInfos.AsNoTracking()
            .Where(m => m.System_No == request.System_No)
            .ToListAsync();
        var menuIds = menus.Select(m => m.Menu_ID).ToHashSet();
        var rightInfos = await _context.ST_RightInfos.AsNoTracking()
            .Where(r => menuIds.Contains(r.Menu_ID))
            .ToDictionaryAsync(r => r.Menu_ID, r => r.Right_Values ?? string.Empty);

        var saved = 0;
        var skipped = 0;
        var inserts = new List<ST_RoleRight>();
        foreach (var item in request.Items)
        {
            if (item == null || !menuIds.Contains(item.menuId))
            {
                skipped += item == null ? 0 : item.rightIds.Count;
                continue;
            }
            var allowed = rightInfos.TryGetValue(item.menuId, out var values) ? values : string.Empty;
            var normalized = OrgPermissionHelper.NormalizeRightValues(string.Concat(item.rightIds), allowed);
            skipped += item.rightIds.Count - normalized.Length;
            if (normalized.Length == 0) continue;
            inserts.Add(new ST_RoleRight
            {
                Role_No = roleNo,
                Menu_ID = item.menuId,
                Right_Values = normalized,
            });
            saved++;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var oldRights = await _context.ST_RoleRights
            .Where(r => r.Role_No == roleNo && menuIds.Contains(r.Menu_ID))
            .ToListAsync();
        _context.ST_RoleRights.RemoveRange(oldRights);
        _context.ST_RoleRights.AddRange(inserts);
        _audit.Log(HttpContext, "permission", "saveRoleRights", "role", roleNo, new { oldCount = oldRights.Count },
            new { saved, skipped, systemNo = request.System_No });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Api.Ok(new { savedCount = saved, skippedCount = skipped, version });
    }

    /// <summary>GET api/system/permissions/me</summary>
    [HttpGet("permissions/me")]
    public async Task<IActionResult> MyPermissions()
    {
        var userNo = User.GetUserNo();
        if (string.IsNullOrWhiteSpace(userNo)) return Api.Unauthorized();
        var permissions = await _permission.GetPermissionsAsync(userNo);
        return Api.Ok(new
        {
            userNo,
            permissions,
            version = await _permission.GetVersionAsync(),
        });
    }
}

public class MenuTreeNodeDto
{
    public int menuId { get; set; }
    public string systemNo { get; set; } = null!;
    public string menuNo { get; set; } = null!;
    public string menuName { get; set; } = null!;
    public int? parentId { get; set; }
    public string? formName { get; set; }
    public byte? index { get; set; }
    public bool assignable { get; set; }
    /// <summary>可分配位串，ST_RightInfo.Right_Values 的原值，如 "012345"</summary>
    public string rightValues { get; set; } = string.Empty;
    /// <summary>该菜单的权限位明细，顺序与 rightValues 去重后的字符顺序一致</summary>
    public List<RightDto> rights { get; set; } = new();
}

public class RightDto
{
    public string rightId { get; set; } = null!;
    public string rightName { get; set; } = null!;
    public string? permissionCode { get; set; }
}

public class RoleRightItemDto
{
    public int menuId { get; set; }
    public List<string> rightIds { get; set; } = new();
}

public class RoleRightsSaveRequest
{
    public string System_No { get; set; } = "BS";
    public List<RoleRightItemDto> Items { get; set; } = new();
}

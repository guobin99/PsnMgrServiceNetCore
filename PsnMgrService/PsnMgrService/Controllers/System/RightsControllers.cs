using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>机台列表：设备权限与出纳授权共用的候选设备来源。</summary>
[Route("api/system/devices")]
[ApiController]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DevicesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>GET api/system/devices</summary>
    [HttpGet]
    [RequirePermission("system:deviceRight:view")]
    public async Task<IActionResult> List(
        [FromQuery] string? systemNo,
        [FromQuery] string? areaNo,
        [FromQuery] string? mocType,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        var query = _context.ST_MOCs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(systemNo)) query = query.Where(m => m.System_No == systemNo);
        if (!string.IsNullOrWhiteSpace(areaNo)) query = query.Where(m => m.Area_No == areaNo);
        if (!string.IsNullOrWhiteSpace(mocType)) query = query.Where(m => m.MOC_Type == mocType);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(m => m.MOC_No.Contains(key) || m.MOC_Name.Contains(key));
        }

        // SQL Server 2008 兼容：内存分页
        var allItems = await query
            .OrderBy(m => m.System_No)
            .ThenBy(m => m.MOC_No)
            .Select(m => new DeviceDto
            {
                mocNo = m.MOC_No,
                mocName = m.MOC_Name,
                systemNo = m.System_No,
                areaNo = m.Area_No,
                mocType = m.MOC_Type,
                commStatus = m.Comm_Status,
                downloadStatus = m.Download_Status,
            })
            .ToListAsync();
        var total = allItems.Count;
        var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Api.Ok(new { total, items });
    }
}

/// <summary>设备权限：BS_UserMOCRight 扩展授权；出纳机与 ST_UserInfo.User_Moc 双向同步。</summary>
[Route("api/system/device-rights")]
[ApiController]
public class DeviceRightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public DeviceRightsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/device-rights/{userNo}</summary>
    [HttpGet("{userNo}")]
    [RequirePermission("system:deviceRight:view")]
    public async Task<IActionResult> Get(string userNo)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        var rights = await _context.BS_UserMOCRights.AsNoTracking()
            .Where(r => r.User_No == userNo)
            .OrderBy(r => r.MOC_No)
            .ToListAsync();
        return Api.Ok(new
        {
            userNo,
            isCashier = user.User_Type == 2,
            userMoc = user.User_Moc,
            items = rights.Select(r => new DeviceRightItemDto
            {
                mocNo = r.MOC_No,
                rightLevel = r.Right_Level,
            }).ToList(),
        });
    }

    /// <summary>PUT api/system/device-rights/{userNo}：整体替换；出纳员同步 User_Moc。</summary>
    [HttpPut("{userNo}")]
    [RequirePermission("system:deviceRight:assign")]
    public async Task<IActionResult> Save(string userNo, [FromBody] DeviceRightsSaveRequest request)
    {
        if (request?.Items == null) return Api.BadRequest("缺少设备授权数据");
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null || user.Is_Del == true) return Api.NotFound("操作员不存在或已注销");

        var requestedNos = request.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Moc_No))
            .Select(i => i.Moc_No.Trim())
            .Distinct()
            .ToList();
        var devices = await _context.ST_MOCs.AsNoTracking()
            .Where(m => requestedNos.Contains(m.MOC_No))
            .ToListAsync();
        if (devices.Count != requestedNos.Count) return Api.Conflict("存在无效或已删除的机台");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var old = await _context.BS_UserMOCRights.Where(r => r.User_No == userNo).ToListAsync();
        _context.BS_UserMOCRights.RemoveRange(old);
        _context.BS_UserMOCRights.AddRange(request.Items.Select(item => new BS_UserMOCRight
        {
            User_No = userNo,
            MOC_No = item.Moc_No.Trim(),
            Right_Level = item.Right_Level is 1 or 2 ? item.Right_Level.Value : (byte)1,
            Update_Time = DateTime.Now,
        }));

        if (user.User_Type == 2)
        {
            user.User_Moc = OrgPermissionHelper.ToCsMocString(requestedNos);
        }

        _audit.Log(HttpContext, "deviceRight", "save", "operator", userNo,
            new { oldCount = old.Count, userMoc = user.User_Moc },
            new { savedCount = request.Items.Count, userMoc = user.User_Moc });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Api.Ok(new { savedCount = request.Items.Count, userMoc = user.User_Moc, version });
    }

    /// <summary>POST api/system/device-rights/batch：同一批机台授予多个操作员。</summary>
    [HttpPost("batch")]
    [RequirePermission("system:deviceRight:assign")]
    public async Task<IActionResult> Batch([FromBody] DeviceRightsBatchRequest request)
    {
        if (request?.User_Nos == null || request.Items == null || request.User_Nos.Count == 0)
        {
            return Api.BadRequest("请指定操作员与设备授权");
        }
        var users = await _context.ST_UserInfos
            .Where(u => request.User_Nos.Contains(u.User_No) && u.Is_Del != true)
            .ToListAsync();
        if (users.Count != request.User_Nos.Count) return Api.Conflict("存在无效操作员");

        var requestedNos = request.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Moc_No))
            .Select(i => i.Moc_No.Trim())
            .Distinct()
            .ToList();
        var exists = await _context.ST_MOCs.CountAsync(m => requestedNos.Contains(m.MOC_No));
        if (exists != requestedNos.Count) return Api.Conflict("存在无效机台");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        foreach (var user in users)
        {
            var old = await _context.BS_UserMOCRights.Where(r => r.User_No == user.User_No).ToListAsync();
            _context.BS_UserMOCRights.RemoveRange(old);
            _context.BS_UserMOCRights.AddRange(request.Items.Select(item => new BS_UserMOCRight
            {
                User_No = user.User_No,
                MOC_No = item.Moc_No.Trim(),
                Right_Level = item.Right_Level is 1 or 2 ? item.Right_Level.Value : (byte)1,
                Update_Time = DateTime.Now,
            }));
            if (user.User_Type == 2) user.User_Moc = OrgPermissionHelper.ToCsMocString(requestedNos);
        }

        _audit.Log(HttpContext, "deviceRight", "batchSave", "batch", null, null,
            new { userCount = users.Count, deviceCount = requestedNos.Count });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Api.Ok(new { userCount = users.Count, deviceCount = requestedNos.Count, version });
    }
}

/// <summary>子系统权限：操作员级授权存 BS_UserSubSysRight，人员级 ST_SubSysRight 只读。</summary>
[Route("api/system/subsystem-rights")]
[ApiController]
public class SubsystemRightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public SubsystemRightsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/subsystem-rights/options</summary>
    [HttpGet("options")]
    [RequirePermission("system:subsystemRight:view")]
    public async Task<IActionResult> Options([FromQuery] string? roleNo)
    {
        var subsystems = await _context.ST_SubSystems.AsNoTracking()
            .OrderBy(s => s.Index_No)
            .ThenBy(s => s.System_No)
            .ToListAsync();
        var menuCounts = new Dictionary<string, int>();
        if (!string.IsNullOrWhiteSpace(roleNo))
        {
            menuCounts = await _context.ST_RoleRights.AsNoTracking()
                .Where(r => r.Role_No == roleNo)
                .Join(_context.ST_MenuInfos.AsNoTracking(),
                    r => r.Menu_ID,
                    m => m.Menu_ID,
                    (r, m) => new { m.System_No })
                .GroupBy(x => x.System_No)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        return Api.Ok(subsystems.Select(s => new SubsystemOptionDto
        {
            systemNo = s.System_No,
            systemName = s.System_Name,
            roleMenuCount = menuCounts.TryGetValue(s.System_No, out var count) ? count : 0,
        }).ToList());
    }

    /// <summary>GET api/system/subsystem-rights/{userNo}</summary>
    [HttpGet("{userNo}")]
    [RequirePermission("system:subsystemRight:view")]
    public async Task<IActionResult> Get(string userNo)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        var isAdministrator = string.Equals(user.Role_No, "Administrators", StringComparison.OrdinalIgnoreCase);
        var allowed = isAdministrator
            ? await _context.ST_SubSystems.Select(s => s.System_No).ToListAsync()
            : await _context.BS_UserSubSysRights.AsNoTracking()
                .Where(r => r.User_No == userNo && r.Is_Allow)
                .Select(r => r.System_No)
                .ToListAsync();
        return Api.Ok(new { userNo, systemNos = allowed, unrestricted = isAdministrator });
    }

    /// <summary>PUT api/system/subsystem-rights/{userNo}：整体替换；与角色菜单权限取交集生效。</summary>
    [HttpPut("{userNo}")]
    [RequirePermission("system:subsystemRight:assign")]
    public async Task<IActionResult> Save(string userNo, [FromBody] SubsystemRightsSaveRequest request)
    {
        if (request?.System_Nos == null) return Api.BadRequest("缺少子系统授权数据");
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null || user.Is_Del == true) return Api.NotFound("操作员不存在或已注销");

        var systems = request.System_Nos.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var knownSystems = await _context.ST_SubSystems.AsNoTracking()
            .Where(s => systems.Contains(s.System_No))
            .Select(s => s.System_No)
            .ToListAsync();
        if (knownSystems.Count != systems.Count) return Api.Conflict("存在无效子系统");

        var roleMenuSystems = await _context.ST_RoleRights.AsNoTracking()
            .Where(r => r.Role_No == user.Role_No)
            .Join(_context.ST_MenuInfos.AsNoTracking(), r => r.Menu_ID, m => m.Menu_ID, (r, m) => m.System_No)
            .Distinct()
            .ToListAsync();
        var conflictCount = systems.Count(s => !roleMenuSystems.Contains(s));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var old = await _context.BS_UserSubSysRights.Where(r => r.User_No == userNo).ToListAsync();
        _context.BS_UserSubSysRights.RemoveRange(old);
        _context.BS_UserSubSysRights.AddRange(knownSystems.Select(systemNo => new BS_UserSubSysRight
        {
            User_No = userNo,
            System_No = systemNo,
            Is_Allow = true,
            Update_Time = DateTime.Now,
        }));

        _audit.Log(HttpContext, "subsystemRight", "save", "operator", userNo,
            new { oldCount = old.Count }, new { savedCount = knownSystems.Count, conflictCount });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Api.Ok(new { savedCount = knownSystems.Count, conflictCount, version });
    }
}

public class DeviceDto
{
    public string mocNo { get; set; } = null!;
    public string mocName { get; set; } = null!;
    public string systemNo { get; set; } = null!;
    public string? areaNo { get; set; }
    public string? mocType { get; set; }
    public byte? commStatus { get; set; }
    public byte? downloadStatus { get; set; }
}

public class DeviceRightItemDto
{
    public string mocNo { get; set; } = null!;
    public byte rightLevel { get; set; }
}

public class DeviceRightsSaveRequest
{
    public List<DeviceRightSaveItem> Items { get; set; } = new();
}

public class DeviceRightSaveItem
{
    public string Moc_No { get; set; } = null!;
    public byte? Right_Level { get; set; }
}

public class DeviceRightsBatchRequest
{
    public List<string> User_Nos { get; set; } = new();
    public List<DeviceRightSaveItem> Items { get; set; } = new();
}

public class SubsystemOptionDto
{
    public string systemNo { get; set; } = null!;
    public string systemName { get; set; } = null!;
    public int roleMenuCount { get; set; }
}

public class SubsystemRightsSaveRequest
{
    public List<string> System_Nos { get; set; } = new();
}

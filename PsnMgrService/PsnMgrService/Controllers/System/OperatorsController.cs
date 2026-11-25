using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>操作员管理：ST_UserInfo 全生命周期维护，写操作只更新提交列。</summary>
[Route("api/system/operators")]
[ApiController]
public partial class OperatorsController : ControllerBase
{
    private static readonly TimeSpan CsOnlineWindow = TimeSpan.FromHours(24);
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public OperatorsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/operators</summary>
    [HttpGet]
    [RequirePermission("system:operator:view")]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string? roleNo,
        [FromQuery] int? deptId,
        [FromQuery] byte? userType,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);

        var query = _context.ST_UserInfos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(u => u.User_No.Contains(key) || u.User_Name.Contains(key));
        }
        if (!string.IsNullOrWhiteSpace(roleNo)) query = query.Where(u => u.Role_No == roleNo);
        if (userType.HasValue) query = query.Where(u => u.User_Type == userType);
        if (string.Equals(status, "deleted", StringComparison.OrdinalIgnoreCase)) query = query.Where(u => u.Is_Del == true);
        else if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)) query = query.Where(u => u.Is_Del != true);

        if (deptId.HasValue)
        {
            var selectedDept = await _context.ST_Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Dept_ID == deptId.Value);
            if (selectedDept == null || string.IsNullOrWhiteSpace(selectedDept.Dept_No))
            {
                return Api.NotFound("选择的部门不存在");
            }
            var deptNo = selectedDept.Dept_No.Trim();
            var deptIds = await _context.ST_Departments.AsNoTracking()
                .Where(d => d.Is_Del != true && d.Dept_No != null && d.Dept_No.StartsWith(deptNo))
                .Select(d => d.Dept_ID)
                .ToListAsync();
            query = query.Where(u => u.BS_Dept_ID != null && deptIds.Contains(u.BS_Dept_ID!.Value));
        }

        // SQL Server 2008 兼容：EF Core 9 的 OFFSET/FETCH 分页不受兼容级别控制，改为内存分页
        var allRows = await query
            .OrderBy(u => u.User_No)
            .ToListAsync();
        var total = allRows.Count;
        var rows = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var roleNos = rows.Select(u => u.Role_No).Where(r => r != null).Distinct().ToList();
        var roles = await _context.ST_RoleInfos.AsNoTracking()
            .Where(r => roleNos.Contains(r.Role_No))
            .ToDictionaryAsync(r => r.Role_No, r => r.Role_Name);
        var deptIds2 = rows.Where(u => u.BS_Dept_ID.HasValue).Select(u => u.BS_Dept_ID!.Value).Distinct().ToList();
        var depts = await _context.ST_Departments.AsNoTracking()
            .Where(d => deptIds2.Contains(d.Dept_ID))
            .ToDictionaryAsync(d => d.Dept_ID, d => d.Dept_Name);

        var items = rows.Select(u => new OperatorListItemDto
        {
            userNo = u.User_No,
            userName = u.User_Name,
            roleNo = u.Role_No,
            roleName = u.Role_No != null && roles.TryGetValue(u.Role_No, out var name) ? name : null,
            deptId = u.BS_Dept_ID,
            deptName = u.BS_Dept_ID.HasValue && depts.TryGetValue(u.BS_Dept_ID.Value, out var deptName) ? deptName : null,
            userType = u.User_Type,
            status = u.Is_Del == true ? "deleted" : "normal",
            csOnline = IsCsOnline(u),
            computerName = u.Computer_Name,
            recentLogin = u.Login_Time,
            remark = u.Remark,
        }).ToList();

        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/system/operators/{userNo}</summary>
    [HttpGet("{userNo}")]
    [RequirePermission("system:operator:view")]
    public async Task<IActionResult> Get(string userNo)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");

        var roleName = await _context.ST_RoleInfos.AsNoTracking()
            .Where(r => r.Role_No == user.Role_No)
            .Select(r => r.Role_Name)
            .FirstOrDefaultAsync();
        var deptName = user.BS_Dept_ID == null
            ? null
            : await _context.ST_Departments.AsNoTracking()
                .Where(d => d.Dept_ID == user.BS_Dept_ID)
                .Select(d => d.Dept_Name)
                .FirstOrDefaultAsync();
        var scope = await _context.BS_UserDataScopes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.User_No == userNo);
        var subsystems = await _context.BS_UserSubSysRights.AsNoTracking()
            .Where(s => s.User_No == userNo && s.Is_Allow)
            .Select(s => s.System_No)
            .ToListAsync();

        return Api.Ok(new OperatorDetailDto
        {
            userNo = user.User_No,
            userName = user.User_Name,
            roleNo = user.Role_No,
            roleName = roleName,
            deptId = user.BS_Dept_ID,
            deptName = deptName,
            userType = user.User_Type,
            status = user.Is_Del == true ? "deleted" : "normal",
            csOnline = IsCsOnline(user),
            computerName = user.Computer_Name,
            recentLogin = user.Login_Time,
            remark = user.Remark,
            dataScopeType = scope?.Scope_Type ?? 3,
            dataScopeSummary = scope?.Summary,
            subsystems = subsystems,
        });
    }

    /// <summary>POST api/system/operators</summary>
    [HttpPost]
    [RequirePermission("system:operator:create")]
    public async Task<IActionResult> Create([FromBody] OperatorSaveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.User_No)
            || string.IsNullOrWhiteSpace(request.User_Name) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Api.BadRequest("编号、姓名与初始密码为必填项");
        }
        var userNo = request.User_No.Trim();
        if (!UserNoRegex().IsMatch(userNo)) return Api.BadRequest("编号需为 2-20 位字母、数字或下划线");
        if (request.User_Name.Trim().Length is <= 0 or > 20) return Api.BadRequest("姓名需为 1-20 个字符");
        if (!OrgPermissionHelper.IsStrongPassword(request.Password, userNo)) return Api.BadRequest("初始密码需为 8-20 位且同时包含字母与数字");
        if (request.Dept_ID == null) return Api.BadRequest("归属部门为必填项");

        var role = await _context.ST_RoleInfos.FirstOrDefaultAsync(r => r.Role_No == request.Role_No);
        if (role == null) return Api.Conflict("所选角色不存在");
        var dept = await _context.ST_Departments.FirstOrDefaultAsync(d => d.Dept_ID == request.Dept_ID && d.Is_Del != true);
        if (dept == null) return Api.Conflict("所选归属部门不存在或已停用");
        if (await _context.ST_UserInfos.AnyAsync(u => u.User_No == userNo)) return Api.Conflict("操作员编号已存在");

        var user = new ST_UserInfo
        {
            User_No = userNo,
            User_Name = request.User_Name.Trim(),
            User_Pwd = new EncryptDB().EncryptStr(request.Password),
            User_Type = request.User_Type ?? 3,
            Role_No = request.Role_No,
            BS_Dept_ID = request.Dept_ID,
            Is_Del = request.Is_Del ?? false,
            Current_Menu = "ST",
            Remark = request.Remark,
            BS_Pwd_Update_Time = DateTime.Now,
        };
        _context.ST_UserInfos.Add(user);
        _context.BS_PwdHistories.Add(new BS_PwdHistory
        {
            User_No = userNo,
            Pwd_Cipher = user.User_Pwd,
            Create_Time = DateTime.Now,
        });
        _audit.Log(HttpContext, "operator", "create", "operator", userNo, null, new { userNo, request.User_Name, request.Role_No, request.Dept_ID });
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo });
    }

    /// <summary>PUT api/system/operators/{userNo}</summary>
    [HttpPut("{userNo}")]
    [RequirePermission("system:operator:update")]
    public async Task<IActionResult> Update(string userNo, [FromBody] OperatorSaveRequest request)
    {
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        if (user.Is_Del == true) return Api.StateConflict("已注销操作员不能编辑，请先恢复");

        var before = new { user.User_Name, user.Role_No, user.BS_Dept_ID, user.User_Type, user.Remark };
        if (!string.IsNullOrWhiteSpace(request.User_Name)) user.User_Name = request.User_Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Role_No))
        {
            if (string.Equals(userNo, "Admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(user.Role_No, request.Role_No, StringComparison.Ordinal))
            {
                return Api.StateConflict("Admin 账号不允许修改角色");
            }
            if (!await _context.ST_RoleInfos.AnyAsync(r => r.Role_No == request.Role_No))
            {
                return Api.Conflict("所选角色不存在");
            }
            user.Role_No = request.Role_No;
        }
        if (request.Dept_ID.HasValue)
        {
            var dept = await _context.ST_Departments.FirstOrDefaultAsync(d => d.Dept_ID == request.Dept_ID && d.Is_Del != true);
            if (dept == null) return Api.Conflict("所选归属部门不存在或已停用");
            user.BS_Dept_ID = request.Dept_ID;
        }
        if (request.User_Type.HasValue && user.User_Type != request.User_Type)
        {
            if (user.User_Type == 2 || request.User_Type == 2)
            {
                return Api.StateConflict("出纳员类型请在出纳授权管理中维护");
            }
            user.User_Type = request.User_Type;
        }
        if (request.Remark != null) user.Remark = request.RemartTruncated();
        if (request.Is_Del.HasValue && userNo != "Admin") user.Is_Del = request.Is_Del;

        _audit.Log(HttpContext, "operator", "update", "operator", userNo, before,
            new { user.User_Name, user.Role_No, user.BS_Dept_ID, user.User_Type, user.Remark });
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo });
    }

    /// <summary>DELETE api/system/operators/{userNo}：逻辑注销并清理授权、吊销令牌。</summary>
    [HttpDelete("{userNo}")]
    [RequirePermission("system:operator:delete")]
    public async Task<IActionResult> Delete(string userNo)
    {
        var currentUserNo = User.GetUserNo();
        if (string.Equals(currentUserNo, userNo, StringComparison.OrdinalIgnoreCase))
        {
            return Api.StateConflict("不能注销当前登录账号");
        }
        if (string.Equals(userNo, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Api.StateConflict("Admin 为内置保护账号，不允许注销");
        }

        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        if (user.Is_Del == true) return Api.StateConflict("操作员已注销");

        user.Is_Del = true;
        _context.ST_DeptRights.RemoveRange(_context.ST_DeptRights.Where(r => r.User_No == userNo));
        _context.BS_UserSubSysRights.RemoveRange(_context.BS_UserSubSysRights.Where(r => r.User_No == userNo));
        _context.BS_UserMOCRights.RemoveRange(_context.BS_UserMOCRights.Where(r => r.User_No == userNo));
        _context.BS_UserDataScopes.RemoveRange(_context.BS_UserDataScopes.Where(r => r.User_No == userNo));
        var tokens = await _context.BS_AuthTokens.Where(t => t.User_No == userNo).ToListAsync();
        foreach (var token in tokens) token.Revoked = true;

        _audit.Log(HttpContext, "operator", "delete", "operator", userNo, new { isDel = false }, new { isDel = true });
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo, isDeleted = true });
    }

    /// <summary>POST api/system/operators/{userNo}/restore</summary>
    [HttpPost("{userNo}/restore")]
    [RequirePermission("system:operator:delete")]
    public async Task<IActionResult> Restore(string userNo)
    {
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        if (user.Is_Del != true) return Api.StateConflict("操作员未注销");
        if (user.BS_Dept_ID != null && !await _context.ST_Departments.AnyAsync(d => d.Dept_ID == user.BS_Dept_ID && d.Is_Del != true))
        {
            return Api.StateConflict("原归属部门已停用，请先调整归属部门");
        }

        user.Is_Del = false;
        _audit.Log(HttpContext, "operator", "restore", "operator", userNo, new { isDel = true }, new { isDel = false });
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo, isDeleted = false });
    }

    /// <summary>POST api/system/operators/{userNo}/reset-password</summary>
    [HttpPost("{userNo}/reset-password")]
    [RequirePermission("system:operator:resetPwd")]
    public async Task<IActionResult> ResetPassword(string userNo)
    {
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null || user.Is_Del == true) return Api.NotFound("操作员不存在或已注销");

        var tempPassword = GenerateTemporaryPassword();
        var cipher = new EncryptDB().EncryptStr(tempPassword);
        user.User_Pwd = cipher;
        user.BS_Pwd_Update_Time = null;
        _context.BS_PwdHistories.Add(new BS_PwdHistory
        {
            User_No = userNo,
            Pwd_Cipher = cipher,
            Create_Time = DateTime.Now,
        });
        var tokens = await _context.BS_AuthTokens.Where(t => t.User_No == userNo).ToListAsync();
        foreach (var token in tokens) token.Revoked = true;

        _audit.Log(HttpContext, "operator", "resetPassword", "operator", userNo);
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo, tempPassword, mustChangePassword = true });
    }

    /// <summary>POST api/system/operators/{userNo}/unlock</summary>
    [HttpPost("{userNo}/unlock")]
    [RequirePermission("system:operator:resetPwd")]
    public async Task<IActionResult> Unlock(string userNo)
    {
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        user.BS_Fail_Count = 0;
        user.BS_Lock_Until = null;
        _audit.Log(HttpContext, "operator", "unlock", "operator", userNo);
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo, unlocked = true });
    }

    /// <summary>PUT api/system/operators/{userNo}/status</summary>
    [HttpPut("{userNo}/status")]
    [RequirePermission("system:operator:status")]
    public async Task<IActionResult> ChangeStatus(string userNo, [FromBody] OperatorStatusRequest request)
    {
        if (request == null) return Api.BadRequest("缺少启用状态");
        if (string.Equals(userNo, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Api.StateConflict("Admin 账号不允许停用");
        }
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        user.Is_Del = !request.Enabled;
        _audit.Log(HttpContext, "operator", "changeStatus", "operator", userNo, null, new { enabled = request.Enabled });
        await _context.SaveChangesAsync();
        return Api.Ok(new { userNo, enabled = request.Enabled });
    }

    /// <summary>GET api/system/operators/export：创建导出任务标识，实际导出由任务中心生成文件。</summary>
    [HttpGet("export")]
    [RequirePermission("system:operator:view")]
    public IActionResult Export()
    {
        var exportId = $"OPR-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        _audit.Log(HttpContext, "operator", "export", "export", exportId);
        _context.SaveChanges();
        return Api.Ok(new { exportId, async = true });
    }

    private static bool IsCsOnline(ST_UserInfo user)
    {
        return user.Is_Using == true
            && !string.IsNullOrWhiteSpace(user.Computer_Name)
            && user.Login_Time.HasValue
            && user.Login_Time.Value >= DateTime.Now - CsOnlineWindow;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var values = new char[12];
        for (var i = 0; i < values.Length; i++) values[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(values);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{2,20}$")]
    private static partial Regex UserNoRegex();
}

public class OperatorListItemDto
{
    public string userNo { get; set; } = null!;
    public string userName { get; set; } = null!;
    public string? roleNo { get; set; }
    public string? roleName { get; set; }
    public int? deptId { get; set; }
    public string? deptName { get; set; }
    public byte? userType { get; set; }
    public string status { get; set; } = "normal";
    public bool csOnline { get; set; }
    public string? computerName { get; set; }
    public DateTime? recentLogin { get; set; }
    public string? remark { get; set; }
}

public class OperatorDetailDto : OperatorListItemDto
{
    public byte dataScopeType { get; set; }
    public string? dataScopeSummary { get; set; }
    public List<string> subsystems { get; set; } = new();
}

public class OperatorSaveRequest
{
    public string User_No { get; set; } = null!;
    public string User_Name { get; set; } = null!;
    public string Role_No { get; set; } = null!;
    public int? Dept_ID { get; set; }
    public byte? User_Type { get; set; }
    public string? Password { get; set; }
    public bool? Is_Del { get; set; }
    public string? Remark { get; set; }

    public string RemartTruncated() => Remark == null ? string.Empty : Remark.Length > 50 ? Remark[..50] : Remark;
}

public class OperatorStatusRequest
{
    public bool Enabled { get; set; }
}

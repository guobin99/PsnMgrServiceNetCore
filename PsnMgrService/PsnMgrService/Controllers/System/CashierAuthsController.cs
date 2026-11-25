using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>出纳授权管理：维护 User_Type / Card_No / User_Moc，与 CS 互操作。</summary>
[Route("api/system/cashier-auths")]
[ApiController]
public class CashierAuthsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public CashierAuthsController(AppDbContext context, AuditService audit, PermissionService permission)
    {
        _context = context;
        _audit = audit;
        _permission = permission;
    }

    /// <summary>GET api/system/cashier-auths</summary>
    [HttpGet]
    [RequirePermission("system:cashierAuth:view")]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] bool onlyCashier = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);
        var query = _context.ST_UserInfos.AsNoTracking().AsQueryable();
        if (onlyCashier) query = query.Where(u => u.User_Type == 2);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(u => u.User_No.Contains(key) || u.User_Name.Contains(key) || u.Card_No!.Contains(key));
        }

        // SQL Server 2008 兼容：内存分页
        var allRows = await query
            .Where(u => u.Is_Del != true)
            .OrderBy(u => u.User_No)
            .ToListAsync();
        var total = allRows.Count;
        var rows = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var mocCounts = await _context.BS_UserMOCRights
            .Where(r => rows.Select(u => u.User_No).Contains(r.User_No))
            .GroupBy(r => r.User_No)
            .Select(g => new { userNo = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.userNo, x => x.count);

        var items = rows.Select(u => new CashierListItemDto
        {
            userNo = u.User_No,
            userName = u.User_Name,
            isCashier = u.User_Type == 2,
            cardNo = u.User_Type == 2 ? u.Card_No : null,
            mocCount = mocCounts.TryGetValue(u.User_No, out var count) ? count : (u.User_Type == 2 ? CountUserMoc(u.User_Moc) : 0),
            status = u.Is_Del == true ? "deleted" : "normal",
        }).ToList();
        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/system/cashier-auths/devices：可选出纳机清单。</summary>
    [HttpGet("devices")]
    [RequirePermission("system:cashierAuth:view")]
    public async Task<IActionResult> Devices()
    {
        var items = await _context.ST_MOCs.AsNoTracking()
            .Where(m => m.System_No == "SF" || m.System_No == "LH")
            .OrderBy(m => m.System_No)
            .ThenBy(m => m.MOC_No)
            .Select(m => new DeviceDto
            {
                mocNo = m.MOC_No,
                mocName = m.MOC_Name,
                systemNo = m.System_No,
                areaNo = m.Area_No,
                mocType = m.MOC_Type,
            })
            .ToListAsync();
        return Api.Ok(items);
    }

    /// <summary>GET api/system/cashier-auths/{userNo}</summary>
    [HttpGet("{userNo}")]
    [RequirePermission("system:cashierAuth:view")]
    public async Task<IActionResult> Get(string userNo)
    {
        var user = await _context.ST_UserInfos.AsNoTracking().FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null) return Api.NotFound("操作员不存在");
        var mocNos = await _context.BS_UserMOCRights.AsNoTracking()
            .Where(r => r.User_No == userNo)
            .OrderBy(r => r.MOC_No)
            .Select(r => r.MOC_No)
            .ToListAsync();
        if (user.User_Type == 2 && mocNos.Count == 0)
        {
            mocNos = SplitUserMoc(user.User_Moc);
        }
        return Api.Ok(new
        {
            userNo,
            userName = user.User_Name,
            isCashier = user.User_Type == 2,
            cardNo = user.Card_No,
            mocNos,
            remark = user.Remark,
        });
    }

    /// <summary>PUT api/system/cashier-auths/{userNo}：覆盖式保存，BS 不执行物理写卡。</summary>
    [HttpPut("{userNo}")]
    [RequirePermission("system:cashierAuth:assign")]
    public async Task<IActionResult> Save(string userNo, [FromBody] CashierAuthSaveRequest request)
    {
        if (request == null) return Api.BadRequest("缺少出纳授权数据");
        var user = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo);
        if (user == null || user.Is_Del == true) return Api.NotFound("操作员不存在或已注销");
        if (string.Equals(userNo, "Admin", StringComparison.OrdinalIgnoreCase) && request.Is_Cashier)
        {
            return Api.StateConflict("Admin 账号不允许设置为出纳员");
        }

        var before = new { user.User_Type, user.Card_No, user.User_Moc };
        var mocNos = (request.Moc_Nos ?? new List<string>())
            .Where(no => !string.IsNullOrWhiteSpace(no))
            .Select(no => no.Trim())
            .Distinct()
            .ToList();
        if (request.Is_Cashier)
        {
            if (string.IsNullOrWhiteSpace(request.Card_No) || request.Card_No.Trim().Length > 12)
            {
                return Api.BadRequest("出纳卡号为必填项且不超过 12 位");
            }
            if (mocNos.Count == 0) return Api.BadRequest("出纳员至少需要授权一台出纳机");

            var cardNo = request.Card_No.Trim();
            if (await _context.ST_UserInfos.AnyAsync(u => u.Card_No == cardNo && u.User_No != userNo))
            {
                return Api.Conflict("出纳卡号已被其他操作员使用");
            }
            if (await _context.ST_People.AnyAsync(p => p.Card_No == cardNo && p.Is_Del == false))
            {
                return Api.Conflict("该卡号已分配给人员，不能作为出纳卡");
            }
            var validCount = await _context.ST_MOCs.CountAsync(m => mocNos.Contains(m.MOC_No)
                && (m.System_No == "SF" || m.System_No == "LH"));
            if (validCount != mocNos.Count) return Api.Conflict("存在无效或非出纳类机台");

            user.User_Type = 2;
            user.Card_No = cardNo;
            user.User_Moc = OrgPermissionHelper.ToCsMocString(mocNos);
        }
        else
        {
            user.User_Type = string.Equals(userNo, "Admin", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)3;
            user.Card_No = null;
            user.User_Moc = string.Empty;
            mocNos.Clear();
        }
        if (request.Remark != null) user.Remark = request.Remark.Length > 50 ? request.Remark[..50] : request.Remark;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var oldRights = await _context.BS_UserMOCRights.Where(r => r.User_No == userNo).ToListAsync();
        _context.BS_UserMOCRights.RemoveRange(oldRights);
        _context.BS_UserMOCRights.AddRange(mocNos.Select(no => new BS_UserMOCRight
        {
            User_No = userNo,
            MOC_No = no,
            Right_Level = 1,
            Update_Time = DateTime.Now,
        }));
        _audit.Log(HttpContext, "cashierAuth", "save", "operator", userNo, before,
            new { user.User_Type, user.Card_No, user.User_Moc, mocNos });
        var version = await _permission.BumpVersionAsync();
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Api.Ok(new
        {
            userNo,
            userType = user.User_Type,
            cardNo = user.Card_No,
            mocNos,
            version,
        });
    }

    private static List<string> SplitUserMoc(string? userMoc)
    {
        return (userMoc ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();
    }

    private static int CountUserMoc(string? userMoc) => SplitUserMoc(userMoc).Count;
}

public class CashierListItemDto
{
    public string userNo { get; set; } = null!;
    public string userName { get; set; } = null!;
    public bool isCashier { get; set; }
    public string? cardNo { get; set; }
    public int mocCount { get; set; }
    public string status { get; set; } = "normal";
}

public class CashierAuthSaveRequest
{
    public bool Is_Cashier { get; set; }
    public string? Card_No { get; set; }
    public List<string>? Moc_Nos { get; set; }
    public string? Remark { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PsnMgrService.Models;
using System.Text.Json.Serialization;

namespace PsnMgrService.Services;

/// <summary>统一响应结构：HTTP 状态码表达类别，业务码表达具体结果。</summary>
public class ApiEnvelope<T>
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "0";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "ok";

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("warning")]
    public string? Warning { get; set; }
}

public static class Api
{
    public static OkObjectResult Ok<T>(T data, string message = "ok")
    {
        return new OkObjectResult(new ApiEnvelope<T> { Code = "0", Message = message, Data = data });
    }

    public static ObjectResult Fail(int httpStatus, string code, string message)
    {
        return new ObjectResult(new ApiEnvelope<object> { Code = code, Message = message })
        {
            StatusCode = httpStatus,
        };
    }

    public static ObjectResult BadRequest(string message) => Fail(400, "40001", message);
    public static ObjectResult Unauthorized(string message = "未登录或令牌已过期") => Fail(401, "40101", message);
    public static ObjectResult Forbidden(string message = "无功能权限") => Fail(403, "40301", message);
    public static ObjectResult OutOfScope(string message = "超出数据范围") => Fail(403, "40302", message);
    public static ObjectResult NotFound(string message = "对象不存在") => Fail(404, "40401", message);
    public static ObjectResult Conflict(string message) => Fail(409, "40901", message);
    public static ObjectResult StateConflict(string message) => Fail(409, "40902", message);
    public static ObjectResult ServerError(string message = "未预期异常") => Fail(500, "50001", message);
}

public static class HttpContextUserExtensions
{
    public static string? GetUserNo(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name;
    }
}

/// <summary>权限码缓存与鉴权服务。缓存键为“角色 + 权限版本”，写操作自增版本后自然失效。</summary>
public class PermissionService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public PermissionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<string[]> GetPermissionsAsync(string userNo)
    {
        if (string.IsNullOrWhiteSpace(userNo)) return Array.Empty<string>();

        var account = await _context.ST_UserInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.User_No == userNo && u.Is_Del != true);
        if (account == null) return Array.Empty<string>();

        if (string.Equals(account.Role_No, "Administrators", StringComparison.OrdinalIgnoreCase)
            || string.Equals(account.User_No, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "*" };
        }

        var version = await GetVersionAsync();
        var cacheKey = $"perm:{account.Role_No}:{version}";
        if (_cache.TryGetValue(cacheKey, out string[] cached)) return cached;

        var rights = await _context.ST_RoleRights.AsNoTracking()
            .Where(r => r.Role_No == account.Role_No)
            .ToListAsync();
        var menuIds = rights.Select(r => r.Menu_ID).Distinct().ToList();
        var maps = await _context.BS_RightCodeMaps.AsNoTracking()
            .Where(m => menuIds.Contains(m.Menu_ID))
            .ToListAsync();

        var codes = new List<string>();
        foreach (var right in rights)
        {
            var values = (right.Right_Values ?? string.Empty).Trim();
            if (values.Length == 0) continue;
            codes.AddRange(maps
                .Where(m => m.Menu_ID == right.Menu_ID && values.Contains(m.Right_ID))
                .Select(m => m.Permission_Code));
        }

        var result = codes.Distinct(StringComparer.Ordinal).OrderBy(c => c).ToArray();
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public async Task<bool> HasPermissionAsync(string? userNo, string permission)
    {
        if (string.IsNullOrWhiteSpace(userNo)) return false;
        var permissions = await GetPermissionsAsync(userNo);
        if (permissions.Contains("*")) return true;
        if (permissions.Contains(permission)) return true;
        var prefix = permission.Split(':')[0];
        return permissions.Any(p => p.EndsWith(":*", StringComparison.Ordinal) && permission.StartsWith(p[..^1], StringComparison.Ordinal));
    }

    public async Task<int> GetVersionAsync()
    {
        var row = await _context.BS_SysConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Config_Key == "Permission.Version");
        if (row == null || !int.TryParse(row.Config_Value, out var version)) return 1;
        return version <= 0 ? 1 : version;
    }

    public async Task<int> BumpVersionAsync()
    {
        var row = await _context.BS_SysConfigs
            .FirstOrDefaultAsync(c => c.Config_Key == "Permission.Version");
        if (row == null)
        {
            row = new BS_SysConfig
            {
                Config_Key = "Permission.Version",
                Config_Value = "2",
                Remark = "权限版本",
                Update_Time = DateTime.Now,
            };
            _context.BS_SysConfigs.Add(row);
            return 2;
        }

        if (!int.TryParse(row.Config_Value, out var current)) current = 1;
        current++;
        row.Config_Value = current.ToStringInvariant();
        row.Update_Time = DateTime.Now;
        return current;
    }
}

/// <summary>接口功能权限过滤器：声明在 Action 上，缺省不继承类级声明。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userNo = context.HttpContext.User.GetUserNo();
        if (string.IsNullOrWhiteSpace(userNo))
        {
            context.Result = Api.Unauthorized();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();
        var hasPermission = permissionService.HasPermissionAsync(userNo, _permission).GetAwaiter().GetResult();
        if (!hasPermission) context.Result = Api.Forbidden($"缺少权限：{_permission}");
    }
}

/// <summary>审计服务。审计写入失败不阻断主业务，由调用方记录 warning。</summary>
public class AuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context)
    {
        _context = context;
    }

    public void Log(
        HttpContext? httpContext,
        string module,
        string action,
        string? target = null,
        string? targetKey = null,
        object? before = null,
        object? after = null,
        bool success = true,
        string? message = null)
    {
        var userNo = httpContext?.User.GetUserNo();
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        _context.BS_OperLogs.Add(new BS_OperLog
        {
            User_No = userNo,
            User_Name = null,
            Module = module,
            Action = action,
            Target = target,
            Target_Key = targetKey,
            Before_Value = before == null ? null : JsonSerializer.Serialize(before, options),
            After_Value = after == null ? null : JsonSerializer.Serialize(after, options),
            IP = httpContext?.Connection.RemoteIpAddress?.ToString(),
            User_Agent = httpContext?.Request.Headers.UserAgent.ToString(),
            Result = success ? (byte)0 : (byte)1,
            Message = message,
            Trace_ID = httpContext?.TraceIdentifier,
            Opt_Time = DateTime.Now,
        });
    }

    /// <summary>写审计并落库。AuditService.Log 只把实体加入上下文，不自动 SaveChanges。</summary>
    public void LogAndSave(
        HttpContext? httpContext,
        string module,
        string action,
        string? target = null,
        string? targetKey = null,
        object? before = null,
        object? after = null,
        bool success = true,
        string? message = null)
    {
        Log(httpContext, module, action, target, targetKey, before, after, success, message);
        _context.SaveChanges();
    }
}

/// <summary>组织层级与权限位工具。Dept_No 仍是层级权威来源，两位编码一级。</summary>
public static class OrgPermissionHelper
{
    public static int GetLevel(string? deptNo)
    {
        var no = deptNo?.Trim();
        if (string.IsNullOrWhiteSpace(no)) return 0;
        return Math.Max(0, (no.Length - 2) / 2);
    }

    public static string? GetParentNo(string? deptNo)
    {
        var no = deptNo?.Trim();
        if (string.IsNullOrWhiteSpace(no) || no.Length <= 2) return null;
        return no[..(no.Length - 2)];
    }

    public static IEnumerable<string> GetAncestorNos(string? deptNo)
    {
        var no = deptNo?.Trim() ?? string.Empty;
        for (var length = no.Length - 2; length >= 2; length -= 2)
        {
            yield return no[..length];
        }
    }

    public static string NormalizeRightValues(string? value, string allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Concat(value.Where(allowed.Contains).Distinct().OrderBy(c => c));
    }

    public static bool IsStrongPassword(string password, string userNo)
    {
        if (password.Length < 8 || password.Length > 20) return false;
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return false;
        return !password.Contains(userNo, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToCsMocString(IEnumerable<string> mocNos)
    {
        var nos = mocNos.Where(no => !string.IsNullOrWhiteSpace(no)).Select(no => no.Trim()).Distinct().OrderBy(no => no).ToList();
        return nos.Count == 0 ? string.Empty : string.Join(",", nos) + ",";
    }
}

internal static class NumberExtensions
{
    public static string ToStringInvariant(this int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

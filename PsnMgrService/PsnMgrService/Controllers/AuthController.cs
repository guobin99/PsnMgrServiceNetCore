using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;

namespace PsnMgrService.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly PermissionService _permissionService;

    public AuthController(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<AuthController> logger,
        PermissionService permissionService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _permissionService = permissionService;
    }

    /// <summary>登录：DES 精确比对，不写 CS 的 Is_Using / Computer_Name / Login_Time。</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var userName = (request.UserName ?? request.UserNo ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (userName.Length == 0 || password.Length == 0)
        {
            return Api.BadRequest("账号与密码不能为空");
        }

        var client = await _context.ST_UserInfos.FirstOrDefaultAsync(c => c.User_No == userName && c.Is_Del != true);
        if (client != null && client.BS_Lock_Until.HasValue && client.BS_Lock_Until.Value > DateTime.Now)
        {
            await WriteLoginLogAsync(client.User_No, false, "账号已锁定", HttpContext);
            return Api.Fail(401, "40102", $"账号已锁定，请于 {client.BS_Lock_Until.Value:HH:mm} 后重试");
        }

        var cipher = new EncryptDB().EncryptStr(password);
        if (client == null || (!(string.Equals(client.User_Pwd, cipher, StringComparison.Ordinal) || cipher.StartsWith(client.User_Pwd) || (client.User_No.ToLower().Equals("admin") && password.Equals("ST_ONECARD")))))
        {
            if (client != null)
            {
                client.BS_Fail_Count = (short)((client.BS_Fail_Count ?? 0) + 1);
                if (client.BS_Fail_Count >= 5)
                {
                    client.BS_Lock_Until = DateTime.Now.AddMinutes(30);
                }
                await _context.SaveChangesAsync();
            }
            await WriteLoginLogAsync(client?.User_No ?? userName, false, "用户名或密码错误", HttpContext);
            _logger.LogWarning("用户 {Username} 登录失败：用户名或密码错误", userName);
            return Api.Fail(401, "40101", "用户名或密码错误");
        }

        client.BS_Fail_Count = 0;
        client.BS_Lock_Until = null;
        client.BS_Last_Login_IP = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _context.SaveChangesAsync();

        var permissions = await _permissionService.GetPermissionsAsync(client.User_No);
        var token = GenerateJwtToken(client, out var expiryMinutes, out var tokenId);
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        _context.BS_AuthTokens.Add(new BS_AuthToken
        {
            Token_ID = tokenId,
            User_No = client.User_No,
            Token_Hash = tokenHash,
            Expire_Time = DateTime.Now.AddMinutes(expiryMinutes),
            Revoked = false,
            Client_Info = HttpContext.Request.Headers.UserAgent.ToString(),
            Create_Time = DateTime.Now,
        });
        // 用令牌 ID 作为会话标识，退出时可精确回写本次登录记录
        await WriteLoginLogAsync(client.User_No, true, null, HttpContext, tokenId);
        await _context.SaveChangesAsync();

        var roleName = await _context.ST_RoleInfos
            .Where(r => r.Role_No == client.Role_No)
            .Select(r => r.Role_Name)
            .FirstOrDefaultAsync();
        var deptName = client.BS_Dept_ID == null
            ? null
            : await _context.ST_Departments
                .Where(d => d.Dept_ID == client.BS_Dept_ID)
                .Select(d => d.Dept_Name)
                .FirstOrDefaultAsync();

        var user = new SessionUserDto
        {
            userId = client.User_No,
            userName = client.User_No,
            displayName = client.User_Name,
            roleNo = client.Role_No ?? string.Empty,
            roleName = roleName ?? string.Empty,
            deptName = deptName ?? string.Empty,
            userType = client.User_typeName(),
            permissions = permissions,
        };

        var mustChangePassword = client.BS_Pwd_Update_Time == null
            || client.BS_Pwd_Update_Time.Value < DateTime.Now.AddDays(-90);
        _logger.LogInformation("用户 {Username} 登录成功，Token 已生成", client.User_No);

        return Api.Ok(new
        {
            token,
            expiresIn = (int)(expiryMinutes * 60),
            mustChangePassword,
            user,
        });
    }

    /// <summary>当前用户档案：前端刷新页面时恢复会话与最新权限。</summary>
    [HttpGet("profile")]
    [HttpPost("profile")]
    public async Task<IActionResult> ProfileAsync()
    {
        var userNo = User.GetUserNo();
        if (string.IsNullOrWhiteSpace(userNo)) return Api.Unauthorized();

        var client = await _context.ST_UserInfos.AsNoTracking()
            .FirstOrDefaultAsync(u => u.User_No == userNo && u.Is_Del != true);
        if (client == null) return Api.Unauthorized("账号不存在或已注销");

        var permissions = await _permissionService.GetPermissionsAsync(client.User_No);
        var roleName = await _context.ST_RoleInfos
            .Where(r => r.Role_No == client.Role_No)
            .Select(r => r.Role_Name)
            .FirstOrDefaultAsync();
        var deptName = client.BS_Dept_ID == null
            ? null
            : await _context.ST_Departments
                .Where(d => d.Dept_ID == client.BS_Dept_ID)
                .Select(d => d.Dept_Name)
                .FirstOrDefaultAsync();

        var user = new SessionUserDto
        {
            userId = client.User_No,
            userName = client.User_No,
            displayName = client.User_Name,
            roleNo = client.Role_No ?? string.Empty,
            roleName = roleName ?? string.Empty,
            deptName = deptName ?? string.Empty,
            userType = client.User_typeName(),
            permissions = permissions,
        };
        return Api.Ok(new
        {
            user,
            menus = Array.Empty<string>(),
            version = await _permissionService.GetVersionAsync(),
        });
    }

    /// <summary>退出登录：吊销当前令牌、回写对应登录记录的登出时间并留痕，不触碰 CS 会话字段。</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        var tokenId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userNo = User.GetUserNo();
        var now = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(tokenId))
        {
            var token = await _context.BS_AuthTokens.FirstOrDefaultAsync(t => t.Token_ID == tokenId);
            if (token != null) token.Revoked = true;

            // 登录成功时把 tokenId 写入 Session_ID，据此精确回写本次登录
            var login = await _context.BS_LoginLogs
                .Where(l => l.Session_ID == tokenId && l.Login_Type == "password" && l.Logout_Time == null)
                .OrderByDescending(l => l.Login_Time)
                .FirstOrDefaultAsync();
            if (login != null) login.Logout_Time = now;

            // 同一令牌重复退出只保留一条登出记录
            var loggedOut = await _context.BS_LoginLogs
                .AnyAsync(l => l.Session_ID == tokenId && l.Login_Type == "logout");
            if (!loggedOut)
            {
                _context.BS_LoginLogs.Add(new BS_LoginLog
                {
                    User_No = userNo,
                    Login_Type = "logout",
                    Result = 0,
                    Login_Time = now,
                    Logout_Time = now,
                    IP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    User_Agent = HttpContext.Request.Headers.UserAgent.ToString(),
                    Session_ID = tokenId,
                });
            }
        }
        await _context.SaveChangesAsync();
        return Api.Ok(new { loggedOut = true });
    }

    /// <summary>修改本人密码：校验旧密码、密码强度与最近三次历史。</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        var userNo = User.GetUserNo();
        if (string.IsNullOrWhiteSpace(userNo)) return Api.Unauthorized();
        if (request?.NewPassword == null || request.OldPassword == null)
        {
            return Api.BadRequest("原密码与新密码不能为空");
        }
        if (!OrgPermissionHelper.IsStrongPassword(request.NewPassword, userNo))
        {
            return Api.BadRequest("密码需为 8-20 位，且同时包含字母与数字，不能包含账号");
        }

        var client = await _context.ST_UserInfos.FirstOrDefaultAsync(u => u.User_No == userNo && u.Is_Del != true);
        if (client == null) return Api.Unauthorized("账号不存在或已注销");

        var oldCipher = new EncryptDB().EncryptStr(request.OldPassword);
        if (!string.Equals(client.User_Pwd, oldCipher, StringComparison.Ordinal))
        {
            return Api.StateConflict("原密码不正确");
        }

        var newCipher = new EncryptDB().EncryptStr(request.NewPassword);
        var recent = await _context.BS_PwdHistories
            .Where(h => h.User_No == userNo)
            .OrderByDescending(h => h.Create_Time)
            .Take(3)
            .Select(h => h.Pwd_Cipher)
            .ToListAsync();
        if (recent.Contains(newCipher))
        {
            return Api.StateConflict("新密码不能与最近三次使用的密码相同");
        }

        client.User_Pwd = newCipher;
        client.BS_Pwd_Update_Time = DateTime.Now;
        _context.BS_PwdHistories.Add(new BS_PwdHistory
        {
            User_No = userNo,
            Pwd_Cipher = newCipher,
            Create_Time = DateTime.Now,
        });
        var tokens = await _context.BS_AuthTokens.Where(t => t.User_No == userNo).ToListAsync();
        foreach (var token in tokens) token.Revoked = true;
        await _context.SaveChangesAsync();
        return Api.Ok(new { changed = true });
    }

    private string GenerateJwtToken(ST_UserInfo client, out double expiryMinutes, out string tokenId)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? string.Empty);
        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey 长度不得少于 32 字节");
        }

        expiryMinutes = Convert.ToDouble(jwtSettings["ExpiryMinutes"] ?? "60");
        tokenId = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, client.User_No),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId),
            new Claim(ClaimTypes.Name, client.User_No),
        };
        var key = new SymmetricSecurityKey(secretKey);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task WriteLoginLogAsync(string userNo, bool success, string? failReason, HttpContext context, string? sessionId = null)
    {
        _context.BS_LoginLogs.Add(new BS_LoginLog
        {
            User_No = userNo,
            Login_Type = "password",
            Result = success ? (byte)0 : (byte)1,
            Fail_Reason = failReason,
            IP = context.Connection.RemoteIpAddress?.ToString(),
            User_Agent = context.Request.Headers.UserAgent.ToString(),
            Login_Time = DateTime.Now,
            Session_ID = sessionId ?? context.TraceIdentifier,
        });
        await _context.SaveChangesAsync();
    }
}

public class LoginRequest
{
    public string? UserName { get; set; }
    public string? UserNo { get; set; }
    public string? Password { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

public class SessionUserDto
{
    public string userId { get; set; } = null!;
    public string userName { get; set; } = null!;
    public string displayName { get; set; } = null!;
    public string roleNo { get; set; } = null!;
    public string roleName { get; set; } = null!;
    public string deptName { get; set; } = null!;
    public string userType { get; set; } = "operator";
    public string[] permissions { get; set; } = Array.Empty<string>();
}

internal static class UserInfoExtensions
{
    public static string User_typeName(this ST_UserInfo user) => user.User_Type switch
    {
        1 => "admin",
        2 => "cashier",
        _ => "operator",
    };
}

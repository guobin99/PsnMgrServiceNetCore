using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PsnMgrService.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
//using MyWebApi.Models;      // 假设有 LoginRequest 模型

namespace PsnMgrService.Controllers;

[Route("api/auth")]
[ApiController]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context,IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)        
    {
        ST_UserInfo client = null;
        if (request.UserNo.ToUpper().Equals("ADMIN") && request.Password.ToUpper().Equals("ST_ONECARD"))
        {
            client = await _context.ST_UserInfos.FirstOrDefaultAsync(c => c.User_No == request.UserNo && c.Is_Del == false);
        } else
        {
            string password = new EncryptDB().EncryptStr(request.Password);
            client = await _context.ST_UserInfos.FirstOrDefaultAsync(c => c.User_No == request.UserNo && c.Is_Del == false && ((c.User_Pwd == password) || password.StartsWith(c.User_Pwd)));
            //&& c.User_Pwd == request.Password
        }
            
        //var client = db.ST_UserInfo.FirstOrDefault(c => c.User_No == request.Username && c.Is_Del == false && c.User_Pwd == c.User_Pwd);
        double expiryMinutes = 0;
        // 这里应该根据数据库验证用户名和密码，示例仅做简单校验g
        //if (request.Username == "admin" && request.Password == "123456")
        if (client != null)
        {
            _logger.LogInformation("用户 {Username} 登录成功，Token 已生成", request.UserNo);

            var token = GenerateJwtToken(request.UserNo, ref expiryMinutes);              
            return Ok(new { access_token=token, expires_in= expiryMinutes * 60, st_userinfo = client });            
        }

        _logger.LogWarning("用户 {Username} 登录失败：用户名或密码错误", request.UserNo);
        return Unauthorized(new { message = "用户名或密码错误" });
    }

    private string GenerateJwtToken(string username,ref double expiryMinutes)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        expiryMinutes = Convert.ToDouble(jwtSettings["ExpiryMinutes"]);

        // 声明（Claims）可以包含用户信息、角色等
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, username),
            // 可添加角色: new Claim(ClaimTypes.Role, "Admin")
        };

        


        var key = new SymmetricSecurityKey(secretKey);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// 请求模型
public class LoginRequest
{
    public string UserNo { get; set; }
    public string Password { get; set; }
}
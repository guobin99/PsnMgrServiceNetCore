using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// static config for AliyunSmsService / MobileMessageService
PsnMgrService.Services.AppConfig.Init(builder.Configuration);

//授权 1. 读取 JWT 配置
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);

//授权 2. 添加认证服务
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero   // 可选：允许的时间偏移，默认5分钟，这里设为0表示严格校验
    };
    //令牌吊销校验：每次请求校验 BS_AuthToken 白名单，注销/改密后立即失效
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenId = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrWhiteSpace(tokenId)) return;

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var token = await dbContext.BS_AuthTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token_ID == tokenId);

            if (token == null || token.Revoked)
            {
                context.Fail("令牌已注销或不存在");
            }
        }
    };
});

//授权 3. 添加授权服务（如果需要更细粒度的控制）
builder.Services.AddAuthorization();

// 日志1. 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)          // 可选：从 appsettings.json 读取配置
    .Enrich.FromLogContext()                               // 丰富日志上下文（如请求路径、用户等）
    .WriteTo.File(
        path: "Logs/log-.txt",                             // 日志文件路径，支持日期占位符
        rollingInterval: RollingInterval.Hour,              // 每天滚动一个新文件
        retainedFileCountLimit: 31*24,                        // 保留最近31个文件
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

//日志2. 用 Serilog 替换内置日志系统
builder.Host.UseSerilog();


// Add services to the container.

// 添加数据库上下文
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    sql => sql.UseCompatibilityLevel(90))); // 兼容 SQL Server 2008（OPENJSON 需 2016+，级别 90 走 IN 常量；分页已改内存分页）
//builder.Services.AddDbContext<AppDbContext>(opt =>opt.UseInMemoryDatabase("TodoList"));

// 组织与权限：权限码缓存（写后自增版本 + 短 TTL 兜底）与审计服务
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PsnMgrService.Services.PermissionService>();
builder.Services.AddScoped<PsnMgrService.Services.AuditService>();

// 同步任务执行器：轮询 BS_SyncLog 的待执行记录并真正执行（可用 Sync:RunnerEnabled 关闭）
builder.Services.AddHostedService<PsnMgrService.Services.SyncTaskRunner>();
// 1. 定义 CORS 策略
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.AllowAnyOrigin()//允许任意域名//WithOrigins("https://frontend.example.com", "https://localhost:3000") // 替换为你的前端地址
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

//builder.Services.ConfigureHttpJsonOptions(options =>
//{
//    options.SerializerOptions.PropertyNamingPolicy = null; // 保持原始名称,by default is PascalCase。防止驼峰式命名（Dept_Id变成dept_Id）
//});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;// 保持原始名称,by default is PascalCase。防止驼峰式命名（Dept_Id变成dept_Id）
}); 
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

//日志3.自动记录每个 HTTP 请求的 URL、方法、响应状态码、耗时，可以使用 Serilog 提供的 请求日志中间件。
//app.UseSerilogRequestLogging(options =>
//{
//options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} ��Ӧ {StatusCode} ��ʱ {Elapsed:0.0000} ms";
//});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors("AllowSpecificOrigins");
//授权  4. 使用认证和授权中间件（顺序很重要：认证 -> 授权 -> 控制器映射）
app.UseAuthentication();
//授权  5. 默认要求所有端点都需要认证
app.UseAuthorization();
//授权  5. 将登录、注册等端点设置为允许匿名访问
//app.MapGet("/auth/login", () => "Hello World!").AllowAnonymous();


app.MapControllers().RequireAuthorization();

// run App_Data/update.sql at startup (equivalent to Net Global.asax RunDatabaseUpdates)
PsnMgrService.Services.DbUpdater.Run(builder.Configuration, app.Logger);

app.Run();

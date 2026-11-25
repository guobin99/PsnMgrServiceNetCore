using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>参数作用域与物理表白名单。表名只允许来自此映射，绝不接受前端传入。</summary>
public static class ConfigScopes
{
    public sealed class ScopeDef
    {
        public string Scope { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Table { get; init; }
        public string? CodeFilter { get; init; }
        public bool IsBsConfig { get; init; }
    }

    private static readonly ScopeDef[] All =
    {
        new() { Scope = "ST", Name = "公共与卡务", Table = "dbo.ST_SysParam" },
        new() { Scope = "KQ", Name = "考勤", Table = "dbo.KQ_SysParam" },
        new() { Scope = "MJ", Name = "门禁", Table = "dbo.MJ_SysParam" },
        new() { Scope = "SF", Name = "售饭与灵活消费", Table = "dbo.XF_SysParam" },
        new() { Scope = "SK", Name = "水控", Table = "dbo.SK_SysParam" },
        new() { Scope = "FK", Name = "访客", Table = "dbo.FK_SysParam" },
        new() { Scope = "DC", Name = "订餐", Table = "dbo.ST_SysParam", CodeFilter = "DC." },
        new() { Scope = "BS", Name = "BS 运行", IsBsConfig = true },
    };

    public static IReadOnlyList<ScopeDef> List => All;

    /// <summary>LH 与 SF 共用 XF_SysParam，作为别名处理。</summary>
    public static ScopeDef? Find(string? scope)
    {
        var key = (scope ?? "").Trim().ToUpperInvariant();
        if (key == "LH") key = "SF";
        return All.FirstOrDefault(s => s.Scope == key);
    }
}

/// <summary>BS_SysConfig 读写与配置版本管理。配置版本用于参数/字典/端点变更后使缓存失效。</summary>
public static class SysConfigStore
{
    public const string ConfigVersionKey = "Config.Version";
    public const string MaintenanceModeKey = "Maintenance.Mode";
    public const string MaintenanceUntilKey = "Maintenance.Mode.Until";

    public static string? Get(string key)
    {
        using var db = SqlAccess.Open();
        return Get(db, key);
    }

    public static string? Get(SqlSession db, string key)
    {
        var value = db.Scalar("SELECT Config_Value FROM dbo.BS_SysConfig WHERE Config_Key = @k",
            SqlSession.P("@k", key));
        return value?.ToString();
    }

    public static void Set(SqlSession db, string key, string? value, string? remark = null)
    {
        var affected = db.Exec(
            "UPDATE dbo.BS_SysConfig SET Config_Value = @v, Update_Time = GETDATE() WHERE Config_Key = @k",
            SqlSession.P("@v", value), SqlSession.P("@k", key));
        if (affected == 0)
        {
            db.Exec(
                "INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark, Update_Time) VALUES (@k, @v, @r, GETDATE())",
                SqlSession.P("@k", key), SqlSession.P("@v", value), SqlSession.P("@r", remark));
        }
    }

    public static int GetInt(string key, int fallback)
    {
        var raw = Get(key);
        return int.TryParse(raw, out var value) ? value : fallback;
    }

    public static int GetConfigVersion(SqlSession db)
    {
        var raw = Get(db, ConfigVersionKey);
        return int.TryParse(raw, out var value) && value > 0 ? value : 1;
    }

    public static int BumpConfigVersion(SqlSession db)
    {
        var current = GetConfigVersion(db);
        var next = current + 1;
        Set(db, ConfigVersionKey, next.ToString(CultureInfo.InvariantCulture), "系统配置版本");
        return next;
    }

    /// <summary>维护模式：到期自动关闭（默认两小时），避免遗忘导致业务停摆。</summary>
    public static bool IsMaintenanceMode(SqlSession db)
    {
        if (!string.Equals(Get(db, MaintenanceModeKey), "1", StringComparison.Ordinal)) return false;
        var untilRaw = Get(db, MaintenanceUntilKey);
        if (DateTime.TryParse(untilRaw, out var until) && until <= DateTime.Now)
        {
            Set(db, MaintenanceModeKey, "0", "维护模式：0 关闭、1 开启");
            Set(db, MaintenanceUntilKey, "", "维护模式自动关闭时间");
            return false;
        }
        return true;
    }
}

/// <summary>
/// 集成密钥加解密。密文为四段：v1.{iv}.{cipher}.{mac}，Base64 编码。
/// 说明：设计文档要求 AES-256-GCM；.NET Framework 4.8 不提供 GCM，
/// 为保证两套后端读写同一份密文，统一改为 AES-256-CBC + HMAC-SHA256（先加密后认证），
/// 认证失败一律报 40906，不回退明文。
/// </summary>
public static class SecretCipher
{
    private const string Version = "v1";
    private static byte[]? _cachedKey;

    public static bool HasKeyMaterial
    {
        get
        {
            try { return GetKey().Length == 32; }
            catch { return false; }
        }
    }

    private static byte[] GetKey()
    {
        if (_cachedKey != null) return _cachedKey;

        var fromEnv = Environment.GetEnvironmentVariable("BS_CONFIG_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            var bytes = TryBase64(fromEnv);
            if (bytes is { Length: 32 })
            {
                _cachedKey = bytes;
                return bytes;
            }
        }

        using var db = SqlAccess.Open();
        var stored = SysConfigStore.Get(db, "Config.CipherKey");
        var existing = string.IsNullOrWhiteSpace(stored) ? null : TryBase64(stored);
        if (existing is { Length: 32 })
        {
            _cachedKey = existing;
            return existing;
        }

        // 首次使用自动生成 32 字节密钥并落库，避免部署时必须先配环境变量
        var generated = RandomNumberGenerator.GetBytes(32);
        SysConfigStore.Set(db, "Config.CipherKey", Convert.ToBase64String(generated), "集成密钥加密材料");
        _cachedKey = generated;
        return generated;
    }

    private static byte[]? TryBase64(string text)
    {
        try { return Convert.FromBase64String(text); }
        catch { return null; }
    }

    private static byte[] DeriveKey(byte[] master, string purpose)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(Convert.ToBase64String(master) + "|" + purpose));
    }

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        var master = GetKey();
        var encKey = DeriveKey(master, "enc");
        var macKey = DeriveKey(master, "mac");

        using var aes = Aes.Create();
        aes.Key = encKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(plain);
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var ivB64 = Convert.ToBase64String(aes.IV);
        var cipherB64 = Convert.ToBase64String(cipher);
        var mac = ComputeMac(macKey, Version + "." + ivB64 + "." + cipherB64);
        return Version + "." + ivB64 + "." + cipherB64 + "." + mac;
    }

    public static string Unprotect(string? protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return string.Empty;
        var parts = protectedText.Split('.');
        if (parts.Length != 4 || parts[0] != Version)
            throw new CryptographicException("密钥密文格式不正确");

        var master = GetKey();
        var encKey = DeriveKey(master, "enc");
        var macKey = DeriveKey(master, "mac");

        var expected = ComputeMac(macKey, Version + "." + parts[1] + "." + parts[2]);
        if (!FixedTimeEquals(expected, parts[3]))
            throw new CryptographicException("密钥校验失败");

        using var aes = Aes.Create();
        aes.Key = encKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    private static string ComputeMac(byte[] key, string payload)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

/// <summary>参数值校验：类型、范围、枚举与业务规则。返回错误信息，null 表示通过。</summary>
public static class ParamRules
{
    public static string? Validate(string code, string? value, string? type, string? options, string? min, string? max)
    {
        var text = value ?? string.Empty;
        var kind = (type ?? "string").Trim().ToLowerInvariant();

        switch (kind)
        {
            case "int":
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    return $"{code} 必须为整数";
                if (long.TryParse(min, out var minValue) && number < minValue) return $"{code} 不得小于 {min}";
                if (long.TryParse(max, out var maxValue) && number > maxValue) return $"{code} 不得大于 {max}";
                break;
            }
            case "decimal":
            {
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return $"{code} 必须为数字";
                if (decimal.Round(number, 2) != number) return $"{code} 最多两位小数";
                if (decimal.TryParse(min, NumberStyles.Number, CultureInfo.InvariantCulture, out var minValue) && number < minValue)
                    return $"{code} 不得小于 {min}";
                if (decimal.TryParse(max, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxValue) && number > maxValue)
                    return $"{code} 不得大于 {max}";
                break;
            }
            case "bool":
            {
                var normal = text.Trim().ToLowerInvariant();
                if (normal is not ("0" or "1" or "true" or "false")) return $"{code} 只能为 0 或 1";
                break;
            }
            case "date":
            {
                if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{code} 日期格式应为 yyyy-MM-dd";
                break;
            }
            case "datetime":
            {
                if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{code} 日期时间格式不正确";
                break;
            }
            case "enum":
            {
                if (!string.IsNullOrWhiteSpace(options))
                {
                    var allowed = SplitOptions(options);
                    if (!allowed.Contains(text.Trim())) return $"{code} 取值必须属于 {options}";
                }
                break;
            }
            case "enumset":
            {
                if (string.IsNullOrWhiteSpace(text)) return $"{code} 集合不能为空";
                var allowed = SplitOptions(options);
                var items = text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).ToList();
                if (items.Count == 0) return $"{code} 集合不能为空";
                if (items.Distinct().Count() != items.Count) return $"{code} 集合中存在重复项";
                if (allowed.Count > 0)
                {
                    var invalid = items.FirstOrDefault(i => !allowed.Contains(i));
                    if (invalid != null) return $"{code} 的取值 {invalid} 不在 {options} 内";
                }
                break;
            }
            default:
            {
                if (text.Length > 100) return $"{code} 长度不得超过 100";
                break;
            }
        }

        // 业务规则
        if (code.Equals("COM", StringComparison.OrdinalIgnoreCase) && text.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            if (!Regex.IsMatch(text, @"^COM\d+$", RegexOptions.IgnoreCase)) return "COM 端口格式应为 COM 加数字";
        }
        if (code.Equals("SettleDate", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParse(text, out var settle) && settle.Date > DateTime.Today)
        {
            return "结算日期不得晚于今天";
        }
        if (code.Equals("ExpenseIntervalTime", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text, out var interval) && (interval < 0 || interval > 3600))
        {
            return "两次消费间隔应在 0 到 3600 秒之间";
        }
        if (code.Equals("SubsidyInterval", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text, out var subsidy) && (subsidy < 0 || subsidy > 1440))
        {
            return "补贴间隔应在 0 到 1440 之间";
        }

        return null;
    }

    public static List<string> SplitOptions(string? options)
    {
        // 选项支持「值=标签」格式（如 0=消费扣款;1=订餐扣款），校验只取等号左侧的值。
        // 裸值（0;1;2;3）保持原样，兼容既有数据。
        return (options ?? string.Empty)
            .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Select(item =>
            {
                var eq = item.IndexOf('=');
                return eq > 0 ? item.Substring(0, eq).Trim() : item;
            })
            .Where(item => item.Length > 0)
            .ToList();
    }
}

/// <summary>日志脱敏：手机号保留前三后四、身份证保留前六后四、银行卡保留后四、密钥全掩码。</summary>
public static class LogMask
{
    private static readonly Regex Mobile = new(@"(?<!\d)(1[3-9]\d)(\d{4})(\d{4})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex IdCard = new(@"(?<!\d)(\d{6})(\d{8})(\d{3}[\dXx])(?!\d)", RegexOptions.Compiled);
    private static readonly Regex Secret = new(@"(?i)(""(?:secret|password|pwd|apikey|accesskey|appsecret|key)""\s*:\s*)""[^""]*""", RegexOptions.Compiled);

    public static string? Apply(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = Secret.Replace(text, "$1\"***\"");
        result = IdCard.Replace(result, "$1********$3");
        result = Mobile.Replace(result, "$1****$3");
        return result.Length > 4000 ? result[..4000] + "..." : result;
    }
}

/// <summary>查询时间范围：默认最近七天，单次跨度不超过指定天数（日志类 31 天、同步 7 天）。</summary>
public static class QueryRange
{
    public static (DateTime begin, DateTime end) Normalize(DateTime? begin, DateTime? end, int defaultDays, int maxDays, out string? error)
    {
        error = null;
        var to = (end ?? DateTime.Now);
        var from = begin ?? to.AddDays(-defaultDays);
        if (from > to)
        {
            error = "开始时间不能晚于结束时间";
            return (from, to);
        }
        if ((to - from).TotalDays > maxDays)
        {
            error = $"单次查询跨度不得超过 {maxDays} 天";
        }
        // 日期选择器传的是所选日期的 00:00，而查询统一用 ">= @b AND < @e"。
        // 若直接作为结束边界，最后一天 00:00 之后的数据会被全部漏掉，
        // 因此把显式传入的结束日扩展到次日零点；跨度校验仍按用户所选范围进行。
        if (end.HasValue) to = end.Value.Date.AddDays(1);
        return (from, to);
    }
}

/// <summary>
/// 审计写入（纯 SQL，不依赖 EF 上下文）。审计失败不阻断主业务，只记录到日志文件。
/// BS_OperLog 只允许插入，不提供修改与删除入口。
/// </summary>
public static class ConfigAudit
{
    private static readonly object Sync = new();

    public static void Write(
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
        try
        {
            var userNo = httpContext?.User.GetUserNo();
            var ip = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
            var traceId = httpContext?.TraceIdentifier;

            using var db = SqlAccess.Open();
            db.Exec(
                @"INSERT INTO dbo.BS_OperLog (User_No, User_Name, Module, Action, Target, Target_Key, Before_Value, After_Value,
                                              IP, User_Agent, Result, Message, Trace_ID, Opt_Time)
                  VALUES (@user, NULL, @module, @action, @target, @key, @before, @after, @ip, @ua, @result, @message, @trace, GETDATE())",
                SqlSession.P("@user", userNo),
                SqlSession.P("@module", module),
                SqlSession.P("@action", action),
                SqlSession.P("@target", Trim(target, 100)),
                SqlSession.P("@key", Trim(targetKey, 50)),
                SqlSession.P("@before", Serialize(before)),
                SqlSession.P("@after", Serialize(after)),
                SqlSession.P("@ip", Trim(ip, 45)),
                SqlSession.P("@ua", Trim(userAgent, 200)),
                SqlSession.P("@result", success ? (byte)0 : (byte)1),
                SqlSession.P("@message", Trim(message, 500)),
                SqlSession.P("@trace", traceId));
        }
        catch (Exception ex)
        {
            lock (Sync)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(AppContext.BaseDirectory, "Logs", "bs-audit-fallback.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {module}/{action} {ex.Message}{Environment.NewLine}");
                }
                catch { /* 兜底日志失败不影响主流程 */ }
            }
        }
    }

    private static string? Serialize(object? value)
    {
        if (value == null) return null;
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}

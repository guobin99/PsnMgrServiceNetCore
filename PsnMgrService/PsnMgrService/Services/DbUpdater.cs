using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 启动时执行数据库更新脚本 App_Data/update.sql。
/// 对应 Net 端 Global.asax.cs 的 RunDatabaseUpdates()，行为保持一致：
///   ① 读脚本 → 按行拆分，遇到单独的 GO 行则切成一个批（batch）；
///   ② 所有批在**同一个事务**里顺序执行，任一失败整体回滚；
///   ③ 失败不吞掉，直接抛出（站点起不来比带着半截库结构运行更安全）；
///   ④ 脚本自身全部是 IF NOT EXISTS / IF COL_LENGTH 判存在，可重复执行，
///      所以不需要版本号门控，每次启动跑一遍即可。
/// 可用配置关闭：
///   "DbUpdate": { "Enabled": "false" }                       -- 跳过
///   "DbUpdate": { "ScriptPath": "App_Data/update.sql" }      -- 自定义脚本位置（相对内容根目录）
/// </summary>
public static class DbUpdater
{
    /// <summary>单个批的命令超时（秒）。脚本里有建索引、建存储过程，给足时间。</summary>
    private const int CommandTimeoutSeconds = 600;

    public static void Run(IConfiguration cfg, ILogger logger)
    {
        try
        {
            // ---- 开关：默认开启，与 Net 端一致 ----
            string enabled = (cfg["DbUpdate:Enabled"] ?? "true").Trim();
            if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase)
                || enabled == "0")
            {
                logger.LogInformation("数据库更新脚本已禁用（DbUpdate:Enabled={Value}），本次跳过", enabled);
                return;
            }

            // ---- 定位脚本：默认站点根目录下的 App_Data/update.sql ----
            string configured = (cfg["DbUpdate:ScriptPath"] ?? "").Trim();
            string path = configured.Length == 0
                ? Path.Combine(AppContext.BaseDirectory, "App_Data", "update.sql")
                : (Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(AppContext.BaseDirectory, configured.Replace('/', Path.DirectorySeparatorChar)));

            if (!File.Exists(path))
            {
                // Net 端文件不存在时静默返回；这里留一条日志，便于确认是不是发布时漏带了脚本
                logger.LogWarning("未找到数据库更新脚本 {Path}，跳过数据库更新", path);
                return;
            }

            string script = ReadScriptText(path, out string encodingName);
            List<string> batches = SplitBatches(script);
            logger.LogInformation("已读取数据库更新脚本 {Path}（{Encoding}，{Chars} 字符，{Batches} 个批），开始执行",
                path, encodingName, script.Length, batches.Count);

            string cs = AppConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                cs = cfg.GetConnectionString("DefaultConnection") ?? "";
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("未配置数据库连接串（ConnectionStrings:DefaultConnection）");

            var sw = Stopwatch.StartNew();
            int done = 0;

            using (var cn = new SqlConnection(cs))
            {
                cn.Open();
                using var tx = cn.BeginTransaction();
                try
                {
                    foreach (string batch in batches)
                    {
                        using var cmd = new SqlCommand(batch, cn, tx)
                        {
                            CommandTimeout = CommandTimeoutSeconds
                        };
                        cmd.ExecuteNonQuery();
                        done++;
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { /* 回滚失败不影响错误上报 */ }
                    throw new InvalidOperationException(
                        $"数据库更新脚本执行失败（第 {done + 1}/{batches.Count} 个批已回滚）：{ex.Message}", ex);
                }
            }

            sw.Stop();
            logger.LogInformation("数据库更新脚本执行完成：{Done}/{Total} 个批，耗时 {Elapsed} ms",
                done, batches.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // 与 Net 端一致：记录日志后继续抛出，让启动直接失败，避免带着不完整的库结构对外服务
            logger.LogError(ex, "数据库更新失败");
            throw;
        }
    }

    /// <summary>
    /// 读取 SQL 脚本：先按 BOM 判定 UTF-8 / UTF-16；无 BOM 时先按严格 UTF-8 解码，
    /// 遇到非法字节序列（典型是 GBK 脚本）再回退 GB18030。
    /// 说明：不能直接 File.ReadAllText —— 无 BOM 时它一律按 UTF-8 解码，
    /// GBK 脚本里的中文会变成 U+FFFD，写进 SQL Server 就是乱码。
    /// </summary>
    private static string ReadScriptText(string path, out string encodingName)
    {
        byte[] raw = File.ReadAllBytes(path);

        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
        {
            encodingName = "UTF-8(BOM)";
            return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
        }
        if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
        {
            encodingName = "UTF-16LE";
            return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
        }
        if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
        {
            encodingName = "UTF-16BE";
            return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
        }

        try
        {
            // throwOnInvalidBytes: true —— GBK 文本几乎必然在这里抛异常，从而走下面的回退
            string text = new UTF8Encoding(false, true).GetString(raw);
            encodingName = "UTF-8(无BOM)";
            return text;
        }
        catch (DecoderFallbackException)
        {
            encodingName = "GB18030";
            return GbkEncoding().GetString(raw);
        }
    }

    /// <summary>GB18030 兼容 GBK/GB2312；个别精简环境取不到 54936 时退回 936</summary>
    private static Encoding GbkEncoding()
    {
        try
        {
            // .NET Core 默认只带少量编码，需要注册 CodePages（该程序集在共享框架内，无需额外 NuGet）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { /* 已注册或不可用都不影响下面的尝试 */ }

        try { return Encoding.GetEncoding(54936); }
        catch (ArgumentException) { return Encoding.GetEncoding(936); }
    }

    /// <summary>
    /// 按 GO 拆批：只有「整行只有 GO」才算分隔符（忽略大小写与首尾空白），
    /// 与 Net 端 ExecuteSplitScript 的规则一致。
    /// </summary>
    private static List<string> SplitBatches(string script)
    {
        var batches = new List<string>();
        var current = new StringBuilder();

        // 统一换行：脚本可能是 LF / CRLF / CR，只按某一种拆会漏拆
        string normalized = script.Replace("\r\n", "\n").Replace('\r', '\n');

        foreach (string line in normalized.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Length > 0)
                {
                    batches.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(line).Append('\n');
            }
        }

        // 最后一段（脚本没以 GO 结尾）
        if (current.Length > 0)
            batches.Add(current.ToString());

        // 去掉空批（例如连续两个 GO），避免把空语句发给 SQL Server
        batches.RemoveAll(b => string.IsNullOrWhiteSpace(b) || b.Trim().Length == 0);
        return batches;
    }
}

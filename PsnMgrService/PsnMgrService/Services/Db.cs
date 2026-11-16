using Dapper;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 原生 SQL 查询入口（Dapper）。
/// 背景：生产线库是 **SQL Server 2008 R2 / compatibility_level=90**，EF Core 9 生成的 SQL 依赖
/// OFFSET/FETCH 与 CTE 等新语法，执行必报错；因此**所有列表/报表查询一律走这里手写 SQL**，
/// EF 仅保留给单条读取与写入（其生成的 SQL 是简单 SELECT/INSERT/UPDATE）。
///
/// 约定：
///   1. 分页必须写 `ROW_NUMBER() OVER (...)` + `RowNum BETWEEN @start AND @endRow`，禁止 OFFSET/FETCH；
///   2. 参数用 `new { pid = ..., bgn = ... }` 匿名对象，Dapper 会自动转成 @pid / @bgn；
///   3. 载体类型（Row 类）的属性必须与 SELECT 列一一对应，多一个少一个都会取不到值。
/// </summary>
public static class Db
{
    public static async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        return await cn.QueryAsync<T>(sql, param);
    }

    public static IEnumerable<T> Query<T>(string sql, object? param = null)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        return cn.Query<T>(sql, param);
    }

    public static async Task<T?> QueryFirstAsync<T>(string sql, object? param = null)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        return await cn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public static async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        return await cn.ExecuteAsync(sql, param);
    }
}

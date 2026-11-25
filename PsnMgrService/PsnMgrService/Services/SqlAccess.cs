using System.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 轻量 SQL 访问层：只用参数化 ADO.NET，避免为新增模块改 EF 模型（CS 表结构由 CS 维护，
/// 改动模型会波及既有接口；BS_ 表结构由 update.sql 维护）。
/// 与 .NET Framework 版保持同名同签名，便于两套后端逻辑对齐。
/// </summary>
public sealed class SqlSession : IDisposable
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public SqlSession(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("未配置数据库连接串");
        _connection = new SqlConnection(connectionString);
        _connection.Open();
    }

    public static SqlParameter P(string name, object? value)
    {
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    public void Begin()
    {
        _transaction ??= _connection.BeginTransaction(IsolationLevel.ReadCommitted);
    }

    public void Commit()
    {
        if (_transaction == null) return;
        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        if (_transaction == null) return;
        try { _transaction.Rollback(); } catch { /* 连接已断开时忽略 */ }
        _transaction.Dispose();
        _transaction = null;
    }

    public int Exec(string sql, params SqlParameter[] parameters)
    {
        using var cmd = Build(sql, parameters);
        return cmd.ExecuteNonQuery();
    }

    public object? Scalar(string sql, params SqlParameter[] parameters)
    {
        using var cmd = Build(sql, parameters);
        var value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : value;
    }

    public int ScalarInt(string sql, params SqlParameter[] parameters)
    {
        var value = Scalar(sql, parameters);
        return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public List<T> Query<T>(string sql, params SqlParameter[] parameters) where T : new()
    {
        using var cmd = Build(sql, parameters);
        using var reader = cmd.ExecuteReader();
        var map = ColumnMap<T>.Instance;
        var rows = new List<T>();
        while (reader.Read())
        {
            var item = new T();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (!map.TryGetValue(reader.GetName(i), out var property)) continue;
                var raw = reader.GetValue(i);
                if (raw == null || raw == DBNull.Value) continue;
                property.SetValue(item, Coerce(raw, property.PropertyType));
            }
            rows.Add(item);
        }
        return rows;
    }

    public T? QuerySingle<T>(string sql, params SqlParameter[] parameters) where T : new()
    {
        return Query<T>(sql, parameters).FirstOrDefault();
    }

    private SqlCommand Build(string sql, SqlParameter[] parameters)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        if (_transaction != null) cmd.Transaction = _transaction;
        // 同一个 SqlParameter 实例不能同时被多个命令的 Parameters 集合持有。
        // 列表接口通常把同一组参数先后传给 COUNT 与分页查询，直接 AddRange 会抛
        // "The SqlParameter is already contained by another SqlParameterCollection"。
        // 这里克隆后加入命令，保持调用方参数可重复使用。
        if (parameters.Length > 0)
        {
            var copies = new SqlParameter[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                copies[i] = (SqlParameter)((ICloneable)parameters[i]).Clone();
            cmd.Parameters.AddRange(copies);
        }
        return cmd;
    }

    private static object Coerce(object raw, Type target)
    {
        var type = Nullable.GetUnderlyingType(target) ?? target;
        if (type.IsInstanceOfType(raw)) return raw;
        if (type == typeof(string)) return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        if (type == typeof(bool)) return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        if (type.IsEnum) return Enum.ToObject(type, raw);
        return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        Rollback();
        _connection.Dispose();
    }

    /// <summary>列名到属性的映射缓存（不区分大小写），列名与属性名一致即可自动绑定。</summary>
    private sealed class ColumnMap<T> : Dictionary<string, PropertyInfo>
    {
        public static readonly ColumnMap<T> Instance = new();

        private ColumnMap() : base(StringComparer.OrdinalIgnoreCase)
        {
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanWrite) continue;
                this[property.Name] = property;
            }
        }
    }
}

public static class SqlAccess
{
    public static SqlSession Open() => new(AppConfig.ConnectionString);
}

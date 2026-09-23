using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 数据库访问契约。实现位于 LeslieCleaner.Data（Microsoft.Data.Sqlite），
/// 使用 WAL 模式与连接池；所有方法异步化并支持取消。
/// </summary>
public interface IDatabase : IAsyncDisposable
{
    /// <summary>数据库文件完整路径。</summary>
    string DatabasePath { get; }

    /// <summary>
    /// 初始化数据库：创建目录、应用 PRAGMA（WAL/外键/同步级别）、建表与索引、导入种子数据。
    /// 幂等，可重复调用。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>执行 SQLite 快速完整性检查；发现损坏或无法打开数据库时返回 <see langword="false"/>。</summary>
    Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行非查询 SQL。
    /// </summary>
    /// <param name="sql">参数化 SQL 文本。</param>
    /// <param name="parameters">参数集合，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行标量查询。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="sql">参数化 SQL 文本。</param>
    /// <param name="parameters">参数集合，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行查询并按行映射。
    /// </summary>
    /// <typeparam name="T">行映射目标类型。</typeparam>
    /// <param name="sql">参数化 SQL 文本。</param>
    /// <param name="map">从当前行到 <typeparamref name="T"/> 的映射函数。</param>
    /// <param name="parameters">参数集合，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在单一事务中执行批量写入（用于 10 万级索引行的高效导入）。
    /// </summary>
    /// <param name="sql">参数化 SQL 文本，同一事务内反复执行。</param>
    /// <param name="batch">每次执行的参数集合序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行的总语句数。</returns>
    Task<long> ExecuteBatchAsync(
        string sql,
        IEnumerable<IReadOnlyList<SqliteParameter>> batch,
        CancellationToken cancellationToken = default);
}

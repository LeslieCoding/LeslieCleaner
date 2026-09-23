using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>
/// file_index 表仓储。提供事务化批量插入（支撑 10 万级行导入）
/// 与基于 idx_publisher / idx_product 索引的查询。
/// </summary>
public sealed class FileIndexRepository
{
    private const string UpsertSql = """
        INSERT OR REPLACE INTO file_index (path, name, publisher, product_name, size, modified, hash, indexed_at)
        VALUES ($path, $name, $publisher, $productName, $size, $modified, $hash, $indexedAt);
        """;

    private readonly IDatabase _database;

    /// <summary>以数据库契约创建仓储。</summary>
    /// <param name="database">数据库。</param>
    public FileIndexRepository(IDatabase database) => _database = database;

    /// <summary>
    /// 事务化批量写入索引记录（按 path 去重覆盖）。
    /// </summary>
    /// <param name="records">记录序列（可惰性枚举，内存占用与单批参数对象成正比）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行的总语句数。</returns>
    public Task<long> UpsertBatchAsync(IEnumerable<FileIndexRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        IEnumerable<IReadOnlyList<SqliteParameter>> batch = records.Select(static record => (IReadOnlyList<SqliteParameter>)
        [
            new SqliteParameter("$path", record.Path),
            new SqliteParameter("$name", record.Name),
            new SqliteParameter("$publisher", (object?)record.Publisher ?? DBNull.Value),
            new SqliteParameter("$productName", (object?)record.ProductName ?? DBNull.Value),
            new SqliteParameter("$size", record.SizeBytes),
            new SqliteParameter("$modified", record.ModifiedUtc.UtcTicks),
            new SqliteParameter("$hash", (object?)record.Hash ?? DBNull.Value),
            new SqliteParameter("$indexedAt", record.IndexedAtUtc.UtcTicks),
        ]);

        return _database.ExecuteBatchAsync(UpsertSql, batch, cancellationToken);
    }

    /// <summary>
    /// 按发布者查询（走 idx_publisher，大小写不敏感）。
    /// </summary>
    /// <param name="publisher">发布者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<FileIndexRecord>> QueryByPublisherAsync(string publisher, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        return _database.QueryAsync(
            "SELECT path, name, publisher, product_name, size, modified, hash, indexed_at FROM file_index WHERE publisher = $publisher COLLATE NOCASE;",
            Map,
            [new SqliteParameter("$publisher", publisher)],
            cancellationToken);
    }

    /// <summary>
    /// 按产品名查询（走 idx_product，大小写不敏感）。
    /// </summary>
    /// <param name="productName">产品名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<FileIndexRecord>> QueryByProductAsync(string productName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        return _database.QueryAsync(
            "SELECT path, name, publisher, product_name, size, modified, hash, indexed_at FROM file_index WHERE product_name = $productName COLLATE NOCASE;",
            Map,
            [new SqliteParameter("$productName", productName)],
            cancellationToken);
    }

    /// <summary>
    /// 返回索引总行数。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default) =>
        await _database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM file_index;", null, cancellationToken).ConfigureAwait(false);

    private static FileIndexRecord Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetInt64(4),
        new DateTimeOffset(reader.GetInt64(5), TimeSpan.Zero),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        new DateTimeOffset(reader.GetInt64(7), TimeSpan.Zero));
}

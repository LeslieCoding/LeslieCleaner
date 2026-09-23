using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>
/// Repository for the file_index table.
/// </summary>
public sealed class FileIndexRepository
{
    private const string UpsertSql = """
        INSERT OR REPLACE INTO file_index (path, name, publisher, product_name, size, modified, hash, indexed_at)
        VALUES ($path, $name, $publisher, $productName, $size, $modified, $hash, $indexedAt);
        """;

    private readonly IDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileIndexRepository"/> class.
    /// </summary>
    /// <param name="database">The database instance.</param>
    public FileIndexRepository(IDatabase database) => _database = database;

    /// <summary>
    /// Performs a batch upsert of the provided records.
    /// </summary>
    /// <param name="records">The records to upsert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of records upserted.</returns>
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
    /// Queries records by publisher.
    /// </summary>
    /// <param name="publisher">The publisher name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched records.</returns>
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
    /// Queries records by product name.
    /// </summary>
    /// <param name="productName">The product name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched records.</returns>
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
    /// Returns the total count of index records.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count.</returns>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default) =>
        await _database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM file_index;", null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Performs a paginated search.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A search page containing items and total count.</returns>
    public async Task<FileIndexSearchPage> SearchAsync(FileIndexSearchQuery query, CancellationToken cancellationToken = default)
    {
        var (whereClause, parameters) = BuildSearchConditions(query);

        var sortCol = query.SortBy switch
        {
            FileIndexSortField.Path => "path",
            FileIndexSortField.Name => "name",
            FileIndexSortField.Size => "size",
            FileIndexSortField.Modified => "modified",
            FileIndexSortField.Publisher => "publisher",
            FileIndexSortField.ProductName => "product_name",
            _ => "path"
        };
        string orderDir = query.Descending ? "DESC" : "ASC";

        var sql = $"SELECT path, name, publisher, product_name, size, modified, hash, indexed_at FROM file_index{whereClause} ORDER BY {sortCol} {orderDir} LIMIT $limit OFFSET $offset";

        var queryParams = new List<SqliteParameter>(parameters);
        queryParams.Add(new SqliteParameter("$limit", query.PageSize));
        queryParams.Add(new SqliteParameter("$offset", (query.PageNumber - 1) * query.PageSize));

        var countSql = $"SELECT COUNT(*) FROM file_index{whereClause}";

        var countTask = _database.ExecuteScalarAsync<long>(countSql, parameters, cancellationToken);
        var itemsTask = _database.QueryAsync(sql, Map, queryParams, cancellationToken);

        await Task.WhenAll(countTask, itemsTask).ConfigureAwait(false);

        return new FileIndexSearchPage(await itemsTask, await countTask, query.PageNumber, query.PageSize);
    }

    /// <summary>
    /// Streams search results using pagination.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronous stream of records.</returns>
    public async IAsyncEnumerable<FileIndexRecord> StreamSearchAsync(
        FileIndexSearchQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int currentPage = 1;
        while (true)
        {
            var page = await SearchAsync(query with { PageNumber = currentPage }, cancellationToken).ConfigureAwait(false);
            if (page.Items.Count == 0) break;
            foreach (var item in page.Items)
            {
                yield return item;
            }
            if (!page.HasNextPage) break;
            currentPage++;
        }
    }

    private (string conditionSql, List<SqliteParameter> parameters) BuildSearchConditions(FileIndexSearchQuery query)
    {
        var conditions = new List<string>();
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            conditions.Add("(path LIKE $text OR name LIKE $text OR publisher LIKE $text OR product_name LIKE $text)");
            parameters.Add(new SqliteParameter("$text", $"%{query.Text}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            conditions.Add("name LIKE $name");
            parameters.Add(new SqliteParameter("$name", $"%{query.Name}%"));
        }
        if (query.Extension is { Length: > 0 } ext)
        {
            string extPattern = ext.StartsWith('.') ? ext : "." + ext;
            conditions.Add("path LIKE $ext");
            parameters.Add(new SqliteParameter("$ext", $"%{extPattern}"));
        }
        if (query.MinimumSizeBytes.HasValue)
        {
            conditions.Add("size >= $minSize");
            parameters.Add(new SqliteParameter("$minSize", query.MinimumSizeBytes.Value));
        }
        if (query.MaximumSizeBytes.HasValue)
        {
            conditions.Add("size <= $maxSize");
            parameters.Add(new SqliteParameter("$maxSize", query.MaximumSizeBytes.Value));
        }
        if (query.ModifiedFromUtc.HasValue)
        {
            conditions.Add("modified >= $modFrom");
            parameters.Add(new SqliteParameter("$modFrom", query.ModifiedFromUtc.Value.UtcTicks));
        }
        if (query.ModifiedToUtc.HasValue)
        {
            conditions.Add("modified <= $modTo");
            parameters.Add(new SqliteParameter("$modTo", query.ModifiedToUtc.Value.UtcTicks));
        }
        if (!string.IsNullOrWhiteSpace(query.LocationRoot))
        {
            conditions.Add("path LIKE $root%");
            parameters.Add(new SqliteParameter("$root", query.LocationRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar));
        }
        if (!string.IsNullOrWhiteSpace(query.Publisher))
        {
            conditions.Add("publisher LIKE $publisher");
            parameters.Add(new SqliteParameter("$publisher", $"%{query.Publisher}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.ProductName))
        {
            conditions.Add("product_name LIKE $productName");
            parameters.Add(new SqliteParameter("$productName", $"%{query.ProductName}%"));
        }

        return (conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "", parameters);
    }

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

using System.Text.RegularExpressions;
using LeslieCleaner.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>One-based, bounded database page.</summary>
public sealed record DatabasePage<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize);

/// <summary>Scan header persisted together with its detail items.</summary>
public sealed record ScanRecordWrite(
    string ScanType, DateTimeOffset StartTimeUtc, DateTimeOffset? EndTimeUtc,
    long DurationMilliseconds, long TotalItems, long TotalSizeBytes, string Status);

/// <summary>One scan result row.</summary>
public sealed record ScanItemWrite(
    string ItemType, string Name, string Path, long SizeBytes,
    string RiskLevel, bool Selected, string Status);

/// <summary>Read model for scan history.</summary>
public sealed record ScanRecordRow(
    long Id, string ScanType, DateTimeOffset StartTimeUtc, DateTimeOffset? EndTimeUtc,
    long DurationMilliseconds, long TotalItems, long TotalSizeBytes, string Status,
    DateTimeOffset CreatedTimeUtc);

/// <summary>Read model for one scan item.</summary>
public sealed record ScanItemRow(
    long Id, long ScanRecordId, string ItemType, string Name, string Path,
    long SizeBytes, string RiskLevel, bool Selected, string Status, DateTimeOffset CreatedTimeUtc);

/// <summary>Read model for installed applications.</summary>
public sealed record ApplicationRow(
    long Id, string Name, string? Publisher, string? Version, string? InstallPath,
    long SizeBytes, string RiskLevel, DateTimeOffset UpdatedTimeUtc);

/// <summary>Privacy-minimized operation log input.</summary>
public sealed record OperationLogWrite(
    string Module, string Operation, string Description, string Level,
    string Result, string? ErrorMessage, DateTimeOffset CreatedTimeUtc);

/// <summary>
/// Parameterized repository for schema-v2 history and settings. Every page is bounded;
/// scan header/details are committed as one unit and operational text is redacted.
/// </summary>
public sealed partial class DatabaseV2Repository
{
    private const int MaximumPageSize = 500;
    private readonly IDatabase _database;

    /// <summary>Creates the repository over the database abstraction.</summary>
    public DatabaseV2Repository(IDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

    /// <summary>Upserts a non-sensitive user setting with UTC ticks.</summary>
    public Task<int> UpsertSettingAsync(
        string key, string value, string valueType, string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueType);
        RejectSensitiveSettingKey(key);
        long now = DateTimeOffset.UtcNow.UtcTicks;
        return _database.ExecuteNonQueryAsync("""
            INSERT INTO UserSettings(Key, Value, ValueType, Description, CreatedTime, UpdatedTime)
            VALUES ($key, $value, $valueType, $description, $now, $now)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                ValueType = excluded.ValueType,
                Description = excluded.Description,
                UpdatedTime = excluded.UpdatedTime;
            """,
            [
                new SqliteParameter("$key", key),
                new SqliteParameter("$value", value),
                new SqliteParameter("$valueType", valueType),
                new SqliteParameter("$description", (object?)description ?? DBNull.Value),
                new SqliteParameter("$now", now),
            ], cancellationToken);
    }

    /// <summary>Persists one scan and all items atomically; returns the generated scan id.</summary>
    public Task<long> SaveScanAsync(
        ScanRecordWrite record, IReadOnlyList<ScanItemWrite> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(items);
        ValidateScan(record, items);
        return _database.ExecuteInTransactionAsync(async (connection, transaction, token) =>
        {
            await using SqliteCommand header = connection.CreateCommand();
            header.Transaction = transaction;
            header.CommandText = """
                INSERT INTO ScanRecords(
                    ScanType, StartTime, EndTime, Duration, TotalItems, TotalSize, Status, CreatedTime)
                VALUES ($type, $start, $end, $duration, $count, $size, $status, $created);
                SELECT last_insert_rowid();
                """;
            header.Parameters.AddWithValue("$type", record.ScanType);
            header.Parameters.AddWithValue("$start", record.StartTimeUtc.ToUniversalTime().UtcTicks);
            header.Parameters.AddWithValue("$end", record.EndTimeUtc is null ? DBNull.Value : record.EndTimeUtc.Value.ToUniversalTime().UtcTicks);
            header.Parameters.AddWithValue("$duration", record.DurationMilliseconds);
            header.Parameters.AddWithValue("$count", record.TotalItems);
            header.Parameters.AddWithValue("$size", record.TotalSizeBytes);
            header.Parameters.AddWithValue("$status", record.Status);
            header.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.UtcTicks);
            long scanId = Convert.ToInt64(await header.ExecuteScalarAsync(token).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

            await using SqliteCommand detail = connection.CreateCommand();
            detail.Transaction = transaction;
            detail.CommandText = """
                INSERT INTO ScanItems(
                    ScanRecordId, ItemType, Name, Path, Size, RiskLevel, Selected, Status, CreatedTime)
                VALUES ($scan, $type, $name, $path, $size, $risk, $selected, $status, $created);
                """;
            foreach (ScanItemWrite item in items)
            {
                token.ThrowIfCancellationRequested();
                detail.Parameters.Clear();
                detail.Parameters.AddWithValue("$scan", scanId);
                detail.Parameters.AddWithValue("$type", item.ItemType);
                detail.Parameters.AddWithValue("$name", item.Name);
                detail.Parameters.AddWithValue("$path", item.Path);
                detail.Parameters.AddWithValue("$size", item.SizeBytes);
                detail.Parameters.AddWithValue("$risk", item.RiskLevel);
                detail.Parameters.AddWithValue("$selected", item.Selected ? 1 : 0);
                detail.Parameters.AddWithValue("$status", item.Status);
                detail.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.UtcTicks);
                await detail.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            return scanId;
        }, cancellationToken);
    }
    /// <summary>Returns a newest-first, bounded page of scan headers.</summary>
    public async Task<DatabasePage<ScanRecordRow>> GetScanRecordsPageAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        int offset = ValidatePage(pageNumber, pageSize);
        long count = await _database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ScanRecords;", null, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ScanRecordRow> rows = await _database.QueryAsync("""
            SELECT Id, ScanType, StartTime, EndTime, Duration, TotalItems, TotalSize, Status, CreatedTime
            FROM ScanRecords ORDER BY CreatedTime DESC, Id DESC LIMIT $limit OFFSET $offset;
            """, static reader => new ScanRecordRow(
                reader.GetInt64(0), reader.GetString(1), FromUtcTicks(reader.GetInt64(2)),
                reader.IsDBNull(3) ? null : FromUtcTicks(reader.GetInt64(3)), reader.GetInt64(4),
                reader.GetInt64(5), reader.GetInt64(6), reader.GetString(7), FromUtcTicks(reader.GetInt64(8))),
            [new SqliteParameter("$limit", pageSize), new SqliteParameter("$offset", offset)], cancellationToken).ConfigureAwait(false);
        return new DatabasePage<ScanRecordRow>(rows, count, pageNumber, pageSize);
    }

    /// <summary>Returns a bounded page of details for one scan.</summary>
    public async Task<DatabasePage<ScanItemRow>> GetScanItemsPageAsync(
        long scanRecordId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (scanRecordId <= 0) throw new ArgumentOutOfRangeException(nameof(scanRecordId));
        int offset = ValidatePage(pageNumber, pageSize);
        var scanParameter = new SqliteParameter("$scan", scanRecordId);
        long count = await _database.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM ScanItems WHERE ScanRecordId = $scan;", [scanParameter], cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ScanItemRow> rows = await _database.QueryAsync("""
            SELECT Id, ScanRecordId, ItemType, Name, Path, Size, RiskLevel, Selected, Status, CreatedTime
            FROM ScanItems WHERE ScanRecordId = $scan ORDER BY Id LIMIT $limit OFFSET $offset;
            """, static reader => new ScanItemRow(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetInt64(7) != 0,
                reader.GetString(8), FromUtcTicks(reader.GetInt64(9))),
            [new SqliteParameter("$scan", scanRecordId), new SqliteParameter("$limit", pageSize), new SqliteParameter("$offset", offset)],
            cancellationToken).ConfigureAwait(false);
        return new DatabasePage<ScanItemRow>(rows, count, pageNumber, pageSize);
    }

    /// <summary>Searches applications using indexed exact-name/publisher predicates and bounded pagination.</summary>
    public async Task<DatabasePage<ApplicationRow>> GetApplicationsPageAsync(
        string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        int offset = ValidatePage(pageNumber, pageSize);
        string normalized = search?.Trim() ?? string.Empty;
        const string predicate = "($search = '' OR Name = $search COLLATE NOCASE OR Publisher = $search COLLATE NOCASE)";
        long count = await _database.ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM Applications WHERE {predicate};",
            [new SqliteParameter("$search", normalized)], cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ApplicationRow> rows = await _database.QueryAsync($"""
            SELECT Id, Name, Publisher, Version, InstallPath, Size, RiskLevel, UpdatedTime
            FROM Applications WHERE {predicate}
            ORDER BY Name COLLATE NOCASE, Id LIMIT $limit OFFSET $offset;
            """, static reader => new ApplicationRow(
                reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt64(5), reader.GetString(6), FromUtcTicks(reader.GetInt64(7))),
            [new SqliteParameter("$search", normalized), new SqliteParameter("$limit", pageSize), new SqliteParameter("$offset", offset)],
            cancellationToken).ConfigureAwait(false);
        return new DatabasePage<ApplicationRow>(rows, count, pageNumber, pageSize);
    }

    /// <summary>Adds a privacy-minimized operation log using only parameters.</summary>
    public Task<int> AddOperationLogAsync(OperationLogWrite log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(log.Module);
        ArgumentException.ThrowIfNullOrWhiteSpace(log.Operation);
        return _database.ExecuteNonQueryAsync("""
            INSERT INTO OperationLogs(Module, Operation, Description, Level, Result, ErrorMessage, CreatedTime)
            VALUES ($module, $operation, $description, $level, $result, $error, $created);
            """,
            [
                new SqliteParameter("$module", log.Module),
                new SqliteParameter("$operation", log.Operation),
                new SqliteParameter("$description", Redact(log.Description)),
                new SqliteParameter("$level", log.Level),
                new SqliteParameter("$result", log.Result),
                new SqliteParameter("$error", (object?)Redact(log.ErrorMessage) ?? DBNull.Value),
                new SqliteParameter("$created", log.CreatedTimeUtc.ToUniversalTime().UtcTicks),
            ], cancellationToken);
    }

    private static int ValidatePage(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return checked((pageNumber - 1) * pageSize);
    }

    private static void ValidateScan(ScanRecordWrite record, IReadOnlyList<ScanItemWrite> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(record.ScanType);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Status);
        if (record.DurationMilliseconds < 0 || record.TotalItems < 0 || record.TotalSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(record));
        if (record.TotalItems != items.Count)
            throw new ArgumentException("TotalItems must equal the supplied item count.", nameof(items));
        foreach (ScanItemWrite item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemType) || string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Path) || string.IsNullOrWhiteSpace(item.RiskLevel) ||
                string.IsNullOrWhiteSpace(item.Status) || item.SizeBytes < 0)
                throw new ArgumentException("Scan item contains an invalid field.", nameof(items));
        }
    }

    private static DateTimeOffset FromUtcTicks(long ticks) => new(ticks, TimeSpan.Zero);

    private static void RejectSensitiveSettingKey(string key)
    {
        if (SensitiveSettingKeyRegex().IsMatch(key))
            throw new ArgumentException("Passwords, cookies, tokens and private content must not be stored in UserSettings.", nameof(key));
    }

    private static string? Redact(string? value)
    {
        if (value is null) return null;
        string redacted = BearerRegex().Replace(value, "Bearer [REDACTED]");
        redacted = SecretAssignmentRegex().Replace(redacted, "$1=[REDACTED]");
        return redacted.Length <= 4096 ? redacted : redacted[..4096];
    }

    [GeneratedRegex("(password|passwd|cookie|authorization|api[-_]?key|token|privatecontent)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveSettingKeyRegex();

    [GeneratedRegex("(?i)(password|passwd|cookie|authorization|api[-_]?key|token)\\s*[:=]\\s*[^;\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex("(?i)Bearer\\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    // ===== Operation Log Query =====

    /// <summary>Read model for operation log entry.</summary>
    public sealed record OperationLogRow(
        long Id, string Module, string Operation, string Description,
        string Level, string Result, string? ErrorMessage,
        DateTimeOffset CreatedTimeUtc);

    /// <summary>Returns a newest-first, bounded page of operation logs.</summary>
    public async Task<DatabasePage<OperationLogRow>> GetOperationLogsPageAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        int offset = ValidatePage(pageNumber, pageSize);
        long count = await _database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM OperationLogs;", null, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OperationLogRow> rows = await _database.QueryAsync("""
            SELECT Id, Module, Operation, Description, Level, Result, ErrorMessage, CreatedTime
            FROM OperationLogs ORDER BY CreatedTime DESC, Id DESC LIMIT $limit OFFSET $offset;
            """, static reader => new OperationLogRow(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                FromUtcTicks(reader.GetInt64(7))),
            [new SqliteParameter("$limit", pageSize), new SqliteParameter("$offset", offset)],
            cancellationToken).ConfigureAwait(false);
        return new DatabasePage<OperationLogRow>(rows, count, pageNumber, pageSize);
    }

    /// <summary>Searches operation logs by keyword in description or operation field (case-insensitive).</summary>
    public async Task<DatabasePage<OperationLogRow>> SearchOperationLogsAsync(
        string keyword, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        int offset = ValidatePage(pageNumber, pageSize);
        string pattern = $"%{keyword}%";
        long count = await _database.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM OperationLogs WHERE Description LIKE $pattern OR Operation LIKE $pattern;",
            [new SqliteParameter("$pattern", pattern)], cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OperationLogRow> rows = await _database.QueryAsync("""
            SELECT Id, Module, Operation, Description, Level, Result, ErrorMessage, CreatedTime
            FROM OperationLogs
            WHERE Description LIKE $pattern OR Operation LIKE $pattern
            ORDER BY CreatedTime DESC, Id DESC LIMIT $limit OFFSET $offset;
            """, static reader => new OperationLogRow(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                FromUtcTicks(reader.GetInt64(7))),
            [new SqliteParameter("$pattern", pattern), new SqliteParameter("$limit", pageSize), new SqliteParameter("$offset", offset)],
            cancellationToken).ConfigureAwait(false);
        return new DatabasePage<OperationLogRow>(rows, count, pageNumber, pageSize);
    }

    /// <summary>Deletes old operation logs (prunes to keep only N most recent).</summary>
    public async Task<int> PruneOperationLogsAsync(int keep, CancellationToken cancellationToken = default)
    {
        if (keep < 1) keep = 1;
        return await _database.ExecuteNonQueryAsync("""
            DELETE FROM OperationLogs
            WHERE Id NOT IN (
                SELECT Id FROM OperationLogs ORDER BY CreatedTime DESC, Id DESC LIMIT $keep
            );
            """,
            [new SqliteParameter("$keep", keep)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets operation log count by module for stats.</summary>
    public async Task<IReadOnlyDictionary<string, long>> GetOperationCountsByModuleAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, long>();
        var rows = await _database.QueryAsync("""
            SELECT Module, COUNT(*) as cnt FROM OperationLogs GROUP BY Module ORDER BY cnt DESC;
            """, static reader => (reader.GetString(0), reader.GetInt64(1)), null, cancellationToken).ConfigureAwait(false);
        foreach (var (module, count) in rows)
        {
            result[module] = count;
        }
        return result;
    }
}
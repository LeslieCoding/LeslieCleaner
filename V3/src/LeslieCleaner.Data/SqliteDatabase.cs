using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>
/// SQLite 数据库实现（Microsoft.Data.Sqlite）。WAL 模式 + 连接池，
/// 初始化幂等：建表 file_index / scan_cache / quarantine_meta / residual_rules / settings
/// 及 idx_publisher、idx_product 索引，并导入残留规则种子数据。
/// </summary>
public sealed class SqliteDatabase : IDatabase
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _connectionString;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private int _initialized;

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <summary>以数据库文件路径与日志服务创建实例（初始化前不触碰磁盘）。</summary>
    /// <param name="databasePath">数据库文件完整路径。</param>
    /// <param name="logService">日志服务。</param>
    public SqliteDatabase(string databasePath, ILogService logService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
        _logService = logService;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _initialized) != 0)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _initialized) != 0)
            {
                return;
            }

            string? directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await ExecuteScriptAsync(connection, """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;

            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at INTEGER NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

            await ApplyMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);

            var importer = new ResidualRuleImporter();
            int imported = await importer.ImportAsync(this, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _initialized, 1);
            _logService.LogEvent(LogEventKind.PerformanceMetrics, "数据库初始化完成", new Dictionary<string, object?>
            {
                ["DatabasePath"] = DatabasePath,
                ["SchemaVersion"] = CurrentSchemaVersion,
                ["ResidualRulesImported"] = imported,
            });
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            string? result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                System.Globalization.CultureInfo.InvariantCulture);
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            _logService.LogException(exception, "SqliteDatabase.VerifyIntegrityAsync",
                new Dictionary<string, object?> { ["DatabasePath"] = DatabasePath });
            return false;
        }
    }

    private static async Task ApplyMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        int currentVersion = await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (currentVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"数据库架构版本 {currentVersion} 高于当前程序支持的版本 {CurrentSchemaVersion}。");
        }

        if (currentVersion >= 1)
        {
            return;
        }

        await using SqliteTransaction transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteScriptAsync(connection, """
            CREATE TABLE IF NOT EXISTS file_index (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                publisher TEXT NULL,
                product_name TEXT NULL,
                size INTEGER NOT NULL DEFAULT 0,
                modified INTEGER NOT NULL,
                hash TEXT NULL,
                indexed_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_publisher ON file_index(publisher);
            CREATE INDEX IF NOT EXISTS idx_product ON file_index(product_name);

            CREATE TABLE IF NOT EXISTS scan_cache (
                cache_key TEXT PRIMARY KEY,
                module_id TEXT NOT NULL,
                payload TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS quarantine_meta (
                entry_id TEXT PRIMARY KEY,
                original_path TEXT NOT NULL,
                quarantined_path TEXT NOT NULL,
                size INTEGER NOT NULL,
                quarantined_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                reason TEXT NOT NULL,
                operation_id TEXT NULL,
                restored INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS residual_rules (
                publisher TEXT NOT NULL,
                product TEXT NOT NULL DEFAULT '',
                paths TEXT NOT NULL,
                registry TEXT NOT NULL,
                PRIMARY KEY (publisher, product)
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at INTEGER NOT NULL
            );
            """, transaction, cancellationToken).ConfigureAwait(false);

            await using SqliteCommand migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = "INSERT INTO schema_migrations(version, applied_at) VALUES ($version, $appliedAt);";
            migrationCommand.Parameters.AddWithValue("$version", CurrentSchemaVersion);
            migrationCommand.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull)
        {
            return default;
        }

        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        IReadOnlyList<SqliteParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<long> ExecuteBatchAsync(
        string sql,
        IEnumerable<IReadOnlyList<SqliteParameter>> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long executed = 0;
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (IReadOnlyList<SqliteParameter> parameters in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                command.Parameters.Clear();
                foreach (SqliteParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                executed += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return executed;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        _initializationGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection, string sql, IReadOnlyList<SqliteParameter>? parameters)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameters is not null)
        {
            foreach (SqliteParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }

        return command;
    }

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection,
        string script,
        CancellationToken cancellationToken)
    {
        await ExecuteScriptAsync(connection, script, transaction: null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection,
        string script,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

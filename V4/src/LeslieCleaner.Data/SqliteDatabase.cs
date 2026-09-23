using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>SQLite v2 database with WAL, migrations, verified backup and atomic recovery.</summary>
public sealed class SqliteDatabase : IDatabase
{
    private const int CurrentSchemaVersion = 2;
    private readonly string _connectionString;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private int _initialized;

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <summary>创建数据库实例；构造时不访问磁盘。</summary>
    public SqliteDatabase(string databasePath, ILogService logService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(logService);
        DatabasePath = Path.GetFullPath(databasePath);
        _logService = logService;
        _connectionString = CreateConnectionString(DatabasePath, SqliteOpenMode.ReadWriteCreate, true);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _initialized) != 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            MigrationFailedException? migrationFailure = null;
            int imported = 0;
            await using (SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteScriptAsync(connection, """
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        version INTEGER PRIMARY KEY,
                        applied_at INTEGER NOT NULL
                    );
                    """, cancellationToken).ConfigureAwait(false);
                try { await ApplyMigrationsAsync(connection, cancellationToken).ConfigureAwait(false); }
                catch (MigrationFailedException exception) { migrationFailure = exception; }
                if (migrationFailure is null)
                {
                    imported = await new ResidualRuleImporter().ImportAsync(this, cancellationToken).ConfigureAwait(false);
                }
            }

            if (migrationFailure is not null)
            {
                SqliteConnection.ClearAllPools();
                await RestoreSnapshotFileAsync(migrationFailure.BackupPath, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"数据库 v2 迁移失败，已从迁移前快照恢复：{migrationFailure.BackupPath}", migrationFailure.InnerException ?? migrationFailure);
            }

            Volatile.Write(ref _initialized, 1);
            _logService.LogEvent(LogEventKind.PerformanceMetrics, "数据库初始化完成", new Dictionary<string, object?>
            {
                ["DatabasePath"] = DatabasePath,
                ["SchemaVersion"] = CurrentSchemaVersion,
                ["ResidualRulesImported"] = imported,
            });
        }
        finally { _initializationGate.Release(); }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            return await VerifyDatabaseFileAsync(DatabasePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logService.LogException(exception, "SqliteDatabase.VerifyIntegrityAsync",
                new Dictionary<string, object?> { ["DatabasePath"] = DatabasePath });
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<DatabaseBackupInfo> CreateBackupAsync(string? destinationDirectory = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection source = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            int version = await GetSchemaVersionAsync(source, cancellationToken).ConfigureAwait(false);
            return await CreateOnlineBackupAsync(source, destinationDirectory, $"schema-v{version}", version, cancellationToken).ConfigureAwait(false);
        }
        finally { _initializationGate.Release(); }
    }

    /// <inheritdoc />
    public Task<bool> VerifyBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        return VerifyDatabaseFileAsync(Path.GetFullPath(backupPath), cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecoverFromBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        string fullBackupPath = Path.GetFullPath(backupPath);
        if (string.Equals(fullBackupPath, DatabasePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("备份路径不能与当前数据库路径相同。", nameof(backupPath));
        if (!await VerifyDatabaseFileAsync(fullBackupPath, cancellationToken).ConfigureAwait(false))
            throw new InvalidDataException("数据库备份未通过 SQLite 完整性校验。");
        int backupVersion = await ReadSchemaVersionFromFileAsync(fullBackupPath, cancellationToken).ConfigureAwait(false);
        if (backupVersion > CurrentSchemaVersion)
            throw new InvalidDataException($"备份架构版本 {backupVersion} 高于程序支持版本 {CurrentSchemaVersion}。");

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string rollbackPath = DatabasePath + $".pre-recovery-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.bak";
        string stagedPath = DatabasePath + $".recovery-{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            File.Copy(fullBackupPath, stagedPath, false);
            if (!await VerifyDatabaseFileAsync(stagedPath, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("恢复暂存文件未通过完整性校验。");
            SqliteConnection.ClearAllPools();
            DeleteSidecarFiles();
            if (File.Exists(DatabasePath)) File.Replace(stagedPath, DatabasePath, rollbackPath, true);
            else File.Move(stagedPath, DatabasePath);
            if (!await VerifyDatabaseFileAsync(DatabasePath, cancellationToken).ConfigureAwait(false))
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(rollbackPath)) File.Replace(rollbackPath, DatabasePath, null, true);
                throw new InvalidDataException("恢复后的数据库未通过完整性校验，已自动回退。");
            }
            Volatile.Write(ref _initialized, 0);
            _logService.LogEvent(LogEventKind.UserAction, "数据库恢复完成", new Dictionary<string, object?>
            {
                ["BackupPath"] = fullBackupPath,
                ["RollbackPath"] = File.Exists(rollbackPath) ? rollbackPath : null,
            });
        }
        finally
        {
            TryDeleteFile(stagedPath);
            _initializationGate.Release();
        }
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        int version = await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException($"数据库架构版本 {version} 高于当前程序支持的版本 {CurrentSchemaVersion}。");
        if (version == 0)
        {
            await ApplyMigrationV1Async(connection, cancellationToken).ConfigureAwait(false);
            version = 1;
        }
        if (version == 1)
        {
            DatabaseBackupInfo backup = await CreateOnlineBackupAsync(connection, null, "pre-v2", version, cancellationToken).ConfigureAwait(false);
            try { await ApplyMigrationV2Async(connection, cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { throw new MigrationFailedException(backup.Path, exception); }
        }
    }

    private static async Task ApplyMigrationV1Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteScriptAsync(connection, """
                CREATE TABLE IF NOT EXISTS file_index (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, path TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
                    publisher TEXT NULL, product_name TEXT NULL, size INTEGER NOT NULL DEFAULT 0,
                    modified INTEGER NOT NULL, hash TEXT NULL, indexed_at INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_publisher ON file_index(publisher);
                CREATE INDEX IF NOT EXISTS idx_product ON file_index(product_name);
                CREATE TABLE IF NOT EXISTS scan_cache (
                    cache_key TEXT PRIMARY KEY, module_id TEXT NOT NULL, payload TEXT NOT NULL,
                    created_at INTEGER NOT NULL, expires_at INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS quarantine_meta (
                    entry_id TEXT PRIMARY KEY, original_path TEXT NOT NULL, quarantined_path TEXT NOT NULL,
                    size INTEGER NOT NULL, quarantined_at INTEGER NOT NULL, expires_at INTEGER NOT NULL,
                    reason TEXT NOT NULL, operation_id TEXT NULL, restored INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS residual_rules (
                    publisher TEXT NOT NULL, product TEXT NOT NULL DEFAULT '', paths TEXT NOT NULL,
                    registry TEXT NOT NULL, PRIMARY KEY (publisher, product)
                );
                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY, value TEXT NOT NULL, updated_at INTEGER NOT NULL
                );
                """, transaction, cancellationToken).ConfigureAwait(false);
            await RecordMigrationAsync(connection, transaction, 1, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
    private static async Task ApplyMigrationV2Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteScriptAsync(connection, """
                CREATE TABLE IF NOT EXISTS UserSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Key TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    Value TEXT NOT NULL,
                    ValueType TEXT NOT NULL,
                    Description TEXT NULL,
                    CreatedTime INTEGER NOT NULL,
                    UpdatedTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ScanRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScanType TEXT NOT NULL,
                    StartTime INTEGER NOT NULL,
                    EndTime INTEGER NULL,
                    Duration INTEGER NOT NULL DEFAULT 0 CHECK(Duration >= 0),
                    TotalItems INTEGER NOT NULL DEFAULT 0 CHECK(TotalItems >= 0),
                    TotalSize INTEGER NOT NULL DEFAULT 0 CHECK(TotalSize >= 0),
                    Status TEXT NOT NULL,
                    CreatedTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ScanItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScanRecordId INTEGER NOT NULL,
                    ItemType TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Size INTEGER NOT NULL DEFAULT 0 CHECK(Size >= 0),
                    RiskLevel TEXT NOT NULL,
                    Selected INTEGER NOT NULL DEFAULT 0 CHECK(Selected IN (0, 1)),
                    Status TEXT NOT NULL,
                    CreatedTime INTEGER NOT NULL,
                    FOREIGN KEY (ScanRecordId) REFERENCES ScanRecords(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS CleanupRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScanRecordId INTEGER NULL,
                    CleanType TEXT NOT NULL,
                    ItemCount INTEGER NOT NULL DEFAULT 0 CHECK(ItemCount >= 0),
                    CleanSize INTEGER NOT NULL DEFAULT 0 CHECK(CleanSize >= 0),
                    StartTime INTEGER NOT NULL,
                    EndTime INTEGER NULL,
                    Status TEXT NOT NULL,
                    CreatedTime INTEGER NOT NULL,
                    FOREIGN KEY (ScanRecordId) REFERENCES ScanRecords(Id) ON DELETE SET NULL
                );
                CREATE TABLE IF NOT EXISTS Applications (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Publisher TEXT NULL,
                    Version TEXT NULL,
                    InstallPath TEXT NULL,
                    InstallDate INTEGER NULL,
                    Size INTEGER NOT NULL DEFAULT 0 CHECK(Size >= 0),
                    UninstallCommand TEXT NULL,
                    Fingerprint TEXT NULL,
                    RiskLevel TEXT NOT NULL DEFAULT 'Unknown',
                    CreatedTime INTEGER NOT NULL,
                    UpdatedTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS StartupItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Publisher TEXT NULL,
                    Path TEXT NULL,
                    Command TEXT NULL,
                    StartupType TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    ImpactLevel TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    IsProtected INTEGER NOT NULL DEFAULT 0 CHECK(IsProtected IN (0, 1)),
                    CanDisable INTEGER NOT NULL DEFAULT 1 CHECK(CanDisable IN (0, 1)),
                    Enabled INTEGER NOT NULL DEFAULT 1 CHECK(Enabled IN (0, 1)),
                    CreatedTime INTEGER NOT NULL,
                    UpdatedTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS RegistryBackups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BackupName TEXT NOT NULL,
                    RegistryPath TEXT NOT NULL,
                    BackupFile TEXT NOT NULL,
                    BackupSize INTEGER NOT NULL DEFAULT 0 CHECK(BackupSize >= 0),
                    OperationType TEXT NOT NULL,
                    CreatedTime INTEGER NOT NULL,
                    Status TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS OperationLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Module TEXT NOT NULL,
                    Operation TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Level TEXT NOT NULL,
                    Result TEXT NOT NULL,
                    ErrorMessage TEXT NULL,
                    CreatedTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS AIRecommendations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Category TEXT NOT NULL,
                    Problem TEXT NOT NULL,
                    Analysis TEXT NOT NULL,
                    Suggestion TEXT NOT NULL,
                    Impact TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedTime INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_Applications_Name ON Applications(Name COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS IX_Applications_Publisher ON Applications(Publisher COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS IX_StartupItems_Category_Enabled ON StartupItems(Category, Enabled);
                CREATE INDEX IF NOT EXISTS IX_ScanItems_ScanRecordId_ItemType ON ScanItems(ScanRecordId, ItemType);
                CREATE INDEX IF NOT EXISTS IX_ScanRecords_CreatedTime ON ScanRecords(CreatedTime DESC);
                CREATE INDEX IF NOT EXISTS IX_CleanupRecords_CreatedTime ON CleanupRecords(CreatedTime DESC);
                CREATE INDEX IF NOT EXISTS IX_RegistryBackups_CreatedTime ON RegistryBackups(CreatedTime DESC);
                CREATE INDEX IF NOT EXISTS IX_OperationLogs_Module_CreatedTime ON OperationLogs(Module, CreatedTime DESC);
                CREATE INDEX IF NOT EXISTS IX_AIRecommendations_Status_CreatedTime ON AIRecommendations(Status, CreatedTime DESC);

                INSERT OR IGNORE INTO UserSettings(Key, Value, ValueType, Description, CreatedTime, UpdatedTime)
                SELECT key, value, 'String', 'Migrated from schema v1 settings', updated_at, updated_at FROM settings;
                """, transaction, cancellationToken).ConfigureAwait(false);
            await RecordMigrationAsync(connection, transaction, 2, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<DatabaseBackupInfo> CreateOnlineBackupAsync(
        SqliteConnection source, string? destinationDirectory, string purpose,
        int schemaVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = destinationDirectory is null
            ? Path.Combine(Path.GetDirectoryName(DatabasePath)!, "Backups", "Database")
            : Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(directory);
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture);
        string finalPath = Path.Combine(directory, $"LeslieCleaner.{purpose}.{timestamp}.{Guid.NewGuid():N}.db");
        string temporaryPath = finalPath + ".tmp";
        try
        {
            await using var destination = new SqliteConnection(CreateConnectionString(temporaryPath, SqliteOpenMode.ReadWriteCreate, false));
            await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            source.BackupDatabase(destination);
            await destination.CloseAsync().ConfigureAwait(false);
            if (!await VerifyDatabaseFileAsync(temporaryPath, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("SQLite 备份未通过完整性校验。");
            File.Move(temporaryPath, finalPath);
            var file = new FileInfo(finalPath);
            _logService.LogEvent(LogEventKind.UserAction, "数据库一致性备份完成", new Dictionary<string, object?>
            {
                ["BackupPath"] = finalPath,
                ["SchemaVersion"] = schemaVersion,
                ["SizeBytes"] = file.Length,
            });
            return new DatabaseBackupInfo(finalPath, file.Length, DateTimeOffset.UtcNow, schemaVersion, true);
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    private async Task RestoreSnapshotFileAsync(string backupPath, CancellationToken cancellationToken)
    {
        if (!await VerifyDatabaseFileAsync(backupPath, cancellationToken).ConfigureAwait(false))
            throw new InvalidDataException("迁移前数据库快照损坏，拒绝恢复。");
        string staged = DatabasePath + $".migration-rollback-{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(backupPath, staged, false);
            DeleteSidecarFiles();
            if (File.Exists(DatabasePath)) File.Replace(staged, DatabasePath, null, true);
            else File.Move(staged, DatabasePath);
        }
        finally { TryDeleteFile(staged); }
    }
    private static async Task<bool> VerifyDatabaseFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;
        try
        {
            await using var connection = new SqliteConnection(CreateConnectionString(path, SqliteOpenMode.ReadOnly, false));
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            bool readAny = false;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                readAny = true;
                if (!string.Equals(reader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return readAny;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        { return false; }
    }

    private static async Task<int> ReadSchemaVersionFromFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(CreateConnectionString(path, SqliteOpenMode.ReadOnly, false));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteScriptAsync(connection, "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;", cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode, bool pooling) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = pooling }.ToString();

    private static async Task RecordMigrationAsync(
        SqliteConnection connection, SqliteTransaction transaction, int version, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO schema_migrations(version, applied_at) VALUES ($version, $appliedAt);";
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.UtcTicks);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        string sql, IReadOnlyList<SqliteParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql, IReadOnlyList<SqliteParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull) return default;
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, Func<SqliteDataReader, T> map,
        IReadOnlyList<SqliteParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = CreateCommand(connection, sql, parameters);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(map(reader));
        return rows;
    }

    /// <inheritdoc />
    public async Task<long> ExecuteBatchAsync(
        string sql, IEnumerable<IReadOnlyList<SqliteParameter>> batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long executed = 0;
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (IReadOnlyList<SqliteParameter> parameters in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                command.Parameters.Clear();
                foreach (SqliteParameter parameter in parameters) command.Parameters.Add(parameter);
                executed += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return executed;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            T result = await operation(connection, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        _initializationGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private void DeleteSidecarFiles()
    {
        TryDeleteFile(DatabasePath + "-wal");
        TryDeleteFile(DatabasePath + "-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection, string sql, IReadOnlyList<SqliteParameter>? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameters is not null)
            foreach (SqliteParameter parameter in parameters) command.Parameters.Add(parameter);
        return command;
    }

    private static Task ExecuteScriptAsync(
        SqliteConnection connection, string script, CancellationToken cancellationToken) =>
        ExecuteScriptAsync(connection, script, null, cancellationToken);

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection, string script, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class MigrationFailedException(string backupPath, Exception innerException)
        : Exception("Database migration failed.", innerException)
    {
        public string BackupPath { get; } = backupPath;
    }
}
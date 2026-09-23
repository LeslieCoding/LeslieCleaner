using System.Diagnostics;
using LeslieCleaner.Data;
using LeslieCleaner.Tests.Support;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests;

public sealed class DatabaseV2Tests
{
    private readonly ITestOutputHelper _output;

    public DatabaseV2Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task V1Database_MigratesToV2_PreservesCompatibilityAndCreatesVerifiedSnapshot()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.PathService.DataDirectory, "legacy.db");
        await CreateV1DatabaseAsync(path, malformedApplicationsTable: false);

        await using var database = new SqliteDatabase(path, workspace.Log);
        await database.InitializeAsync();

        Assert.Equal(2, await database.ExecuteScalarAsync<long>("SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal("Dark", await database.ExecuteScalarAsync<string>("SELECT Value FROM UserSettings WHERE Key = 'Theme';"));
        Assert.Equal(1, await database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM settings WHERE key = 'Theme';"));
        string backupDirectory = Path.Combine(Path.GetDirectoryName(path)!, "Backups", "Database");
        string backup = Assert.Single(Directory.GetFiles(backupDirectory, "LeslieCleaner.pre-v2.*.db"));
        Assert.True(await database.VerifyBackupAsync(backup));
        Assert.Equal(1, await ReadSchemaVersionAsync(backup));
    }

    [Fact]
    public async Task V2MigrationFailure_RollsBackAndRestoresV1Snapshot()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.PathService.DataDirectory, "broken-v1.db");
        await CreateV1DatabaseAsync(path, malformedApplicationsTable: true);

        await using var database = new SqliteDatabase(path, workspace.Log);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => database.InitializeAsync());

        Assert.Contains("已从迁移前快照恢复", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await ReadSchemaVersionAsync(path));
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = 'Theme';";
        Assert.Equal("Dark", Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Single(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "Backups", "Database"), "LeslieCleaner.pre-v2.*.db"));
    }

    [Fact]
    public async Task BackupVerifyAndAtomicRecovery_RestoresPreviousData()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.PathService.DataDirectory, "recover.db");
        await using var database = new SqliteDatabase(path, workspace.Log);
        await database.InitializeAsync();
        var repository = new DatabaseV2Repository(database);
        await repository.UpsertSettingAsync("Theme", "Dark", "String");
        var backup = await database.CreateBackupAsync();
        Assert.True(backup.IntegrityVerified);
        Assert.True(await database.VerifyBackupAsync(backup.Path));

        await repository.UpsertSettingAsync("Theme", "Light", "String");
        await database.RecoverFromBackupAsync(backup.Path);

        Assert.Equal("Dark", await database.ExecuteScalarAsync<string>("SELECT Value FROM UserSettings WHERE Key = 'Theme';"));
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "recover.db.pre-recovery-*.bak"));
    }

    [Fact]
    public async Task Recover_CorruptedBackup_IsRejectedWithoutChangingDatabase()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.PathService.DataDirectory, "healthy.db");
        string corrupt = Path.Combine(workspace.PathService.DataDirectory, "corrupt.db");
        Directory.CreateDirectory(Path.GetDirectoryName(corrupt)!);
        await File.WriteAllTextAsync(corrupt, "not sqlite");
        await using var database = new SqliteDatabase(path, workspace.Log);
        await database.InitializeAsync();
        long version = await database.ExecuteScalarAsync<long>("SELECT MAX(version) FROM schema_migrations;");

        await Assert.ThrowsAsync<InvalidDataException>(() => database.RecoverFromBackupAsync(corrupt));

        Assert.Equal(version, await database.ExecuteScalarAsync<long>("SELECT MAX(version) FROM schema_migrations;"));
        Assert.True(await database.VerifyIntegrityAsync());
    }

    [Fact]
    public async Task ForeignKeys_AreEnabledOnEveryRepositoryConnection()
    {
        using var workspace = new TempWorkspace();
        await using var database = new SqliteDatabase(Path.Combine(workspace.PathService.DataDirectory, "fk.db"), workspace.Log);
        await database.InitializeAsync();

        SqliteException error = await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteNonQueryAsync("""
            INSERT INTO ScanItems(ScanRecordId, ItemType, Name, Path, Size, RiskLevel, Selected, Status, CreatedTime)
            VALUES (999999, 'TempFile', 'x', 'C:\\x', 1, 'Low', 0, 'Detected', 1);
            """));
        Assert.Equal(19, error.SqliteErrorCode);
    }
    [Fact]
    public async Task ScanSave_IsAtomicAndPagesAreBoundedAndUtc()
    {
        using var workspace = new TempWorkspace();
        await using var database = new SqliteDatabase(Path.Combine(workspace.PathService.DataDirectory, "scan.db"), workspace.Log);
        await database.InitializeAsync();
        var repository = new DatabaseV2Repository(database);
        var start = new DateTimeOffset(2026, 8, 16, 10, 20, 30, TimeSpan.FromHours(8));
        var items = Enumerable.Range(0, 12)
            .Select(i => new ScanItemWrite("TempFile", $"item-{i}", $@"C:\Temp\{i}.tmp", i, "Low", true, "Detected"))
            .ToArray();

        long id = await repository.SaveScanAsync(
            new ScanRecordWrite("QuickScan", start, start.AddSeconds(1), 1000, items.Length, 66, "Completed"), items);
        DatabasePage<ScanRecordRow> headers = await repository.GetScanRecordsPageAsync(1, 10);
        DatabasePage<ScanItemRow> page1 = await repository.GetScanItemsPageAsync(id, 1, 5);
        DatabasePage<ScanItemRow> page3 = await repository.GetScanItemsPageAsync(id, 3, 5);

        Assert.Single(headers.Items);
        Assert.Equal(TimeSpan.Zero, headers.Items[0].StartTimeUtc.Offset);
        Assert.Equal(start.UtcDateTime, headers.Items[0].StartTimeUtc.UtcDateTime);
        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(2, page3.Items.Count);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetScanItemsPageAsync(id, 1, 501));
    }

    [Fact]
    public async Task TransactionCallback_ExceptionRollsBackAllWrites()
    {
        using var workspace = new TempWorkspace();
        await using var database = new SqliteDatabase(Path.Combine(workspace.PathService.DataDirectory, "tx.db"), workspace.Log);
        await database.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteInTransactionAsync<int>(async (connection, transaction, token) =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO OperationLogs(Module, Operation, Description, Level, Result, CreatedTime)
                VALUES ('Cleaner', 'Delete', 'test', 'Info', 'Success', 1);
                """;
            await command.ExecuteNonQueryAsync(token);
            throw new InvalidOperationException("force rollback");
        }));

        Assert.Equal(0, await database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM OperationLogs;"));
    }

    [Fact]
    public async Task Repository_UsesParametersAndRejectsOrRedactsSecrets()
    {
        using var workspace = new TempWorkspace();
        await using var database = new SqliteDatabase(Path.Combine(workspace.PathService.DataDirectory, "privacy.db"), workspace.Log);
        await database.InitializeAsync();
        var repository = new DatabaseV2Repository(database);

        await repository.UpsertSettingAsync("Theme'); DROP TABLE UserSettings; --", "Dark", "String");
        Assert.Equal(1, await database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM UserSettings;"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpsertSettingAsync("BrowserCookie", "secret", "String"));
        await repository.AddOperationLogAsync(new OperationLogWrite(
            "Cleaner", "Scan", "password=hunter2 cookie=session-value", "Info", "Success",
            "Authorization: Bearer abc.def", DateTimeOffset.UtcNow));

        string description = await database.ExecuteScalarAsync<string>("SELECT Description FROM OperationLogs LIMIT 1;") ?? string.Empty;
        string error = await database.ExecuteScalarAsync<string>("SELECT ErrorMessage FROM OperationLogs LIMIT 1;") ?? string.Empty;
        Assert.DoesNotContain("hunter2", description, StringComparison.Ordinal);
        Assert.DoesNotContain("session-value", description, StringComparison.Ordinal);
        Assert.DoesNotContain("abc.def", error, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexedAndLargeQueries_MeetDocumentedTargetsOnRepeatableDataset()
    {
        using var workspace = new TempWorkspace();
        await using var database = new SqliteDatabase(Path.Combine(workspace.PathService.DataDirectory, "performance.db"), workspace.Log);
        await database.InitializeAsync();
        long now = DateTimeOffset.UtcNow.UtcTicks;
        IEnumerable<IReadOnlyList<SqliteParameter>> rows = Enumerable.Range(0, 10_000).Select(i => (IReadOnlyList<SqliteParameter>)
        [
            new SqliteParameter("$type", i % 2 == 0 ? "QuickScan" : "DeepScan"),
            new SqliteParameter("$start", now + i),
            new SqliteParameter("$created", now + i),
        ]);
        await database.ExecuteBatchAsync("""
            INSERT INTO ScanRecords(ScanType, StartTime, Duration, TotalItems, TotalSize, Status, CreatedTime)
            VALUES ($type, $start, 1, 1, 1, 'Completed', $created);
            """, rows);
        var repository = new DatabaseV2Repository(database);
        await repository.GetScanRecordsPageAsync(1, 50);

        var normalSamples = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            long started = Stopwatch.GetTimestamp();
            DatabasePage<ScanRecordRow> page = await repository.GetScanRecordsPageAsync(1, 50);
            normalSamples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(10_000, page.TotalCount);
        }
        normalSamples.Sort();
        double normalMedian = normalSamples[normalSamples.Count / 2];

        long largeStarted = Stopwatch.GetTimestamp();
        IReadOnlyList<long> large = await database.QueryAsync(
            "SELECT Id FROM ScanRecords ORDER BY CreatedTime DESC LIMIT 10000;", static reader => reader.GetInt64(0));
        double largeMilliseconds = Stopwatch.GetElapsedTime(largeStarted).TotalMilliseconds;
        _output.WriteLine($"Environment={Environment.OSVersion}; Runtime={Environment.Version}; Rows=10000; NormalMedianMs={normalMedian:F3}; LargeMs={largeMilliseconds:F3}");

        Assert.Equal(10_000, large.Count);
        Assert.True(normalMedian < 50, $"Normal indexed/paged query median {normalMedian:F3}ms must be <50ms.");
        Assert.True(largeMilliseconds < 500, $"Large 10,000-row query {largeMilliseconds:F3}ms must be <500ms.");
    }
    private static async Task CreateV1DatabaseAsync(string path, bool malformedApplicationsTable)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY, applied_at INTEGER NOT NULL);
            INSERT INTO schema_migrations(version, applied_at) VALUES (1, 1);
            CREATE TABLE file_index (
                id INTEGER PRIMARY KEY AUTOINCREMENT, path TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
                publisher TEXT NULL, product_name TEXT NULL, size INTEGER NOT NULL DEFAULT 0,
                modified INTEGER NOT NULL, hash TEXT NULL, indexed_at INTEGER NOT NULL);
            CREATE INDEX idx_publisher ON file_index(publisher);
            CREATE INDEX idx_product ON file_index(product_name);
            CREATE TABLE scan_cache (
                cache_key TEXT PRIMARY KEY, module_id TEXT NOT NULL, payload TEXT NOT NULL,
                created_at INTEGER NOT NULL, expires_at INTEGER NOT NULL);
            CREATE TABLE quarantine_meta (
                entry_id TEXT PRIMARY KEY, original_path TEXT NOT NULL, quarantined_path TEXT NOT NULL,
                size INTEGER NOT NULL, quarantined_at INTEGER NOT NULL, expires_at INTEGER NOT NULL,
                reason TEXT NOT NULL, operation_id TEXT NULL, restored INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE residual_rules (
                publisher TEXT NOT NULL, product TEXT NOT NULL DEFAULT '', paths TEXT NOT NULL,
                registry TEXT NOT NULL, PRIMARY KEY (publisher, product));
            CREATE TABLE settings(key TEXT PRIMARY KEY, value TEXT NOT NULL, updated_at INTEGER NOT NULL);
            INSERT INTO settings(key, value, updated_at) VALUES ('Theme', 'Dark', 638909280000000000);
            """;
        await command.ExecuteNonQueryAsync();
        if (malformedApplicationsTable)
        {
            command.CommandText = "CREATE TABLE Applications(BadColumn TEXT);";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> ReadSchemaVersionAsync(string path)
    {
        SqliteConnection.ClearAllPools();
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_migrations;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
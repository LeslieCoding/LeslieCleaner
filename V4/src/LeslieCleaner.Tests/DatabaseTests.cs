using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests;

public sealed class DatabaseTests : IAsyncLifetime
{
    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;

    public DatabaseTests()
    {
        string databaseFile = Path.Combine(_workspace.PathService.DataDirectory, "LeslieCleaner.db");
        _database = new SqliteDatabase(databaseFile, _workspace.Log);
    }

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Initialize_UsesWalMode()
    {
        string? mode = await _database.ExecuteScalarAsync<string>("PRAGMA journal_mode;");
        Assert.Equal("wal", mode);
    }

    [Fact]
    public async Task VerifyIntegrity_HealthyDatabase_ReturnsTrue()
    {
        Assert.True(await _database.VerifyIntegrityAsync());
    }

    [Fact]
    public async Task Initialize_CreatesAllTables()
    {
        IReadOnlyList<string> tables = await _database.QueryAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table';",
            static reader => reader.GetString(0));

        Assert.Contains("file_index", tables);
        Assert.Contains("scan_cache", tables);
        Assert.Contains("quarantine_meta", tables);
        Assert.Contains("residual_rules", tables);
        Assert.Contains("settings", tables);
        Assert.Contains("UserSettings", tables);
        Assert.Contains("ScanRecords", tables);
        Assert.Contains("ScanItems", tables);
        Assert.Contains("CleanupRecords", tables);
        Assert.Contains("Applications", tables);
        Assert.Contains("StartupItems", tables);
        Assert.Contains("RegistryBackups", tables);
        Assert.Contains("OperationLogs", tables);
        Assert.Contains("AIRecommendations", tables);
    }

    [Fact]
    public async Task Initialize_CreatesRequiredIndexes()
    {
        IReadOnlyList<string> indexes = await _database.QueryAsync(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%';",
            static reader => reader.GetString(0));

        Assert.Contains("idx_publisher", indexes);
        Assert.Contains("idx_product", indexes);
        Assert.Contains("IX_Applications_Name", indexes);
        Assert.Contains("IX_Applications_Publisher", indexes);
        Assert.Contains("IX_StartupItems_Category_Enabled", indexes);
        Assert.Contains("IX_ScanItems_ScanRecordId_ItemType", indexes);
        Assert.Contains("IX_OperationLogs_Module_CreatedTime", indexes);
    }

    [Fact]
    public async Task Initialize_IsIdempotent()
    {
        await _database.InitializeAsync();
        await _database.InitializeAsync();

        IReadOnlyList<string> tables = await _database.QueryAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table';", static reader => reader.GetString(0));
        Assert.Contains("file_index", tables);
    }

    [Fact]
    public async Task Initialize_ConcurrentCalls_AppliesMigrationExactlyOnce()
    {
        var workspace = new TempWorkspace();
        try
        {
            await using var database = new SqliteDatabase(
                Path.Combine(workspace.PathService.DataDirectory, "concurrent.db"), workspace.Log);

            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => database.InitializeAsync()));

            long version = await database.ExecuteScalarAsync<long>("SELECT MAX(version) FROM schema_migrations;");
            long migrationCount = await database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schema_migrations;");
            Assert.Equal(2, version);
            Assert.Equal(2, migrationCount);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [Fact]
    public async Task Initialize_CancelledBeforeStart_CanBeRetried()
    {
        var workspace = new TempWorkspace();
        using var cancellation = new CancellationTokenSource();
        try
        {
            await using var database = new SqliteDatabase(
                Path.Combine(workspace.PathService.DataDirectory, "cancelled.db"), workspace.Log);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.InitializeAsync(cancellation.Token));
            await database.InitializeAsync();

            long version = await database.ExecuteScalarAsync<long>("SELECT MAX(version) FROM schema_migrations;");
            Assert.Equal(2, version);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [Fact]
    public async Task SeedImport_LoadsResidualRules()
    {
        var importer = new ResidualRuleImporter();

        IReadOnlyList<ResidualRule> rules = await importer.LoadAllAsync(_database);

        Assert.True(rules.Count >= 10, $"种子规则应不少于 10 条，实际 {rules.Count}");
        Assert.Contains(rules, r => r.Publisher.Contains("Tencent"));
        Assert.All(rules, rule =>
        {
            Assert.NotEmpty(rule.Publisher);
            Assert.NotEmpty(rule.Paths);
            Assert.NotEmpty(rule.RegistryKeys);
        });
    }

    [Fact]
    public async Task UpsertAndQueryByPublisher_RoundTrips()
    {
        var repository = new FileIndexRepository(_database);
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            new FileIndexRecord(@"C:\Apps\WeChat\WeChat.exe", "WeChat.exe", "Tencent", "WeChat", 1024, now, null, now),
            new FileIndexRecord(@"C:\Apps\WeChat\uninstall.exe", "uninstall.exe", "Tencent", "WeChat", 2048, now, "abc", now),
            new FileIndexRecord(@"C:\Apps\Chrome\chrome.exe", "chrome.exe", "Google LLC", "Chrome", 4096, now, null, now),
        };

        long written = await repository.UpsertBatchAsync(records);
        IReadOnlyList<FileIndexRecord> tencent = await repository.QueryByPublisherAsync("tencent");

        Assert.Equal(3, written);
        Assert.Equal(2, tencent.Count);
        Assert.All(tencent, r => Assert.Equal("Tencent", r.Publisher));
        Assert.Equal(3, await repository.CountAsync());
    }

    [Fact]
    public async Task Upsert_SamePath_ReplacesRow()
    {
        var repository = new FileIndexRepository(_database);
        var now = DateTimeOffset.UtcNow;
        string path = @"C:\Apps\app.exe";
        await repository.UpsertBatchAsync([new FileIndexRecord(path, "app.exe", "A", "App", 1, now, null, now)]);
        await repository.UpsertBatchAsync([new FileIndexRecord(path, "app.exe", "B", "App", 2, now, null, now)]);

        IReadOnlyList<FileIndexRecord> rows = await repository.QueryByProductAsync("App");

        Assert.Single(rows);
        Assert.Equal("B", rows[0].Publisher);
        Assert.Equal(2, rows[0].SizeBytes);
    }

    [Fact]
    public async Task QueryByProduct_UsesCaseInsensitiveMatch()
    {
        var repository = new FileIndexRepository(_database);
        var now = DateTimeOffset.UtcNow;
        await repository.UpsertBatchAsync([new FileIndexRecord(@"C:\x\code.exe", "code.exe", "Microsoft", "Visual Studio Code", 1, now, null, now)]);

        IReadOnlyList<FileIndexRecord> rows = await repository.QueryByProductAsync("visual studio code");

        Assert.Single(rows);
    }
}

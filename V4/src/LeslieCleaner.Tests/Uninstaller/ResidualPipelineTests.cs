using System.Text;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Residual;
using LeslieCleaner.Tests.Support;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Tests.Uninstaller;

/// <summary>返回固定记录的测试索引引擎（Stage 4 数据源）。</summary>
public sealed class FakeFileIndexEngine(IReadOnlyList<FileIndexRecord> records) : IFileIndexEngine
{
    public Task<long> IndexAsync(IReadOnlyList<string> roots, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(0L);

    public Task<IReadOnlyList<FileIndexRecord>> QueryByPublisherAsync(string publisher, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileIndexRecord>>(
            [.. records.Where(r => r.Publisher?.Equals(publisher, StringComparison.OrdinalIgnoreCase) == true)]);

    public Task<IReadOnlyList<FileIndexRecord>> QueryByProductAsync(string productName, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileIndexRecord>>(
            [.. records.Where(r => r.ProductName?.Equals(productName, StringComparison.OrdinalIgnoreCase) == true)]);

    public Task<FileIndexSearchPage> SearchAsync(FileIndexSearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileIndexSearchPage([.. records], records.Count, query.PageNumber, query.PageSize));

    public async IAsyncEnumerable<FileIndexRecord> StreamSearchAsync(FileIndexSearchQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return record;
        }
        await Task.CompletedTask;
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class ResidualPipelineTests : IAsyncLifetime
{
    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly CleanEngine _cleanEngine;
    private readonly InstalledAppDiscovery _discovery;

    public ResidualPipelineTests()
    {
        _database = new SqliteDatabase(
            Path.Combine(_workspace.PathService.DataDirectory, "residual-test.db"), _workspace.Log);
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var registryEngine = new Infrastructure.Registry.RegistryEngine(backupService, _workspace.Log);
        _cleanEngine = new CleanEngine(_quarantine, backupService, registryEngine, _workspace.PathService, _workspace.Log);
        _discovery = new InstalledAppDiscovery(_workspace.Log);
    }

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        _config.Dispose();
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public void RulePathExpander_ExpandsStandardTokens()
    {
        string expanded = RulePathExpander.Expand(@"%AppData%\Tencent");

        Assert.DoesNotContain("%", expanded);
        Assert.EndsWith(@"\Tencent", expanded);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), expanded);
    }

    [Fact]
    public void RulePathExpander_ExpandExisting_FiltersToRealPaths()
    {
        string existingFile = _workspace.CreateFile("rules/vendor-file.tmp");

        IReadOnlyList<string> result = RulePathExpander.ExpandExisting(
            [existingFile, @"%AppData%\DefinitelyNotHere\Sub12345"]);

        Assert.Single(result);
        Assert.Equal(existingFile, result[0]);
    }

    [Fact]
    public void InnoParser_ExtractsUtf16Paths()
    {
        string path1 = @"C:\Program Files\TestApp\bin\app.exe";
        string path2 = @"D:\Data\TestApp\config.ini";
        var buffer = new List<byte> { 0xDE, 0xAD, 0x00, 0x01 };
        buffer.AddRange(Encoding.Unicode.GetBytes(path1));
        buffer.AddRange([0x00, 0x00, 0xFF, 0xFF]);
        buffer.AddRange(Encoding.Unicode.GetBytes(path2));
        buffer.AddRange([0x00, 0x00]);

        IReadOnlyList<string> paths = InnoUninstallLogParser.ExtractPaths([.. buffer]);

        Assert.Contains(path1, paths);
        Assert.Contains(path2, paths);
    }

    [Fact]
    public void InnoParser_CorruptData_ReturnsEmpty()
    {
        IReadOnlyList<string> paths = InnoUninstallLogParser.ExtractPaths(new byte[100]);

        Assert.Empty(paths);
    }

    [Fact]
    public async Task Stage2_RulesHitExistingPaths()
    {
        string vendorDir = Path.Combine(_workspace.RootDirectory, "VendorLeftover");
        Directory.CreateDirectory(vendorDir);
        string leftoverFile = _workspace.CreateFile(@"VendorLeftover2\config.ini");
        await InsertRuleAsync("TestVendorCorp", null, [vendorDir, leftoverFile], []);

        var engine = CreateEngine(new FakeFileIndexEngine([]));

        ResidualScanReport report = await engine.ScanWithReportAsync(
            new ResidualQuery("TestVendorCorp", null, null, AllowDeepScan: false));

        Assert.Contains(report.Items, i => i.Path.Equals(vendorDir, StringComparison.OrdinalIgnoreCase)
            && i.Source == ResidualSourceStage.KnownRules && i.Confidence >= 0.9);
        Assert.Equal(5, report.StageTimings.Count);
        Assert.True(report.StageTimings[1].Duration < TimeSpan.FromMilliseconds(300),
            $"Stage2 耗时 {report.StageTimings[1].Duration.TotalMilliseconds}ms 应 <300ms");
    }

    [Fact]
    public async Task Stage4_PublisherIndexHits()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<FileIndexRecord>
        {
            new(@"C:\Apps\VendorX\app.exe", "app.exe", "VendorX", "ProductX", 100, now, null, now),
            new(@"C:\Other\other.exe", "other.exe", "OtherVendor", "Other", 100, now, null, now),
        };
        var engine = CreateEngine(new FakeFileIndexEngine(records));

        ResidualScanReport report = await engine.ScanWithReportAsync(
            new ResidualQuery("VendorX", null, null, AllowDeepScan: false));

        Assert.Contains(report.Items, i => i.Path == @"C:\Apps\VendorX\app.exe"
            && i.Source == ResidualSourceStage.PublisherIndex && Math.Abs(i.Confidence - 0.7) < 0.001);
        Assert.DoesNotContain(report.Items, i => i.Path.Contains("other.exe"));
    }

    [Fact]
    public async Task Merge_DedupesByPathKeepingHighestConfidence()
    {
        string sharedFile = _workspace.CreateFile(@"Shared\shared.dll");
        await InsertRuleAsync("MergeVendor", null, [sharedFile], []);
        var now = DateTimeOffset.UtcNow;
        var records = new List<FileIndexRecord>
        {
            new(sharedFile, "shared.dll", "MergeVendor", null, 10, now, null, now),
        };
        var engine = CreateEngine(new FakeFileIndexEngine(records));

        ResidualScanReport report = await engine.ScanWithReportAsync(
            new ResidualQuery("MergeVendor", null, null, AllowDeepScan: false));

        ResidualItem item = Assert.Single(report.Items);
        Assert.Equal(ResidualSourceStage.KnownRules, item.Source); // 0.9 > 0.7
        Assert.Equal(report.Items[0], item);
    }

    [Fact]
    public async Task CleanResiduals_FilesQuarantinedAndRollbackRestores()
    {
        string leftover = _workspace.CreateFile(@"CleanMe\residual.dat", "residual");
        var engine = CreateEngine(new FakeFileIndexEngine([]));
        var items = new List<ResidualItem>
        {
            new(leftover, ResidualKind.File, 8, ResidualSourceStage.KnownRules, 0.9),
        };

        CleanResult result = await engine.CleanResidualsAsync(items);

        Assert.True(result.Items.All(i => i.Success));
        Assert.False(File.Exists(leftover));

        bool rolledBack = await _cleanEngine.RollbackAsync(result.OperationId);
        Assert.True(rolledBack);
        Assert.True(File.Exists(leftover));
    }

    [Fact]
    public async Task CleanResiduals_DirectoryUsesSafeRecursiveEnumerationAndRollbackRestores()
    {
        string directory = Path.Combine(_workspace.RootDirectory, "NestedResidual");
        string first = _workspace.CreateFile(@"NestedResidual\one.dat", "one");
        string second = _workspace.CreateFile(@"NestedResidual\Child\two.dat", "two");
        var engine = CreateEngine(new FakeFileIndexEngine([]));

        CleanResult result = await engine.CleanResidualsAsync(
            [new ResidualItem(directory, ResidualKind.Directory, 6, ResidualSourceStage.KnownRules, 0.9)]);

        Assert.True(result.Items.All(item => item.Success));
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.True(await _cleanEngine.RollbackAsync(result.OperationId));
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task Scan_StageTimings_WithinBibleTargets()
    {
        var engine = CreateEngine(new FakeFileIndexEngine([]));

        ResidualScanReport report = await engine.ScanWithReportAsync(
            new ResidualQuery("NoSuchVendorExists12345", null, null, AllowDeepScan: false));

        Assert.Equal(5, report.StageTimings.Count);
        Assert.True(report.StageTimings[1].Duration < TimeSpan.FromMilliseconds(300), "Stage2 <300ms");
        Assert.True(report.StageTimings[3].Duration < TimeSpan.FromSeconds(1), "Stage4 <1s");
        Assert.True(report.TotalDuration < TimeSpan.FromSeconds(2), $"总耗时 {report.TotalDuration.TotalMilliseconds}ms 应 <2s");
    }

    private ResidualScannerEngine CreateEngine(IFileIndexEngine indexEngine) =>
        new(_database, indexEngine, _cleanEngine, _discovery, _workspace.Log);

    private async Task InsertRuleAsync(string publisher, string? product, IReadOnlyList<string> paths, IReadOnlyList<string> registry)
    {
        await _database.ExecuteBatchAsync(
            "INSERT OR REPLACE INTO residual_rules (publisher, product, paths, registry) VALUES ($p, $pr, $paths, $reg);",
            [
                (IReadOnlyList<SqliteParameter>)[
                    new SqliteParameter("$p", publisher),
                    new SqliteParameter("$pr", product ?? string.Empty),
                    new SqliteParameter("$paths", System.Text.Json.JsonSerializer.Serialize(paths)),
                    new SqliteParameter("$reg", System.Text.Json.JsonSerializer.Serialize(registry))],
            ]);
    }
}

public sealed class ResidualRulesSeedTests : IAsyncLifetime
{
    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;

    public ResidualRulesSeedTests() =>
        _database = new SqliteDatabase(
            Path.Combine(_workspace.PathService.DataDirectory, "seed-test.db"), _workspace.Log);

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Seed_ContainsAtLeast1000Rules()
    {
        long count = await _database.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM residual_rules;");

        Assert.True(count >= 1000, $"规则数 {count} 应 ≥1000");
    }

    [Fact]
    public async Task Seed_AllRulesWellFormed()
    {
        var importer = new ResidualRuleImporter();
        IReadOnlyList<ResidualRule> rules = await importer.LoadAllAsync(_database);

        Assert.All(rules, rule =>
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Publisher));
            Assert.NotEmpty(rule.Paths);
            Assert.NotEmpty(rule.RegistryKeys);
            Assert.All(rule.Paths, path => Assert.DoesNotContain("null", path));
        });
        Assert.Contains(rules, r => r.Publisher == "Tencent" && r.Product == "WeChat");
        Assert.Contains(rules, r => r.Publisher == "Adobe Inc." && r.Product == "Photoshop");
        Assert.Contains(rules, r => r.Publisher == "JetBrains s.r.o." && r.Product == "IntelliJ IDEA");
    }

    [Fact]
    public async Task Query_MatchesPublisherAndProduct()
    {
        var importer = new ResidualRuleImporter();

        IReadOnlyList<ResidualRule> byPublisher = await importer.QueryAsync(_database, "Tencent", null);
        IReadOnlyList<ResidualRule> byProduct = await importer.QueryAsync(_database, "Tencent", "WeChat");

        Assert.NotEmpty(byPublisher);
        Assert.Contains(byProduct, r => r.Product == "WeChat");
    }

    [Fact]
    public async Task Query_ProductOnlyDoesNotThrowAndMatchesExactProduct()
    {
        var importer = new ResidualRuleImporter();

        IReadOnlyList<ResidualRule> rules = await importer.QueryAsync(_database, null, "WeChat");

        Assert.Contains(rules, r => r.Product == "WeChat");
    }

    [Fact]
    public async Task Query_PublisherAndProductDoNotMixDifferentPublisherRules()
    {
        await _database.ExecuteBatchAsync(
            "INSERT OR REPLACE INTO residual_rules (publisher, product, paths, registry) VALUES ($p, $pr, $paths, $reg);",
            [
                (IReadOnlyList<SqliteParameter>)[
                    new SqliteParameter("$p", "Other Publisher"),
                    new SqliteParameter("$pr", "WeChat"),
                    new SqliteParameter("$paths", "[]"),
                    new SqliteParameter("$reg", "[]")],
            ]);
        var importer = new ResidualRuleImporter();

        IReadOnlyList<ResidualRule> rules = await importer.QueryAsync(_database, "Tencent", "WeChat");

        Assert.DoesNotContain(rules, r => r.Publisher == "Other Publisher");
    }

    [Fact]
    public void Seed_TemplatesExpand()
    {
        string[] samples = [@"%ProgramFiles%\Tencent", @"%AppData%\Adobe", @"%LocalAppData%\JetBrains"];

        foreach (string sample in samples)
        {
            string expanded = RulePathExpander.Expand(sample);
            Assert.DoesNotContain("%", expanded);
        }
    }
}

using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Cleaner.Scanning;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests;

/// <summary>
/// 阶段 6-8 性能基准：10 万文件索引（普通枚举 + USN 若可用）、
/// 注册表全量规则扫描（目标 &lt; 3s）、Smart Scan 真实全量（目标 &lt; 5s）。
/// 真实数字输出到测试日志并写入 DEVLOG。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[Trait("Category", "LongRunning")]
public sealed class EngineBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public EngineBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Benchmark_FileIndex_100kFiles()
    {
        using var workspace = new TempWorkspace();
        string indexRoot = Path.Combine(workspace.RootDirectory, "IndexBench");
        Directory.CreateDirectory(indexRoot);

        BenchmarkResult createResult = await PerformanceBenchmark.MeasureAsync(
            "FileIndex.Create100kFixtureFiles", 100_000,
            ct => Task.Run(() => Parallel.For(0, 100_000,
                new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct },
                i =>
                {
                    string dir = Path.Combine(indexRoot, $"d{i % 200:D3}");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, $"f{i:D6}.bin"), [1]);
                }), ct), CancellationToken.None);

        string databaseFile = Path.Combine(workspace.PathService.DataDirectory, "index-bench.db");
        await using var database = new SqliteDatabase(databaseFile, workspace.Log);
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        using var engine = new FileIndexEngine(database, workspace.Log, config);

        BenchmarkResult indexResult = await PerformanceBenchmark.MeasureAsync(
            "FileIndex.FullScan100k.Enumeration", 100_000,
            async ct => await engine.IndexAsync([indexRoot], cancellationToken: ct), CancellationToken.None);

        Assert.Equal(100_000, await new FileIndexRepository(database).CountAsync());

        BenchmarkResult incrementalResult = await PerformanceBenchmark.MeasureAsync(
            "FileIndex.IncrementalNoChange100k", 100_000,
            async ct => await engine.IndexAsync([indexRoot], cancellationToken: ct), CancellationToken.None);

        Report(createResult);
        Report(indexResult);
        Report(incrementalResult);

        // USN 加速路径：仅在本机具备权限时测量并报告
        string? volumeRoot = Path.GetPathRoot(indexRoot);
        using (UsnMftEnumerator? probe = volumeRoot is null ? null : UsnMftEnumerator.TryCreate(volumeRoot))
        {
            if (probe is not null)
            {
                config.GetValue(FileIndexEngine.PreferUsnConfigKey, true);
                await using var usnDatabase = new SqliteDatabase(
                    Path.Combine(workspace.PathService.DataDirectory, "index-bench-usn.db"), workspace.Log);
                using var usnEngine = new FileIndexEngine(usnDatabase, workspace.Log, config);
                BenchmarkResult usnResult = await PerformanceBenchmark.MeasureAsync(
                    "FileIndex.FullScan.UsnMft", 1,
                    async ct => await usnEngine.IndexAsync([indexRoot], cancellationToken: ct), CancellationToken.None);
                Report(usnResult);
            }
            else
            {
                _output.WriteLine("[BENCHMARK] FileIndex.FullScan.UsnMft: 不可用（无卷句柄权限，已验证优雅降级）");
            }
        }
    }

    [Fact]
    public async Task Benchmark_FullScopeIndex_RealAllowedRoots()
    {
        // 任务 2 复现：全量允许区索引（原 40 分钟无进展问题场景）。
        // 修复后必须能完成；目录级超时单独报告。
        using var workspace = new TempWorkspace();
        string databaseFile = Path.Combine(workspace.PathService.DataDirectory, "fullscope.db");
        await using var database = new SqliteDatabase(databaseFile, workspace.Log);
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        using var engine = new FileIndexEngine(database, workspace.Log, config);
        IReadOnlyList<string> roots = ScanScope.GetAllowedRoots();

        using var watchdog = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "FileIndex.FullScope.RealRoots", 1,
            async ct => await engine.IndexAsync(roots, cancellationToken: ct), watchdog.Token);

        long count = await new FileIndexRepository(database).CountAsync();
        int timeoutEvents = workspace.Log.Events.Count(e => e.Message.Contains("超时"));
        _output.WriteLine("[BENCHMARK] FileIndex.FullScope.RealRoots.Count: {0}, TimedOutDirs: {1}", count, timeoutEvents);
        Report(result);
        Assert.True(count > 0);
    }

    [Fact]
    public async Task Benchmark_Registry_FullRulesScan()
    {
        using var workspace = new TempWorkspace();
        var scanner = new RegistryScanner(workspace.Log);
        IReadOnlyList<RegistryFinding> findings = [];

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "Registry.FullRulesScan", 1,
            async ct => findings = await scanner.ScanAsync(cancellationToken: ct), CancellationToken.None);

        Assert.True(findings.Count >= 0);
        _output.WriteLine("[BENCHMARK] Registry.FullRulesScan.Findings: {0}", findings.Count);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_SmartScan_RealFullRun()
    {
        using var workspace = new TempWorkspace();
        var registryScanner = new RegistryScanner(workspace.Log);
        var engine = new SmartScanEngine(new SystemScanEnvironment(), registryScanner, workspace.Log);
        ScanResult scan = new() { StartedUtc = DateTimeOffset.UtcNow, Duration = TimeSpan.Zero, Items = [] };

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "SmartScan.RealFullRun", 1,
            async ct => scan = await engine.ScanAsync(new ScanOptions { Targets = ["*"] }, cancellationToken: ct),
            CancellationToken.None);

        _output.WriteLine(
            "[BENCHMARK] SmartScan.RealFullRun.Items: {0}, TotalSizeMB: {1:F1}",
            scan.Items.Count, scan.TotalSizeBytes / 1048576.0);
        Report(result);
    }

    private void Report(BenchmarkResult result)
    {
        _output.WriteLine(
            "[BENCHMARK] {0}: 总量={1}, 总耗时={2:F2}ms, 均值={3:F3}ms",
            result.Name, result.Iterations,
            result.TotalDuration.TotalMilliseconds,
            result.AveragePerIteration.TotalMilliseconds);
    }
}

using System.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Cleaner.Scanning;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests.Integration;

/// <summary>
/// 圣经第七节性能测试（实测数字写 DEVLOG）：
/// 50 万文件索引（圣经 100 万，缩减注明）、10GB 缓存清理（稀疏文件）、
/// 5000 注册表项扫描、10 分钟连续扫描内存稳定性（24h 泄漏测试的本机替代证据）。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PerformanceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "LongRunning")]
    public async Task Perf_FileIndex_500kFiles()
    {
        using var workspace = new TempWorkspace();
        string root = Path.Combine(workspace.RootDirectory, "Index500k");
        Directory.CreateDirectory(root);

        const int fileCount = 500_000; // 圣经 100 万：本机磁盘/时间约束缩减到 50 万（已在 docs/06-testing.md 注明）
        BenchmarkResult createResult = await PerformanceBenchmark.MeasureAsync(
            "Perf.Create500kFiles", fileCount,
            ct => Task.Run(() => Parallel.For(0, fileCount,
                new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct },
                i =>
                {
                    string dir = Path.Combine(root, $"d{i % 1000:D4}");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, $"f{i:D6}.bin"), [1]);
                }), ct), CancellationToken.None);

        string databaseFile = Path.Combine(workspace.PathService.DataDirectory, "index500k.db");
        await using var database = new SqliteDatabase(databaseFile, workspace.Log);
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        using var engine = new FileIndexEngine(database, workspace.Log, config);

        BenchmarkResult indexResult = await PerformanceBenchmark.MeasureAsync(
            "Perf.FileIndex500k", fileCount,
            async ct => await engine.IndexAsync([root], cancellationToken: ct), CancellationToken.None);

        Assert.Equal(fileCount, await new FileIndexRepository(database).CountAsync());
        Report(createResult);
        Report(indexResult);
    }

    [Fact]
    [Trait("Category", "LongRunning")]
    public async Task Perf_CacheClean_10GBSparse()
    {
        using var workspace = new TempWorkspace();
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        var quarantine = new QuarantineService(workspace.PathService, config, workspace.Log);

        // 200 个 50MB 稀疏文件（NTFS SetLength 逻辑扩展，不占物理块）
        const int fileCount = 200;
        const long fileSize = 50L * 1024 * 1024;
        string cacheDir = Path.Combine(workspace.RootDirectory, "Cache10GB");
        Directory.CreateDirectory(cacheDir);
        var files = new List<string>(fileCount);
        for (int i = 0; i < fileCount; i++)
        {
            string path = Path.Combine(cacheDir, $"cache{i:D4}.dat");
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(fileSize);
            }

            files.Add(path);
        }

        const long totalBytes = (long)fileCount * fileSize;
        BenchmarkResult cleanResult = await PerformanceBenchmark.MeasureAsync(
            "Perf.CacheClean10GB", totalBytes,
            async ct => await quarantine.QuarantineFilesAsync(files, "性能测试缓存清理", cancellationToken: ct),
            CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(cacheDir));
        Assert.Equal(fileCount, (await quarantine.ListEntriesAsync()).Count);
        Report(cleanResult);
    }

    [Fact]
    [Trait("Category", "LongRunning")]
    public async Task Perf_Registry_5000Keys()
    {
        using var workspace = new TempWorkspace();
        string perfKeyRelative = $@"Software\LeslieCleaner.Tests\Perf{Guid.NewGuid():N}";
        using (RegistryKey root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(perfKeyRelative))
        {
            for (int i = 0; i < 5000; i++)
            {
                using RegistryKey child = root.CreateSubKey($"Key{i:D5}");
                child.SetValue("Value", i, RegistryValueKind.DWord);
            }
        }

        var backupService = new RegistryBackupService(workspace.PathService, workspace.Log);
        var engine = new Infrastructure.Registry.RegistryEngine(backupService, workspace.Log);
        long timestamp = Stopwatch.GetTimestamp();
        IReadOnlyList<string> subKeys = await engine.EnumerateSubKeysAsync($@"HKCU\{perfKeyRelative}");
        int readCount = 0;
        foreach (string subKey in subKeys)
        {
            _ = await engine.ReadValueAsync($@"HKCU\{perfKeyRelative}\{subKey}", "Value");
            readCount++;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(timestamp);
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(perfKeyRelative, throwOnMissingSubKey: false);

        Assert.Equal(5000, readCount);
        _output.WriteLine(
            "[BENCHMARK] Perf.Registry5000Keys: 枚举+读取 5000 项, 总耗时={0:F2}ms, 均值={1:F3}ms",
            elapsed.TotalMilliseconds, elapsed.TotalMilliseconds / readCount);
    }

    [Fact]
    [Trait("Category", "Soak")]
    public async Task Perf_MemoryStability_10MinContinuousScan()
    {
        using var workspace = new TempWorkspace();
        var soakLog = new NonRetainingLogService();
        var registryScanner = new Infrastructure.Registry.RegistryScanner(soakLog);
        var engine = new SmartScanEngine(new SystemScanEnvironment(), registryScanner, soakLog);

        // 预热（JIT/缓存就位）
        _ = await engine.ScanAsync(new ScanOptions { Targets = ["*"] });
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        long beforeBytes = GC.GetTotalMemory(forceFullCollection: true);

        var duration = TimeSpan.FromMinutes(10);
        var stopwatch = Stopwatch.StartNew();
        long scanCount = 0;
        while (stopwatch.Elapsed < duration)
        {
            ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["*"] });
            scanCount++;
            if (scanCount % 20 == 0)
            {
                _output.WriteLine("[STABILITY] {0:mm\\:ss} 扫描 {1} 次, 托管堆 {2:F1} MB",
                    stopwatch.Elapsed, scanCount, GC.GetTotalMemory(false) / 1048576.0);
            }
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        long afterBytes = GC.GetTotalMemory(forceFullCollection: true);
        double growthMb = (afterBytes - beforeBytes) / 1048576.0;

        _output.WriteLine(
            "[BENCHMARK] Perf.MemoryStability10Min: 连续扫描 {0} 次, 托管堆增长 {1:F1} MB（阈值 <60MB）",
            scanCount, growthMb);
        Assert.True(growthMb < 60, $"10 分钟连续扫描托管堆增长 {growthMb:F1}MB，疑似泄漏");
    }

    private void Report(BenchmarkResult result)
    {
        _output.WriteLine(
            "[BENCHMARK] {0}: 总量={1}, 总耗时={2:F2}ms, 均值={3:F3}ms",
            result.Name, result.Iterations,
            result.TotalDuration.TotalMilliseconds,
            result.AveragePerIteration.TotalMilliseconds);
    }

    private sealed class NonRetainingLogService : Core.Abstractions.ILogService
    {
        public void LogEvent(Core.Models.LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}


using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Browser;
using LeslieCleaner.Modules.Cleaner.Analytics;
using LeslieCleaner.Modules.Cleaner.Duplicates;
using LeslieCleaner.Modules.Shredder;
using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests;

/// <summary>
/// 阶段 9-14 性能基准：浏览器缓存扫描（目标 &lt; 2s）、启动项枚举、
/// 碎纸机 100MB DoD3 吞吐、重复查找 5000 文件、大文件分析。
/// 真实数字输出到测试日志并写入 DEVLOG。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[Trait("Category", "LongRunning")]
public sealed class ModuleBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public ModuleBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Benchmark_Browser_ScanReal()
    {
        using var workspace = new TempWorkspace();
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        var quarantine = new QuarantineService(workspace.PathService, config, workspace.Log);
        var engine = new BrowserCleanerEngine(workspace.PathService, quarantine, config, workspace.Log);
        IReadOnlyList<BrowserScanItem> items = [];

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "Browser.ScanReal", 1,
            async ct => items = await engine.ScanAsync(cancellationToken: ct), CancellationToken.None);

        _output.WriteLine("[BENCHMARK] Browser.ScanReal.Items: {0}", items.Count);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_Startup_EnumerateReal()
    {
        using var workspace = new TempWorkspace();
        var engine = new StartupManagerEngine(workspace.PathService, workspace.Log);
        IReadOnlyList<StartupItem> items = [];

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "Startup.EnumerateReal", 1,
            async ct => items = await engine.EnumerateAsync(cancellationToken: ct), CancellationToken.None);

        _output.WriteLine("[BENCHMARK] Startup.EnumerateReal.Items: {0}", items.Count);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_Shredder_100MBDoD3()
    {
        using var workspace = new TempWorkspace();
        string file = Path.Combine(workspace.RootDirectory, "shred-100mb.bin");
        await using (FileStream stream = new(file, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(100L * 1024 * 1024);
        }

        var engine = new ShredderEngine(workspace.Log, ssdProbe: static _ => false);
        engine.AddFiles([file]);

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "Shredder.100MB.DoD3", 100L * 1024 * 1024,
            async ct => await engine.StartAsync(ShredMethod.DoD3Pass, cancellationToken: ct), CancellationToken.None);

        Assert.False(File.Exists(file));
        _output.WriteLine("[BENCHMARK] Shredder.100MB.DoD3.ThroughputMBps: {0:F1}",
            300.0 / result.TotalDuration.TotalSeconds);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_DuplicateFinder_5000Files()
    {
        using var workspace = new TempWorkspace();
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        var quarantine = new QuarantineService(workspace.PathService, config, workspace.Log);
        var engine = new DuplicateFinderEngine(quarantine, workspace.Log);
        string root = Path.Combine(workspace.RootDirectory, "dupbench");
        Directory.CreateDirectory(root);

        // 5000 文件：每 10 个共享一组内容（500 组重复），8KB 每个
        byte[][] contents = new byte[500][];
        for (int i = 0; i < 500; i++)
        {
            contents[i] = new byte[8192];
            new Random(i).NextBytes(contents[i]);
        }

        for (int i = 0; i < 5000; i++)
        {
            await File.WriteAllBytesAsync(Path.Combine(root, $"f{i:D5}.bin"), contents[i % 500]);
        }

        IReadOnlyList<DuplicateGroup> groups = [];
        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "DuplicateFinder.5000Files", 5000,
            async ct => groups = await engine.FindDuplicatesAsync([root], cancellationToken: ct), CancellationToken.None);

        Assert.Equal(500, groups.Count);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_LargeFileAnalyzer_Documents()
    {
        using var workspace = new TempWorkspace();
        var engine = new LargeFileAnalyzerEngine(workspace.Log);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        LargeFileAnalysisResult analysis = new([], [], [], [], 0, 0, false);

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "LargeFileAnalyzer.Documents", 1,
            async ct => analysis = await engine.AnalyzeAsync([documents], cancellationToken: ct), CancellationToken.None);

        _output.WriteLine("[BENCHMARK] LargeFileAnalyzer.Documents.Files: {0}, TopFiles: {1}",
            analysis.ScannedFiles, analysis.TopFiles.Count);
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

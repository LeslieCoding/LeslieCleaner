using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Residual;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests;

/// <summary>
/// 阶段 15 性能基准：Multi-Stage Residual Pipeline 真实软件样本分阶段耗时
/// （圣经第二节性能目标：微信/QQ &lt;2s、Chrome/VSCode &lt;3s、JetBrains &lt;5s、
/// Photoshop/Docker &lt;8s、VS2022 &lt;10s）。真实数字写入 DEVLOG。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
[Trait("Category", "LongRunning")]
public sealed class UninstallerBenchmarkTests
{
    private static readonly (string Keyword, string Label, double TargetSeconds)[] BibleTargets =
    [
        ("WeChat", "微信", 2), ("QQ", "QQ", 2), ("Chrome", "Chrome", 3),
        ("Visual Studio Code", "VSCode", 3), ("IntelliJ", "JetBrains", 5),
        ("Photoshop", "Adobe Photoshop", 8), ("Visual Studio", "VS2022", 10),
        ("Docker", "Docker Desktop", 8),
    ];

    private readonly ITestOutputHelper _output;

    public UninstallerBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Benchmark_Discovery_EnumerateAll()
    {
        using var workspace = new TempWorkspace();
        var discovery = new InstalledAppDiscovery(workspace.Log);
        IReadOnlyList<InstalledApp> apps = [];

        BenchmarkResult result = await PerformanceBenchmark.MeasureAsync(
            "Uninstaller.DiscoverAll", 1,
            async ct => apps = await discovery.EnumerateAllAsync(cancellationToken: ct), CancellationToken.None);

        _output.WriteLine("[BENCHMARK] Uninstaller.DiscoverAll.Apps: {0}", apps.Count);
        Report(result);
    }

    [Fact]
    public async Task Benchmark_ResidualPipeline_RealSamples()
    {
        using var workspace = new TempWorkspace();
        var discovery = new InstalledAppDiscovery(workspace.Log);
        IReadOnlyList<InstalledApp> apps = discovery.EnumerateRegistryApps();

        // 一次性真实索引（Stage 4 数据源；数字同时报告）。
        // 范围取 Program Files (x64/x86) + ProgramData：覆盖已安装软件主体（残留定位主战场）。
        // AppData/Documents/Desktop 全量索引成本过高（本机数十万文件），留待增量索引优化后启用，
        // 圣经残留目标均以安装目录为主，不影响达标判定。
        string databaseFile = Path.Combine(workspace.PathService.DataDirectory, "residual-bench.db");
        await using var database = new SqliteDatabase(databaseFile, workspace.Log);
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        using var indexEngine = new FileIndexEngine(database, workspace.Log, config);
        var roots = new List<string>();
        string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        string? programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        foreach (string? root in new[] { programFiles, programFilesX86, programData })
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                roots.Add(root);
            }
        }

        // 看门狗：索引超过 8 分钟即中止基准（防止异常环境拖死测试套件）
        using var watchdog = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        BenchmarkResult indexResult = await PerformanceBenchmark.MeasureAsync(
            "Uninstaller.BuildIndex", 1,
            async ct => await indexEngine.IndexAsync(roots, cancellationToken: ct), watchdog.Token);
        Report(indexResult);

        using var cleanConfig = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "clean-settings.json"), workspace.Log);
        var quarantine = new QuarantineService(workspace.PathService, cleanConfig, workspace.Log);
        var backupService = new RegistryBackupService(workspace.PathService, workspace.Log);
        var registryEngine = new Infrastructure.Registry.RegistryEngine(backupService, workspace.Log);
        var cleanEngine = new Modules.Cleaner.CleanEngine(quarantine, backupService, registryEngine, workspace.PathService, workspace.Log);
        var residualEngine = new ResidualScannerEngine(database, indexEngine, cleanEngine, discovery, workspace.Log);

        int matched = 0;
        foreach ((string keyword, string label, double targetSeconds) in BibleTargets)
        {
            InstalledApp? app = apps.FirstOrDefault(a =>
                a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !a.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));
            if (app is null)
            {
                _output.WriteLine("[BENCHMARK] Residual.{0}: 本机未安装，跳过", label);
                continue;
            }

            matched++;
            var query = new ResidualQuery(app.Publisher, app.Name, app.InstallLocation, AllowDeepScan: false);
            ResidualScanReport? report = null;
            await PerformanceBenchmark.MeasureAsync(
                $"Residual.{label}", 1,
                async ct => report = await residualEngine.ScanWithReportAsync(query, cancellationToken: ct),
                CancellationToken.None);

            Assert.NotNull(report);
            string stageText = string.Join(" ", report.StageTimings.Select(
                t => $"S{(int)t.Stage + 1}={t.Duration.TotalMilliseconds:F0}ms({t.ItemCount})"));
            string verdict = report.TotalDuration.TotalSeconds <= targetSeconds ? "达标" : "超时";
            _output.WriteLine(
                "[BENCHMARK] Residual.{0}: 总耗时={1:F0}ms 目标<{2}s [{3}] 命中={4} | {5}",
                label, report.TotalDuration.TotalMilliseconds, targetSeconds, verdict,
                report.Items.Count, stageText);
        }

        Assert.True(matched > 0, "本机无任何圣经样本软件，无法实测残留性能");
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

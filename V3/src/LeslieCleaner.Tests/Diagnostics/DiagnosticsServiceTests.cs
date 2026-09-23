using System.IO.Compression;
using System.Text.Json;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Diagnostics;

public sealed class DiagnosticsServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly DiagnosticsService _service;

    public DiagnosticsServiceTests()
    {
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        _service = new DiagnosticsService(_workspace.PathService,
            new JsonConfigService(Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log),
            _workspace.Log);

        // 近 7 天日志（含各级别与异常类型）
        string today = DateTime.Now.ToString("yyyyMMdd");
        File.WriteAllText(
            Path.Combine(_workspace.PathService.LogDirectory, $"lesliecleaner-{today}.log"),
            """
            2026-07-24 10:00:00.000 +08:00 [INF] [ScanStart] 扫描开始
            2026-07-24 10:00:01.000 +08:00 [WRN] [DeleteStart] 文件移入隔离区开始
            2026-07-24 10:00:02.000 +08:00 [ERR] [Exception] [测试上下文A] 异常
            System.IO.IOException: 模拟 IO 失败
            2026-07-24 10:00:03.000 +08:00 [ERR] [Exception] [测试上下文B] 异常
            System.IO.IOException: 再次失败
            2026-07-24 10:00:04.000 +08:00 [ERR] [Exception] [测试上下文C] 异常
            System.UnauthorizedAccessException: 模拟权限失败
            """);

        // 超龄日志（不应计入 30 天汇总）
        string oldFile = Path.Combine(_workspace.PathService.LogDirectory, "lesliecleaner-20200101.log");
        File.WriteAllText(oldFile, "2020-01-01 00:00:00.000 +08:00 [ERR] [Exception] 陈旧异常\n");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-60));
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task BuildErrorSummary_AggregatesLevelsAndTypes()
    {
        ErrorSummary summary = await _service.BuildErrorSummaryAsync();

        Assert.Equal(1, summary.TotalWarnings);
        Assert.Equal(3, summary.TotalErrors);
        Assert.Contains(summary.TopExceptions, e => e.TypeName == "System.IO.IOException" && e.Count == 2);
        Assert.Contains(summary.TopExceptions, e => e.TypeName == "System.UnauthorizedAccessException" && e.Count == 1);
        Assert.Contains(summary.ByEventKind, k => k.EventKind == "ScanStart");
    }

    [Fact]
    public async Task GenerateReport_ZipContainsAllParts()
    {
        string outputDir = Path.Combine(_workspace.RootDirectory, "reports");

        string zipPath = await _service.GenerateReportAsync(outputDir);

        Assert.True(File.Exists(zipPath));
        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains(names, n => n.StartsWith("logs/"));
        Assert.Contains("error-summary.json", names);
        Assert.Contains("system-info.json", names);
        Assert.Contains("config-snapshot.json", names);

        using var summaryReader = new StreamReader(archive.GetEntry("error-summary.json")!.Open());
        using JsonDocument summaryJson = JsonDocument.Parse(await summaryReader.ReadToEndAsync());
        Assert.Equal(3, summaryJson.RootElement.GetProperty("TotalErrors").GetInt32());

        using var sysInfoReader = new StreamReader(archive.GetEntry("system-info.json")!.Open());
        string sysInfo = await sysInfoReader.ReadToEndAsync();
        Assert.Contains("OsVersion", sysInfo);
        Assert.Contains("DotNetVersion", sysInfo);
    }

    [Fact]
    public void SanitizedConfig_RedactsSensitiveValues()
    {
        string configFile = Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configFile)!);
        File.WriteAllText(configFile, """
            {
                "quarantine": { "retentionDays": 30 },
                "update": { "serverUrl": "https://update.example.com/api" },
                "general": { "maxConcurrency": 8 }
            }
            """);

        string json = _service.BuildSanitizedConfigJson();

        Assert.DoesNotContain("update.example.com", json);
        Assert.Contains("retentionDays", json);
        Assert.Contains("maxConcurrency", json);
        Assert.Contains("***", json);
    }
}

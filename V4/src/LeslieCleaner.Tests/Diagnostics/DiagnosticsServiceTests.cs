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

        string today = DateTime.Now.ToString("yyyyMMdd");
        string ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        File.WriteAllText(
            Path.Combine(_workspace.PathService.LogDirectory, $"lesliecleaner-{today}.log"),
            $"""
            {ts} [INF] [ScanStart] Scan started
            {ts} [WRN] [DeleteStart] Delete started
            {ts} [ERR] [Exception] Error A
            System.IO.IOException: Simulated IO failure
            {ts} [ERR] [Exception] Error B
            System.IO.IOException: Simulated IO failure again
            {ts} [ERR] [Exception] Error C
            System.UnauthorizedAccessException: Simulated access failure
            """);
        // 瓒呴緞鏃ュ織锛堜笉搴旇鍏?30 澶╂眹鎬伙級
        string oldFile = Path.Combine(_workspace.PathService.LogDirectory, "lesliecleaner-20200101.log");
        File.WriteAllText(oldFile, "2020-01-01 00:00:00.000 +08:00 [ERR] [Exception] 闄堟棫寮傚父\n");
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

    [Fact]
    public async Task GenerateReport_RedactsSensitiveLogContentAndAddsIntegrityManifest()
    {
        string log = Path.Combine(_workspace.PathService.LogDirectory, "sensitive.log");
        File.WriteAllText(log, "user=C:\\Users\\Alice\\private.txt token=top-secret alice@example.com Bearer abc.def");

        string zipPath = await _service.GenerateReportAsync(Path.Combine(_workspace.RootDirectory, "safe-reports"));

        using var archive = ZipFile.OpenRead(zipPath);
        Assert.NotNull(archive.GetEntry("integrity-manifest.json"));
        ZipArchiveEntry exportedLog = archive.GetEntry("logs/log-002.log") ?? throw new InvalidDataException("Sanitized log entry missing.");
        using var reader = new StreamReader(exportedLog.Open());
        string content = await reader.ReadToEndAsync();
        Assert.DoesNotContain("Alice", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top-secret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("alice@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc.def", content, StringComparison.Ordinal);
        Assert.Contains("[REDACTED", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateReport_RejectsRelativeOutputAndLeavesNoArchive()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GenerateReportAsync("relative-reports"));
        Assert.Empty(Directory.EnumerateFiles(_workspace.RootDirectory, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task GenerateReport_PreCanceled_LeavesNoPartialFiles()
    {
        string output = Path.Combine(_workspace.RootDirectory, "cancelled-reports");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GenerateReportAsync(output, cancellation.Token));

        if (Directory.Exists(output))
        {
            Assert.Empty(Directory.EnumerateFiles(output));
        }
    }
}




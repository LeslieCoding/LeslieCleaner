using System.IO.Compression;
using System.Text.Json;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Infrastructure.Monitoring;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Diagnostics;

public sealed class LoggingMonitoringTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void SamplingModes_UseMandatedIntervals()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), PerformanceMonitorOptions.IntervalFor(PerformanceSamplingMode.Active));
        Assert.Equal(TimeSpan.FromSeconds(5), PerformanceMonitorOptions.IntervalFor(PerformanceSamplingMode.Normal));
        Assert.Equal(TimeSpan.FromSeconds(30), PerformanceMonitorOptions.IntervalFor(PerformanceSamplingMode.Idle));
    }

    [Fact]
    public async Task Capture_BoundsHistoryAndDebouncesThresholdAlerts()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var collector = new SequenceCollector(
            Snapshot(now, cpu: 95, privateBytes: 900),
            Snapshot(now.AddSeconds(1), cpu: 96, privateBytes: 901),
            Snapshot(now.AddSeconds(2), cpu: 97, privateBytes: 902),
            Snapshot(now.AddSeconds(3), cpu: 98, privateBytes: 903));
        var options = new PerformanceMonitorOptions
        {
            HistoryCapacity = 3,
            CpuWarningPercent = 90,
            PrivateMemoryWarningBytes = 800,
            ConsecutiveSamplesBeforeAlert = 2,
            AlertDebounce = TimeSpan.FromMinutes(1),
        };
        await using var monitor = new Infrastructure.Monitoring.PerformanceMonitor(collector, _workspace.Log, options);
        var alerts = new List<PerformanceAlert>();
        monitor.AlertRaised += (_, alert) => alerts.Add(alert);

        for (int i = 0; i < 4; i++) await monitor.CaptureAsync();

        Assert.Equal(3, monitor.GetHistory().Count);
        Assert.Equal(now.AddSeconds(1), monitor.GetHistory()[0].TimestampUtc);
        Assert.Equal(2, alerts.Count);
        Assert.Single(alerts, x => x.Rule == "CpuHigh");
        Assert.Single(alerts, x => x.Rule == "PrivateMemoryHigh");
        Assert.True(_workspace.Log.HasEvent(LogEventKind.PerformanceMetrics));
    }

    [Fact]
    public async Task BackgroundLoop_StopAndDispose_EndsPromptlyWithoutMoreSamples()
    {
        var collector = new CountingCollector();
        var monitor = new Infrastructure.Monitoring.PerformanceMonitor(collector, _workspace.Log);
        await monitor.StartAsync(PerformanceSamplingMode.Active);
        await collector.FirstCapture.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        int countAfterStop = collector.Count;
        await Task.Delay(100);

        Assert.False(monitor.IsRunning);
        Assert.Equal(countAfterStop, collector.Count);
        await monitor.DisposeAsync();
    }

    [Fact]
    public async Task Collector_ReportsRealMemoryAndExplicitlyUnavailableNetwork()
    {
        var collector = new ProcessPerformanceMetricCollector();

        PerformanceSnapshot first = await collector.CaptureAsync();
        await Task.Delay(25);
        PerformanceSnapshot second = await collector.CaptureAsync();

        Assert.True(first.WorkingSetBytes > 0);
        Assert.True(first.PrivateBytes > 0);
        Assert.True(second.CpuPercent.IsAvailable);
        Assert.False(second.NetworkReceiveBytesPerSecond.IsAvailable);
        Assert.Null(second.NetworkReceiveBytesPerSecond.Value);
        Assert.Contains("ETW", second.NetworkReceiveBytesPerSecond.UnavailableReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackgroundCollectorException_IsLoggedAndLoopTerminates()
    {
        await using var monitor = new Infrastructure.Monitoring.PerformanceMonitor(new ThrowingCollector(), _workspace.Log);

        await monitor.StartAsync(PerformanceSamplingMode.Active);
        await Task.Delay(100);

        Assert.False(monitor.IsRunning);
        Assert.Contains(_workspace.Log.Exceptions, x => x is InvalidOperationException);
    }

    [Fact]
    public void CrashLoopProtector_RedactsPrivateDataAndBlocksThirdRestart()
    {
        var protector = new CrashLoopProtector(_workspace.PathService, maximumCrashes: 3, window: TimeSpan.FromMinutes(5));
        var exception = new InvalidOperationException("password=hunter2 C:\\Users\\Alice\\private.txt alice@example.com");

        string first = protector.RecordCrash(exception, "Scanner C:\\Users\\Alice", "4.0.0");
        protector.RecordCrash(exception, "Scanner", "4.0.0");
        protector.RecordCrash(exception, "Scanner", "4.0.0");
        string content = File.ReadAllText(first);

        Assert.False(protector.IsRestartAllowed());
        Assert.DoesNotContain("hunter2", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED", content, StringComparison.Ordinal);

        protector.MarkStable();
        Assert.True(protector.IsRestartAllowed());
    }

    [Fact]
    public async Task LogArchive_CompressesAndVerifiesBeforeSourceRemoval()
    {
        string directory = Path.Combine(_workspace.RootDirectory, "logs-to-archive");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "completed.log");
        await File.WriteAllTextAsync(source, new string('x', 32_000));
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddDays(-2));

        int count = await new LogArchiveMaintenance().CompressAsync(directory, TimeSpan.FromDays(1));

        Assert.Equal(1, count);
        Assert.False(File.Exists(source));
        string archivePath = source + ".zip";
        Assert.True(File.Exists(archivePath));
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.Equal(32_000, archive.GetEntry("completed.log")!.Length);
    }

    [Fact]
    public async Task LogArchive_PreCanceled_DoesNotModifyFiles()
    {
        string directory = Path.Combine(_workspace.RootDirectory, "cancelled-archive");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "completed.log");
        await File.WriteAllTextAsync(source, "content");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddDays(-2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LogArchiveMaintenance().CompressAsync(directory, TimeSpan.FromDays(1), cancellation.Token));

        Assert.True(File.Exists(source));
        Assert.False(File.Exists(source + ".zip"));
    }

    [Fact]
    public async Task DiagnosticsArchive_IncludesStructuredPerformanceCrashAndTextFormats()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_workspace.PathService.LogDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_workspace.PathService.LogDirectory, "audit-test.jsonl"),
            "{\"Level\":\"Warning\",\"RenderedMessage\":\"verified delete\"}" + Environment.NewLine);
        await using var monitor = new LeslieCleaner.Infrastructure.Monitoring.PerformanceMonitor(
            new SequenceCollector(Snapshot(now, 12, 1024)), _workspace.Log);
        await monitor.CaptureAsync();
        var crashProtector = new CrashLoopProtector(_workspace.PathService);
        crashProtector.RecordCrash(new InvalidOperationException("simulated"), "DiagnosticsTest", "4.0.0");
        var config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        var diagnostics = new DiagnosticsService(
            _workspace.PathService, config, _workspace.Log, monitor, crashProtector);

        string path = await diagnostics.GenerateReportAsync(Path.Combine(_workspace.RootDirectory, "structured-report"));

        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry("performance-history.json"));
        Assert.NotNull(archive.GetEntry("crash-records.json"));
        Assert.NotNull(archive.GetEntry("diagnostics-summary.txt"));
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("logs/log-", StringComparison.Ordinal) &&
                                                  entry.FullName.EndsWith(".jsonl", StringComparison.Ordinal));
        using var performanceReader = new StreamReader(archive.GetEntry("performance-history.json")!.Open());
        using JsonDocument performance = JsonDocument.Parse(await performanceReader.ReadToEndAsync());
        Assert.Single(performance.RootElement.EnumerateArray());
        using var crashReader = new StreamReader(archive.GetEntry("crash-records.json")!.Open());
        using JsonDocument crashes = JsonDocument.Parse(await crashReader.ReadToEndAsync());
        Assert.Single(crashes.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Capture_PreCanceled_DoesNotRetainSnapshot()
    {
        await using var monitor = new LeslieCleaner.Infrastructure.Monitoring.PerformanceMonitor(
            new CountingCollector(), _workspace.Log);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await monitor.CaptureAsync(cancellation.Token));

        Assert.Empty(monitor.GetHistory());
    }

    [Fact]
    public async Task CaptureOverhead_TwoThousandSamples_RemainsBoundedAndFast()
    {
        var collector = new RepeatingCollector(Snapshot(DateTimeOffset.UtcNow, 1, 1024));
        var options = new PerformanceMonitorOptions { HistoryCapacity = 32 };
        await using var monitor = new LeslieCleaner.Infrastructure.Monitoring.PerformanceMonitor(collector, _workspace.Log, options);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();

        for (int i = 0; i < 2_000; i++) await monitor.CaptureAsync();

        TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        Assert.Equal(32, monitor.GetHistory().Count);
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"2,000 samples took {elapsed.TotalMilliseconds:F1} ms");
    }
    private static PerformanceSnapshot Snapshot(DateTimeOffset timestamp, double cpu, long privateBytes) => new(
        timestamp,
        Environment.ProcessId,
        PerformanceMetric.Available(cpu),
        privateBytes / 2,
        privateBytes,
        PerformanceMetric.Available(0),
        PerformanceMetric.Available(0),
        PerformanceMetric.Unavailable("not collected"),
        PerformanceMetric.Unavailable("not collected"));

    private sealed class SequenceCollector(params PerformanceSnapshot[] snapshots) : IPerformanceMetricCollector
    {
        private int _index;

        public ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshots[_index++]);
        }
    }

    private sealed class CountingCollector : IPerformanceMetricCollector
    {
        private int _count;
        public TaskCompletionSource FirstCapture { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count => Volatile.Read(ref _count);

        public ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _count);
            FirstCapture.TrySetResult();
            return ValueTask.FromResult(Snapshot(DateTimeOffset.UtcNow, 0, 1));
        }
    }

    private sealed class RepeatingCollector(PerformanceSnapshot snapshot) : IPerformanceMetricCollector
    {
        public ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshot with { TimestampUtc = DateTimeOffset.UtcNow });
        }
    }
    private sealed class ThrowingCollector : IPerformanceMetricCollector
    {
        public ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated collector failure");
    }
}

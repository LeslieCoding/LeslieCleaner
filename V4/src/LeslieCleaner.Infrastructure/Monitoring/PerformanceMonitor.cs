using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Monitoring;

/// <summary>Bounded, cancellable process-performance monitor with threshold debounce.</summary>
public sealed class PerformanceMonitor : IPerformanceMonitor
{
    private readonly IPerformanceMetricCollector _collector;
    private readonly ILogService _log;
    private readonly PerformanceMonitorOptions _options;
    private readonly object _historySync = new();
    private readonly Queue<PerformanceSnapshot> _history;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _lastAlerts = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private int _mode = (int)PerformanceSamplingMode.Normal;
    private int _cpuExceeded;
    private int _memoryExceeded;
    private bool _disposed;

    /// <summary>Creates a monitor.</summary>
    public PerformanceMonitor(
        IPerformanceMetricCollector collector,
        ILogService log,
        PerformanceMonitorOptions? options = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? new PerformanceMonitorOptions();
        _options.Validate();
        _history = new Queue<PerformanceSnapshot>(_options.HistoryCapacity);
    }

    /// <inheritdoc />
    public event EventHandler<PerformanceAlert>? AlertRaised;

    /// <inheritdoc />
    public bool IsRunning => Volatile.Read(ref _loopTask) is { IsCompleted: false };

    /// <inheritdoc />
    public PerformanceSamplingMode Mode => (PerformanceSamplingMode)Volatile.Read(ref _mode);

    /// <inheritdoc />
    public async Task StartAsync(PerformanceSamplingMode mode = PerformanceSamplingMode.Normal, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        SetMode(mode);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loopTask is { IsCompleted: false }) return;
            _loopCancellation?.Dispose();
            _loopCancellation = new CancellationTokenSource();
            _loopTask = RunAsync(_loopCancellation.Token);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    public void SetMode(PerformanceSamplingMode mode)
    {
        ThrowIfDisposed();
        _ = PerformanceMonitorOptions.IntervalFor(mode);
        Volatile.Write(ref _mode, (int)mode);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Task? loop = _loopTask;
            if (loop is null) return;
            _loopCancellation?.Cancel();
            await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            _loopTask = null;
            _loopCancellation?.Dispose();
            _loopCancellation = null;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        PerformanceSnapshot snapshot = await _collector.CaptureAsync(cancellationToken).ConfigureAwait(false);
        lock (_historySync)
        {
            while (_history.Count >= _options.HistoryCapacity) _history.Dequeue();
            _history.Enqueue(snapshot);
        }

        EvaluateAlerts(snapshot);
        _log.LogEvent(LogEventKind.PerformanceMetrics, "Process performance sample", new Dictionary<string, object?>
        {
            ["CpuPercent"] = snapshot.CpuPercent.Value,
            ["WorkingSetBytes"] = snapshot.WorkingSetBytes,
            ["PrivateBytes"] = snapshot.PrivateBytes,
            ["DiskReadBytesPerSecond"] = snapshot.DiskReadBytesPerSecond.Value,
            ["DiskWriteBytesPerSecond"] = snapshot.DiskWriteBytesPerSecond.Value,
            ["NetworkAvailable"] = snapshot.NetworkReceiveBytesPerSecond.IsAvailable,
        });
        return snapshot;
    }

    /// <inheritdoc />
    public IReadOnlyList<PerformanceSnapshot> GetHistory()
    {
        lock (_historySync) return _history.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _lifecycle.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await CaptureAsync(token).ConfigureAwait(false);
                await Task.Delay(PerformanceMonitorOptions.IntervalFor(Mode), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.LogException(ex, "PerformanceMonitor.RunAsync");
        }
    }

    private void EvaluateAlerts(PerformanceSnapshot snapshot)
    {
        _cpuExceeded = snapshot.CpuPercent is { IsAvailable: true, Value: { } cpu } && cpu > _options.CpuWarningPercent
            ? _cpuExceeded + 1
            : 0;
        _memoryExceeded = snapshot.PrivateBytes > _options.PrivateMemoryWarningBytes
            ? _memoryExceeded + 1
            : 0;

        if (_cpuExceeded >= _options.ConsecutiveSamplesBeforeAlert && snapshot.CpuPercent.Value is { } cpuValue)
            RaiseDebounced("CpuHigh", PerformanceAlertLevel.Critical, cpuValue, _options.CpuWarningPercent, "%", snapshot.TimestampUtc);
        if (_memoryExceeded >= _options.ConsecutiveSamplesBeforeAlert)
            RaiseDebounced("PrivateMemoryHigh", PerformanceAlertLevel.Warning, snapshot.PrivateBytes, _options.PrivateMemoryWarningBytes, "bytes", snapshot.TimestampUtc);
    }

    private void RaiseDebounced(string rule, PerformanceAlertLevel level, double observed, double threshold, string unit, DateTimeOffset timestamp)
    {
        lock (_historySync)
        {
            if (_lastAlerts.TryGetValue(rule, out DateTimeOffset last) && timestamp - last < _options.AlertDebounce) return;
            _lastAlerts[rule] = timestamp;
        }

        var alert = new PerformanceAlert(timestamp, rule, level, observed, threshold, unit);
        _log.LogEvent(LogEventKind.PerformanceMetrics, "Performance threshold exceeded", new Dictionary<string, object?>
        {
            ["Rule"] = rule,
            ["Level"] = level.ToString(),
            ["Observed"] = observed,
            ["Threshold"] = threshold,
            ["Unit"] = unit,
        });

        try { AlertRaised?.Invoke(this, alert); }
        catch (Exception ex) { _log.LogException(ex, "PerformanceMonitor.AlertSubscriber"); }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

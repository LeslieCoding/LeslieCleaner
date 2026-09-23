using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Collects bounded process-resource history and emits debounced alerts.</summary>
public interface IPerformanceMonitor : IAsyncDisposable
{
    /// <summary>Raised after a threshold has remained exceeded for the configured duration.</summary>
    event EventHandler<PerformanceAlert>? AlertRaised;

    /// <summary>Whether the background sampling loop is active.</summary>
    bool IsRunning { get; }

    /// <summary>Current sampling mode.</summary>
    PerformanceSamplingMode Mode { get; }

    /// <summary>Starts background sampling. Repeated calls are idempotent.</summary>
    Task StartAsync(PerformanceSamplingMode mode = PerformanceSamplingMode.Normal, CancellationToken cancellationToken = default);

    /// <summary>Changes cadence without restarting the monitor.</summary>
    void SetMode(PerformanceSamplingMode mode);

    /// <summary>Stops background sampling and waits for the loop to exit.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Captures one sample and stores it in the bounded history.</summary>
    ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns an immutable oldest-to-newest snapshot of retained history.</summary>
    IReadOnlyList<PerformanceSnapshot> GetHistory();
}

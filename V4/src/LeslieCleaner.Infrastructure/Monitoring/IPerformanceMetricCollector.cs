using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Monitoring;

/// <summary>Low-level process metric collector abstraction used for deterministic testing.</summary>
public interface IPerformanceMetricCollector
{
    /// <summary>Captures a single process metric snapshot.</summary>
    ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

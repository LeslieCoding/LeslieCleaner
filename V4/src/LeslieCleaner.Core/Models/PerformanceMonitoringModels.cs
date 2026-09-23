namespace LeslieCleaner.Core.Models;

/// <summary>Sampling cadence for process performance monitoring.</summary>
public enum PerformanceSamplingMode
{
    /// <summary>One-second cadence while an operation is active.</summary>
    Active,

    /// <summary>Five-second normal cadence.</summary>
    Normal,

    /// <summary>Thirty-second cadence while idle.</summary>
    Idle,
}

/// <summary>A metric value that explicitly describes collection availability.</summary>
/// <param name="Value">Measured value, or <see langword="null"/> when unavailable.</param>
/// <param name="IsAvailable">Whether the platform supplied a reliable value.</param>
/// <param name="UnavailableReason">Reason collection was unavailable.</param>
public sealed record PerformanceMetric(double? Value, bool IsAvailable, string? UnavailableReason = null)
{
    /// <summary>Creates an available value.</summary>
    public static PerformanceMetric Available(double value) => new(value, true);

    /// <summary>Creates an explicitly unavailable value.</summary>
    public static PerformanceMetric Unavailable(string reason) => new(null, false, reason);
}

/// <summary>One bounded process-resource snapshot.</summary>
public sealed record PerformanceSnapshot(
    DateTimeOffset TimestampUtc,
    int ProcessId,
    PerformanceMetric CpuPercent,
    long WorkingSetBytes,
    long PrivateBytes,
    PerformanceMetric DiskReadBytesPerSecond,
    PerformanceMetric DiskWriteBytesPerSecond,
    PerformanceMetric NetworkReceiveBytesPerSecond,
    PerformanceMetric NetworkSendBytesPerSecond);

/// <summary>Performance alert severity.</summary>
public enum PerformanceAlertLevel
{
    /// <summary>Small deviation.</summary>
    Notice,

    /// <summary>Potential performance issue.</summary>
    Warning,

    /// <summary>Application stability risk.</summary>
    Critical,
}

/// <summary>A threshold alert emitted by the performance monitor.</summary>
public sealed record PerformanceAlert(
    DateTimeOffset TimestampUtc,
    string Rule,
    PerformanceAlertLevel Level,
    double ObservedValue,
    double Threshold,
    string Unit);

/// <summary>Validated performance-monitor configuration.</summary>
public sealed record PerformanceMonitorOptions
{
    /// <summary>Maximum number of snapshots retained in memory.</summary>
    public int HistoryCapacity { get; init; } = 360;

    /// <summary>CPU threshold in percent.</summary>
    public double CpuWarningPercent { get; init; } = 90;

    /// <summary>Private-memory threshold in bytes.</summary>
    public long PrivateMemoryWarningBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Consecutive samples required before an alert.</summary>
    public int ConsecutiveSamplesBeforeAlert { get; init; } = 3;

    /// <summary>Minimum time between alerts for the same rule.</summary>
    public TimeSpan AlertDebounce { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Returns the mandated cadence for a sampling mode.</summary>
    public static TimeSpan IntervalFor(PerformanceSamplingMode mode) => mode switch
    {
        PerformanceSamplingMode.Active => TimeSpan.FromSeconds(1),
        PerformanceSamplingMode.Normal => TimeSpan.FromSeconds(5),
        PerformanceSamplingMode.Idle => TimeSpan.FromSeconds(30),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>Validates configuration values.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HistoryCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CpuWarningPercent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PrivateMemoryWarningBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ConsecutiveSamplesBeforeAlert);
        if (AlertDebounce < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(AlertDebounce));
    }
}

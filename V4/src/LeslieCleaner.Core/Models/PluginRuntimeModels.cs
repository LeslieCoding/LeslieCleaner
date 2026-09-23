namespace LeslieCleaner.Core.Models;

/// <summary>One operation submitted to an isolated plugin host.</summary>
public sealed record PluginRuntimeRequest(string Operation, string? PayloadJson = null, TimeSpan? Timeout = null);

/// <summary>Outcome of an isolated lifecycle or execution command.</summary>
public sealed record PluginRuntimeResult(
    bool Success,
    string PluginId,
    string? PayloadJson = null,
    string? ErrorCode = null,
    string? Message = null,
    PluginRuntimeTelemetry? Telemetry = null);

/// <summary>Process-level resource data captured by the main process.</summary>
public sealed record PluginRuntimeTelemetry(
    int ProcessId,
    bool IsRunning,
    long WorkingSetBytes,
    TimeSpan TotalProcessorTime,
    int ThreadCount,
    TimeSpan LastOperationDuration,
    int CrashCount,
    DateTimeOffset SampledAtUtc,
    string? LastTerminationReason = null);

/// <summary>Hard runtime budgets enforced by terminating the isolated process.</summary>
public sealed record PluginRuntimeLimits(
    TimeSpan StartupTimeout,
    TimeSpan DefaultExecutionTimeout,
    TimeSpan StopTimeout,
    long MaximumWorkingSetBytes,
    TimeSpan MaximumProcessorTime,
    int MaximumThreadCount,
    TimeSpan MonitorInterval)
{
    /// <summary>Conservative production defaults.</summary>
    public static PluginRuntimeLimits Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(3),
        256L * 1024L * 1024L,
        TimeSpan.FromMinutes(2),
        64,
        TimeSpan.FromMilliseconds(100));
}

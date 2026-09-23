using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Monitoring;

/// <summary>Collects reliable metrics for the current process without retaining process handles.</summary>
public sealed class ProcessPerformanceMetricCollector : IPerformanceMetricCollector
{
    private readonly object _sync = new();
    private DateTimeOffset? _previousTimestamp;
    private TimeSpan _previousCpu;
    private ulong? _previousReadBytes;
    private ulong? _previousWriteBytes;

    /// <inheritdoc />
    public ValueTask<PerformanceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        TimeSpan totalCpu = process.TotalProcessorTime;
        long workingSet = process.WorkingSet64;
        long privateBytes = process.PrivateMemorySize64;
        PerformanceMetric cpu;
        PerformanceMetric diskRead;
        PerformanceMetric diskWrite;

        lock (_sync)
        {
            double elapsedSeconds = _previousTimestamp is { } previous
                ? (timestamp - previous).TotalSeconds
                : 0;
            cpu = elapsedSeconds > 0
                ? PerformanceMetric.Available(Math.Clamp(
                    (totalCpu - _previousCpu).TotalSeconds * 100 / (elapsedSeconds * Environment.ProcessorCount), 0, 100))
                : PerformanceMetric.Unavailable("A second sample is required to calculate CPU usage.");

            if (TryGetIoCounters(process.Handle, out IoCounters counters))
            {
                diskRead = elapsedSeconds > 0 && _previousReadBytes is { } priorRead
                    ? PerformanceMetric.Available((counters.ReadTransferCount - priorRead) / elapsedSeconds)
                    : PerformanceMetric.Unavailable("A second sample is required to calculate disk throughput.");
                diskWrite = elapsedSeconds > 0 && _previousWriteBytes is { } priorWrite
                    ? PerformanceMetric.Available((counters.WriteTransferCount - priorWrite) / elapsedSeconds)
                    : PerformanceMetric.Unavailable("A second sample is required to calculate disk throughput.");
                _previousReadBytes = counters.ReadTransferCount;
                _previousWriteBytes = counters.WriteTransferCount;
            }
            else
            {
                string reason = OperatingSystem.IsWindows()
                    ? "Process disk I/O counters are unavailable."
                    : "Process disk I/O counters require Windows.";
                diskRead = PerformanceMetric.Unavailable(reason);
                diskWrite = PerformanceMetric.Unavailable(reason);
            }

            _previousTimestamp = timestamp;
            _previousCpu = totalCpu;
        }

        const string networkReason = "Reliable per-process network counters require ETW and are not collected by this lightweight monitor.";
        return ValueTask.FromResult(new PerformanceSnapshot(
            timestamp,
            process.Id,
            cpu,
            workingSet,
            privateBytes,
            diskRead,
            diskWrite,
            PerformanceMetric.Unavailable(networkReason),
            PerformanceMetric.Unavailable(networkReason)));
    }

    private static bool TryGetIoCounters(IntPtr processHandle, out IoCounters counters)
    {
        counters = default;
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            return GetProcessIoCounters(processHandle, out counters);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters ioCounters);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }
}

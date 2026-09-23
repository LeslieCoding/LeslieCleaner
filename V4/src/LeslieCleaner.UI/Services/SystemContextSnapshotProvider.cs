using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LeslieCleaner.Modules.AI;

namespace LeslieCleaner.UI.Services;

/// <summary>只读取匿名系统汇总的 AI 上下文快照；不枚举文件、账号或浏览器内容。</summary>
public sealed class SystemContextSnapshotProvider
{
    /// <summary>异步采样 CPU、物理内存和系统盘空间。</summary>
    public async Task<AiAggregateContext> CaptureAsync(CancellationToken cancellationToken = default)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuStart = process.TotalProcessorTime;
        long tickStart = Stopwatch.GetTimestamp();
        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        process.Refresh();
        double wallSeconds = Stopwatch.GetElapsedTime(tickStart).TotalSeconds;
        double cpu = wallSeconds <= 0 ? 0 : (process.TotalProcessorTime - cpuStart).TotalSeconds /
            (wallSeconds * Environment.ProcessorCount) * 100;

        double memory = TryGetPhysicalMemoryLoad(out uint load) ? load : 0;
        double diskFree = GetSystemDriveFreePercent();
        return new AiAggregateContext(
            "Windows", cpu, memory, diskFree,
            TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64)),
            0, 0, 0, 0, 0, 0).Normalize();
    }

    internal static double GetFreePercent(long freeBytes, long totalBytes) =>
        totalBytes <= 0 ? 0 : Math.Clamp(freeBytes * 100d / totalBytes, 0, 100);

    private static double GetSystemDriveFreePercent()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            return drive.IsReady ? GetFreePercent(drive.AvailableFreeSpace, drive.TotalSize) : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0;
        }
    }

    private static bool TryGetPhysicalMemoryLoad(out uint load)
    {
        load = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return false;
        }

        load = status.MemoryLoad;
        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
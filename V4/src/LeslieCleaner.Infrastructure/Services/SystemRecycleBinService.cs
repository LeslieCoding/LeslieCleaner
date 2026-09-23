using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 基于 Windows Shell API 与 NTFS 分配信息的回收站服务。Shell 提供权威项目数和
/// 载荷逻辑字节；文件系统测量补充 <c>$I</c> 元数据及实际磁盘占用，避免把 2 KB
/// 逻辑文件错误显示成与系统“占用 8 KB”相冲突的数据。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SystemRecycleBinService : ISystemRecycleBinService
{
    private const uint NoConfirmation = 0x00000001;
    private const uint NoProgressUi = 0x00000002;
    private const uint NoSound = 0x00000004;
    private const uint InvalidFileSize = 0xFFFFFFFF;

    private readonly ILogService _logService;

    /// <summary>使用日志服务创建回收站 Shell 适配器。</summary>
    public SystemRecycleBinService(ILogService logService) => _logService = logService;

    /// <inheritdoc />
    public Task<RecycleBinSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => QuerySnapshot(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<RecycleBinEmptyResult> EmptyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecycleBinSnapshot before = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (before.ItemCount == 0 && before.SizeBytes == 0 && before.ReclaimableSizeBytes == 0)
        {
            return new RecycleBinEmptyResult(before, before);
        }

        int hresult = SHEmptyRecycleBinW(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
        if (hresult < 0)
        {
            throw new Win32Exception(hresult, $"Windows 回收站清空失败 (HRESULT 0x{hresult:X8})。");
        }

        RecycleBinSnapshot after = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < 2 && !IsVerifiedEmpty(after); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
            after = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = new RecycleBinEmptyResult(before, after);
        if (!IsVerifiedEmpty(after))
        {
            throw new IOException(
                $"Windows 回收站清空后校验失败：仍有 {after.ItemCount:N0} 项，" +
                $"逻辑 {after.SizeBytes:N0} 字节，磁盘占用 {after.AllocatedSizeBytes:N0} 字节，" +
                $"不可访问位置 {after.InaccessibleLocationCount:N0} 个。");
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "Windows 回收站已清空并双口径校验", new Dictionary<string, object?>
        {
            ["BeforeItemCount"] = before.ItemCount,
            ["BeforeLogicalBytes"] = before.SizeBytes,
            ["BeforeAllocatedBytes"] = before.AllocatedSizeBytes,
            ["BeforeMetadataBytes"] = before.MetadataSizeBytes,
            ["MeasurementSource"] = before.Source.ToString(),
        });
        return result;
    }

    private static bool IsVerifiedEmpty(RecycleBinSnapshot snapshot)
    {
        if (snapshot.ItemCount != 0 || snapshot.SizeBytes != 0)
        {
            return false;
        }

        return snapshot.Source == RecycleBinMeasurementSource.WindowsShell ||
               snapshot is { HasCompleteAllocationMeasurement: true, AllocatedSizeBytes: 0 };
    }

    private static RecycleBinSnapshot QuerySnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ShQueryRbInfo { cbSize = Marshal.SizeOf<ShQueryRbInfo>() };
        int hresult = SHQueryRecycleBinW(null, ref info);
        if (hresult < 0)
        {
            throw new Win32Exception(hresult, $"Windows 回收站查询失败 (HRESULT 0x{hresult:X8})。");
        }

        AllocationMeasurement allocation = MeasureCurrentUserRecycleBin(cancellationToken);
        return new RecycleBinSnapshot(
            info.i64NumItems,
            info.i64Size,
            allocation.AllocatedBytes,
            allocation.MetadataLogicalBytes,
            allocation.PayloadLogicalBytes,
            allocation.InaccessibleLocationCount,
            allocation.WasMeasured
                ? RecycleBinMeasurementSource.WindowsShellAndFileSystem
                : RecycleBinMeasurementSource.WindowsShell);
    }

    private static AllocationMeasurement MeasureCurrentUserRecycleBin(CancellationToken cancellationToken)
    {
        string? sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            return default;
        }

        long allocatedBytes = 0;
        long metadataLogicalBytes = 0;
        long payloadLogicalBytes = 0;
        int inaccessible = 0;
        bool measured = false;

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (drive.DriveType != DriveType.Fixed)
            {
                continue;
            }

            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                measured = true;
                string userRecycleRoot = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin", sid);
                if (!Directory.Exists(userRecycleRoot))
                {
                    continue;
                }

                var pending = new Stack<string>();
                pending.Push(userRecycleRoot);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string directory = pending.Pop();
                    string[] entries;
                    try
                    {
                        entries = Directory.GetFileSystemEntries(directory);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        inaccessible++;
                        continue;
                    }

                    foreach (string entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(entry);
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                inaccessible++;
                                continue;
                            }

                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                pending.Push(entry);
                                continue;
                            }

                            var file = new FileInfo(entry);
                            long logicalBytes = file.Length;
                            if (file.Name.StartsWith("$I", StringComparison.OrdinalIgnoreCase))
                            {
                                metadataLogicalBytes = checked(metadataLogicalBytes + logicalBytes);
                            }
                            else
                            {
                                payloadLogicalBytes = checked(payloadLogicalBytes + logicalBytes);
                            }

                            if (TryGetAllocatedSize(entry, out long fileAllocatedBytes))
                            {
                                allocatedBytes = checked(allocatedBytes + fileAllocatedBytes);
                            }
                            else
                            {
                                // 保留可解释的下界，不把测量失败伪装成 0。
                                allocatedBytes = checked(allocatedBytes + logicalBytes);
                                inaccessible++;
                            }
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or OverflowException)
                        {
                            inaccessible++;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                inaccessible++;
            }
        }

        return new AllocationMeasurement(
            allocatedBytes,
            metadataLogicalBytes,
            payloadLogicalBytes,
            inaccessible,
            measured);
    }

    private static bool TryGetAllocatedSize(string path, out long allocatedBytes)
    {
        Marshal.SetLastPInvokeError(0);
        uint low = GetCompressedFileSizeW(path, out uint high);
        int error = Marshal.GetLastPInvokeError();
        if (low == InvalidFileSize && error != 0)
        {
            allocatedBytes = 0;
            return false;
        }

        allocatedBytes = checked((long)(((ulong)high << 32) | low));
        return true;
    }

    private readonly record struct AllocationMeasurement(
        long AllocatedBytes,
        long MetadataLogicalBytes,
        long PayloadLogicalBytes,
        int InaccessibleLocationCount,
        bool WasMeasured);

    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? rootPath, uint flags);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHQueryRecycleBinW(string? rootPath, ref ShQueryRbInfo queryInfo);

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string fileName, out uint fileSizeHigh);
}
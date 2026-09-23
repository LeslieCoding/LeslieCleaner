using System.ComponentModel;
using System.Runtime.InteropServices;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 基于 Windows Shell API 的回收站服务。使用 Shell 维护的逻辑项目统计，
/// 不枚举或删除受保护的 <c>$Recycle.Bin</c> 文件系统条目。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SystemRecycleBinService : ISystemRecycleBinService
{
    private const uint NoConfirmation = 0x00000001;
    private const uint NoProgressUi = 0x00000002;
    private const uint NoSound = 0x00000004;

    private readonly ILogService _logService;

    /// <summary>使用日志服务创建回收站 Shell 适配器。</summary>
    public SystemRecycleBinService(ILogService logService) => _logService = logService;

    /// <inheritdoc />
    public Task<RecycleBinSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(QuerySnapshot());
    }

    /// <inheritdoc />
    public async Task<RecycleBinEmptyResult> EmptyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecycleBinSnapshot before = QuerySnapshot();
        if (before.ItemCount == 0 && before.SizeBytes == 0)
        {
            return new RecycleBinEmptyResult(before, before);
        }

        int hresult = SHEmptyRecycleBinW(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
        if (hresult < 0)
        {
            throw new Win32Exception(hresult, $"Windows 回收站清空失败 (HRESULT 0x{hresult:X8})。");
        }

        RecycleBinSnapshot after = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var result = new RecycleBinEmptyResult(before, after);
        if (!result.IsEmpty)
        {
            throw new IOException($"Windows 回收站清空后校验失败：仍有 {after.ItemCount:N0} 项、{after.SizeBytes:N0} 字节。");
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "Windows 回收站已清空并校验", new Dictionary<string, object?>
        {
            ["BeforeItemCount"] = before.ItemCount,
            ["BeforeSizeBytes"] = before.SizeBytes,
        });
        return result;
    }

    private static RecycleBinSnapshot QuerySnapshot()
    {
        var info = new ShQueryRbInfo { cbSize = Marshal.SizeOf<ShQueryRbInfo>() };
        int hresult = SHQueryRecycleBinW(null, ref info);
        if (hresult < 0)
        {
            throw new Win32Exception(hresult, $"Windows 回收站查询失败 (HRESULT 0x{hresult:X8})。");
        }

        return new RecycleBinSnapshot(info.i64NumItems, info.i64Size);
    }

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
}

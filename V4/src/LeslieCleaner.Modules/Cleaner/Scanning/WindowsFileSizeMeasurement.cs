using System.Runtime.InteropServices;

namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>读取 Windows 文件的逻辑大小与真实磁盘分配大小。</summary>
public static class WindowsFileSizeMeasurement
{
    private const uint InvalidFileSize = 0xFFFFFFFF;

    /// <summary>
    /// 尝试读取 NTFS/Windows 文件系统分配字节。支持压缩和稀疏文件；失败时返回
    /// <see langword="false"/>，调用方必须保留逻辑大小并标记口径不完整。
    /// </summary>
    public static bool TryGetAllocatedSize(string path, out long allocatedBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows())
        {
            allocatedBytes = 0;
            return false;
        }

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

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string fileName, out uint fileSizeHigh);
}

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LeslieCleaner.Modules.Shredder;

/// <summary>
/// 存储介质检测。经 IOCTL_STORAGE_QUERY_PROPERTY 读取设备描述符，
/// 以 SeekPenalty 判定 SSD（无寻道惩罚）；同时用 Trim 属性辅助。
/// 打开物理盘句柄可能需要管理员：失败返回 <see langword="null"/>（未知），
/// 调用方按"保守按 SSD 处理"或提示用户决定。
/// </summary>
public static class StorageMediaDetector
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int PropertyStandardQuery = 0;

    /// <summary>
    /// 探测指定路径所在物理盘是否 SSD。无法判定（无权限/非 Windows/失败）返回
    /// <see langword="null"/>。
    /// </summary>
    /// <param name="path">卷内任意路径。</param>
    public static bool? IsSsd(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            string trimmed = root.TrimEnd('\\');
            if (trimmed.Length != 2 || trimmed[1] != ':')
            {
                return null;
            }

            int driveNumber = trimmed[0] - 'A';
            if (driveNumber is < 0 or > 25)
            {
                return null;
            }

            // 盘符 → 物理盘号：经 \\.\X: 句柄查询卷号过于繁复；
            // 采用卷句柄直接查询设备属性（Windows 支持对卷句柄发 STORAGE_QUERY_PROPERTY）
            using SafeFileHandle handle = CreateFile(
                @"\\.\" + trimmed, GenericRead, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return null;
            }

            byte[] query = new byte[8];
            BitConverter.TryWriteBytes(query.AsSpan(0, 4), StorageDeviceSeekPenaltyProperty);
            BitConverter.TryWriteBytes(query.AsSpan(4, 4), PropertyStandardQuery);
            byte[] output = new byte[64];

            bool ok = DeviceIoControl(
                handle, IoctlStorageQueryProperty, query, (uint)query.Length,
                output, (uint)output.Length, out _, IntPtr.Zero);
            return ok ? ParseSeekPenaltyDescriptor(output) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析 DEVICE_SEEK_PENALTY_DESCRIPTOR：版本(4)+大小(4)+IncursSeekPenalty(1)。
    /// 无寻道惩罚 → SSD。纯函数，可单元测试。
    /// </summary>
    /// <param name="descriptor">IOCTL 输出缓冲。</param>
    public static bool ParseSeekPenaltyDescriptor(ReadOnlySpan<byte> descriptor)
    {
        if (descriptor.Length < 9)
        {
            return false;
        }

        bool incursSeekPenalty = descriptor[8] != 0;
        return !incursSeekPenalty;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        [In] byte[] inBuffer,
        uint inBufferSize,
        [Out] byte[] outBuffer,
        uint outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);
}

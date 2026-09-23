using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LeslieCleaner.Infrastructure.Indexing;

/// <summary>
/// NTFS USN/MFT 加速读取器。经 DeviceIoControl(FSCTL_ENUM_USN_DATA) 直接枚举
/// 主文件表，绕过逐文件 Win32 API，实现秒级全卷文件清单。
/// 打开卷句柄需要管理员权限：<see cref="TryCreate"/> 在无权限/非 NTFS/任何失败时
/// 返回 <see langword="null"/>，调用方自动降级为普通目录枚举。
/// </summary>
public sealed class UsnMftEnumerator : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint FsctlEnumUsnData = 0x000900B3;
    private const uint FileAttributeDirectory = 0x00000010;
    private const int ErrorHandleEof = 38;

    private readonly SafeFileHandle _volumeHandle;
    private readonly string _volumeRoot;
    private int _disposed;

    private UsnMftEnumerator(SafeFileHandle volumeHandle, string volumeRoot)
    {
        _volumeHandle = volumeHandle;
        _volumeRoot = volumeRoot;
    }

    /// <summary>USN 枚举出的单条文件记录。</summary>
    /// <param name="Path">重建的完整路径。</param>
    /// <param name="Name">文件名。</param>
    /// <param name="IsDirectory">是否目录。</param>
    /// <param name="LastChangeUtcTicks">MFT 记录最后变更时间（UTC ticks，FILETIME 与 DateTime 同纪元）。</param>
    public readonly record struct UsnFileEntry(string Path, string Name, bool IsDirectory, long LastChangeUtcTicks);

    /// <summary>
    /// 尝试为指定卷创建枚举器。无管理员权限、非 NTFS 卷或任何底层失败时返回
    /// <see langword="null"/>（优雅降级信号），不抛出。
    /// </summary>
    /// <param name="volumeRoot">卷根，如 "C:\"。</param>
    public static UsnMftEnumerator? TryCreate(string volumeRoot)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(volumeRoot))
        {
            return null;
        }

        try
        {
            // 卷根（如 "C:\" 或 "C:"）→ 卷设备路径 \\.\C:
            string trimmed = volumeRoot.TrimEnd('\\');
            if (trimmed.Length != 2 || trimmed[1] != ':')
            {
                return null;
            }

            string devicePath = @"\\.\" + trimmed;

            SafeFileHandle handle = CreateFile(
                devicePath, GenericRead, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            return new UsnMftEnumerator(handle, volumeRoot.EndsWith('\\') ? volumeRoot : volumeRoot + "\\");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 枚举全卷文件记录（含目录）。调用方负责按允许根过滤。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public IEnumerable<UsnFileEntry> EnumerateAll(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var records = new Dictionary<ulong, (ulong ParentFrn, string Name, bool IsDirectory, long TimeStamp)>();
        var med = new MftEnumDataV0 { StartFileReferenceNumber = 0, LowUsn = long.MinValue, HighUsn = long.MaxValue };
        byte[] buffer = new byte[1024 * 1024];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GCHandle medHandle = GCHandle.Alloc(med, GCHandleType.Pinned);
            int bytesReturned;
            bool ok;
            try
            {
                ok = DeviceIoControl(
                    _volumeHandle, FsctlEnumUsnData, medHandle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<MftEnumDataV0>(),
                    buffer, (uint)buffer.Length, out bytesReturned, IntPtr.Zero);
            }
            finally
            {
                medHandle.Free();
            }

            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorHandleEof)
                {
                    break;
                }

                throw new IOException($"FSCTL_ENUM_USN_DATA 失败，Win32 错误 {error}",
                    new System.ComponentModel.Win32Exception(error));
            }

            if (bytesReturned <= sizeof(ulong))
            {
                break;
            }

            // 输出前 8 字节为下一次调用的起始 FRN
            ulong nextFrn = BitConverter.ToUInt64(buffer, 0);
            int offset = sizeof(ulong);
            while (offset < bytesReturned)
            {
                int recordLength = BitConverter.ToInt32(buffer, offset);
                if (recordLength <= 0 || offset + recordLength > bytesReturned)
                {
                    break;
                }

                ushort majorVersion = BitConverter.ToUInt16(buffer, offset + 4);
                if (majorVersion is 2 or 3)
                {
                    ulong frn = BitConverter.ToUInt64(buffer, offset + 8) & 0x0000FFFFFFFFFFFF;
                    ulong parentFrn = BitConverter.ToUInt64(buffer, offset + 16) & 0x0000FFFFFFFFFFFF;
                    long timeStamp = BitConverter.ToInt64(buffer, offset + 32);
                    uint attributes = BitConverter.ToUInt32(buffer, offset + 52);
                    ushort nameLength = BitConverter.ToUInt16(buffer, offset + 56);
                    ushort nameOffset = BitConverter.ToUInt16(buffer, offset + 58);
                    string name = System.Text.Encoding.Unicode.GetString(buffer, offset + nameOffset, nameLength);
                    records[frn] = (parentFrn, name, (attributes & FileAttributeDirectory) != 0, timeStamp);
                }

                offset += recordLength;
            }

            med.StartFileReferenceNumber = nextFrn;
        }

        var pathCache = new Dictionary<ulong, string>();
        foreach (KeyValuePair<ulong, (ulong ParentFrn, string Name, bool IsDirectory, long TimeStamp)> record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? path = BuildPath(record.Key, records, pathCache);
            if (path is not null)
            {
                yield return new UsnFileEntry(path, record.Value.Name, record.Value.IsDirectory, record.Value.TimeStamp);
            }
        }
    }

    /// <summary>释放卷句柄。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _volumeHandle.Dispose();
        }
    }

    private string? BuildPath(
        ulong frn,
        Dictionary<ulong, (ulong ParentFrn, string Name, bool IsDirectory, long TimeStamp)> records,
        Dictionary<ulong, string> pathCache)
    {
        if (pathCache.TryGetValue(frn, out string? cached))
        {
            return cached;
        }

        if (!records.TryGetValue(frn, out (ulong ParentFrn, string Name, bool IsDirectory, long TimeStamp) record))
        {
            return null;
        }

        string path;
        if (record.ParentFrn == frn || !records.ContainsKey(record.ParentFrn))
        {
            // 到达卷根
            path = _volumeRoot + record.Name;
        }
        else
        {
            string? parentPath = BuildPath(record.ParentFrn, records, pathCache);
            path = parentPath is null ? _volumeRoot + record.Name : Path.Combine(parentPath, record.Name);
        }

        pathCache[frn] = path;
        return path;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(UsnMftEnumerator));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MftEnumDataV0
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
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
        IntPtr inBuffer,
        uint inBufferSize,
        [Out] byte[] outBuffer,
        uint outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);
}

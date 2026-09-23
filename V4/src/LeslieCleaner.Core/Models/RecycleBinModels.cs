namespace LeslieCleaner.Core.Models;

/// <summary>回收站容量测量来源。</summary>
public enum RecycleBinMeasurementSource
{
    /// <summary>仅包含 Windows Shell 的权威逻辑项目统计。</summary>
    WindowsShell,

    /// <summary>Windows Shell 逻辑统计与 NTFS 实际分配空间均已测量。</summary>
    WindowsShellAndFileSystem,
}

/// <summary>
/// Windows 回收站的系统级统计快照。<see cref="SizeBytes"/> 与资源管理器的
/// “大小”口径一致；<see cref="AllocatedSizeBytes"/> 包括载荷和 <c>$I</c>
/// 元数据实际占用，用于显示真正可释放的磁盘空间。
/// </summary>
/// <param name="ItemCount">Shell 报告的逻辑项目数量。</param>
/// <param name="SizeBytes">Shell 报告的载荷逻辑总字节数。</param>
/// <param name="AllocatedSizeBytes">回收站载荷与元数据在磁盘上的实际分配字节数；未测得时为 0。</param>
/// <param name="MetadataSizeBytes"><c>$I</c> 元数据的逻辑字节数。</param>
/// <param name="EnumeratedPayloadSizeBytes">文件系统枚举得到的 <c>$R</c> 载荷逻辑字节数。</param>
/// <param name="InaccessibleLocationCount">枚举时无法访问的位置数；大于 0 表示磁盘占用不是完整统计。</param>
/// <param name="Source">统计来源。</param>
public sealed record RecycleBinSnapshot(
    long ItemCount,
    long SizeBytes,
    long AllocatedSizeBytes = 0,
    long MetadataSizeBytes = 0,
    long EnumeratedPayloadSizeBytes = 0,
    int InaccessibleLocationCount = 0,
    RecycleBinMeasurementSource Source = RecycleBinMeasurementSource.WindowsShell)
{
    /// <summary>用于“预计释放”的磁盘占用；文件系统统计不可用时回退到 Shell 逻辑字节。</summary>
    public long ReclaimableSizeBytes => AllocatedSizeBytes > 0 ? AllocatedSizeBytes : SizeBytes;

    /// <summary>磁盘占用统计是否完整且无不可访问位置。</summary>
    public bool HasCompleteAllocationMeasurement =>
        Source == RecycleBinMeasurementSource.WindowsShellAndFileSystem && InaccessibleLocationCount == 0;

    /// <summary>文件系统枚举载荷与 Shell 权威逻辑字节的差值；非零时应显示口径差异诊断。</summary>
    public long ShellToFileSystemLogicalDifferenceBytes => EnumeratedPayloadSizeBytes - SizeBytes;

    /// <summary>是否检测到 Shell 与文件系统载荷逻辑字节不一致。</summary>
    public bool HasLogicalDiscrepancy =>
        Source == RecycleBinMeasurementSource.WindowsShellAndFileSystem &&
        ShellToFileSystemLogicalDifferenceBytes != 0;
}

/// <summary>清空回收站后的已验证结果。</summary>
/// <param name="Before">清空前的 Shell 快照。</param>
/// <param name="After">清空后的 Shell 快照。</param>
public sealed record RecycleBinEmptyResult(RecycleBinSnapshot Before, RecycleBinSnapshot After)
{
    /// <summary>仅当清空后项目数和字节数均为零时为真。</summary>
    public bool IsEmpty => After is { ItemCount: 0, SizeBytes: 0 } &&
        (After.Source == RecycleBinMeasurementSource.WindowsShell ||
         After is { HasCompleteAllocationMeasurement: true, AllocatedSizeBytes: 0 });
}
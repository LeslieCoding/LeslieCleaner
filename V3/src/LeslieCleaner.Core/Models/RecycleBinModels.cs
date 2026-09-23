namespace LeslieCleaner.Core.Models;

/// <summary>Windows 回收站的系统级统计快照。</summary>
/// <param name="ItemCount">Shell 报告的逻辑项目数量。</param>
/// <param name="SizeBytes">Shell 报告的逻辑项目总字节数。</param>
public sealed record RecycleBinSnapshot(long ItemCount, long SizeBytes);

/// <summary>清空回收站后的已验证结果。</summary>
/// <param name="Before">清空前的 Shell 快照。</param>
/// <param name="After">清空后的 Shell 快照。</param>
public sealed record RecycleBinEmptyResult(RecycleBinSnapshot Before, RecycleBinSnapshot After)
{
    /// <summary>仅当清空后项目数和字节数均为零时为真。</summary>
    public bool IsEmpty => After is { ItemCount: 0, SizeBytes: 0 };
}

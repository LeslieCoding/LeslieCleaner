namespace LeslieCleaner.Core.Models;

/// <summary>还原冲突处理策略（原始位置已存在同名文件时）。</summary>
public enum RestoreConflictPolicy
{
    /// <summary>放弃还原并抛出异常。</summary>
    Fail,

    /// <summary>覆盖现有文件。</summary>
    Overwrite,

    /// <summary>自动重命名还原文件（追加序号）。</summary>
    Rename,
}

/// <summary>清理请求。</summary>
public sealed record CleanRequest
{
    /// <summary>待清理项集合。</summary>
    public required IReadOnlyList<ScannedItem> Items { get; init; }

    /// <summary>操作原因（写入隔离区元数据与日志）。</summary>
    public required string Reason { get; init; }

    /// <summary>操作标识；为空时由引擎生成。</summary>
    public string? OperationId { get; init; }

    /// <summary>是否在清理前创建系统还原点，默认 <see langword="false"/>。</summary>
    public bool CreateRestorePoint { get; init; }

    /// <summary>
    /// 是否明确允许不可回滚地清空 Windows 回收站。默认 <see langword="false"/>，
    /// 防止把回收站误当作普通可隔离缓存处理。
    /// </summary>
    public bool AllowIrreversibleRecycleBin { get; init; }
}

/// <summary>清理进度快照。</summary>
/// <param name="ProcessedItems">已处理条目数。</param>
/// <param name="TotalItems">总条目数。</param>
/// <param name="CurrentPath">当前处理路径，可为 <see langword="null"/>。</param>
public sealed record CleanProgress(int ProcessedItems, int TotalItems, string? CurrentPath);

/// <summary>单个已处理清理项的结果。</summary>
/// <param name="Item">原始扫描项。</param>
/// <param name="QuarantineEntryId">对应隔离条目标识；未隔离（如注册表项）时为 <see langword="null"/>。</param>
/// <param name="BackupId">对应备份记录标识；无备份时为 <see langword="null"/>。</param>
/// <param name="Success">是否成功。</param>
/// <param name="Error">失败原因；成功时为 <see langword="null"/>。</param>
public sealed record CleanedItem(
    ScannedItem Item,
    Guid? QuarantineEntryId,
    Guid? BackupId,
    bool Success,
    string? Error);

/// <summary>清理结果聚合。持有 <see cref="OperationId"/> 可用于整体回滚。</summary>
public sealed record CleanResult
{
    /// <summary>本次清理操作标识（回滚凭据）。</summary>
    public required string OperationId { get; init; }

    /// <summary>逐项结果。</summary>
    public required IReadOnlyList<CleanedItem> Items { get; init; }

    /// <summary>实际释放字节数（成功项合计）。</summary>
    public long FreedBytes { get; init; }

    /// <summary>清理是否被取消。</summary>
    public bool WasCancelled { get; init; }
}

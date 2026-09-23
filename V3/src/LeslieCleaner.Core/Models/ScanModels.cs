namespace LeslieCleaner.Core.Models;

/// <summary>扫描选项。</summary>
public sealed record ScanOptions
{
    /// <summary>扫描目标目录/路径集合。</summary>
    public required IReadOnlyList<string> Targets { get; init; }

    /// <summary>最大并行度；小于等于 0 时取 CPU 核心数 * 2（圣经线程架构）。</summary>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>是否跟随符号链接/重解析点，默认 <see langword="false"/> 以避免循环与越界。</summary>
    public bool FollowReparsePoints { get; init; }

    /// <summary>单次扫描超时；为 <see langword="null"/> 表示不限制。</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>扫描进度快照。</summary>
/// <param name="Phase">当前阶段描述（如 "规则库定位"）。</param>
/// <param name="ProcessedItems">已处理条目数。</param>
/// <param name="TotalItems">预计总条目数；未知时为 <see langword="null"/>。</param>
/// <param name="CurrentPath">当前处理路径，可为 <see langword="null"/>。</param>
public sealed record ScanProgress(string Phase, long ProcessedItems, long? TotalItems, string? CurrentPath)
{
    /// <summary>完成百分比（0-100）；总量未知时为 <see langword="null"/>。</summary>
    public double? PercentComplete =>
        TotalItems is > 0 ? Math.Clamp(ProcessedItems * 100.0 / TotalItems.Value, 0, 100) : null;
}

/// <summary>单个扫描命中项。</summary>
/// <param name="Path">命中路径（文件或注册表键）。</param>
/// <param name="SizeBytes">可释放字节数。</param>
/// <param name="Category">分类（如 "TempFiles"、"BrowserCache"）。</param>
/// <param name="LastModifiedUtc">最后修改时间（UTC）。</param>
/// <param name="ItemCount">此记录代表的逻辑项目数；普通文件固定为 1。</param>
public sealed record ScannedItem(
    string Path,
    long SizeBytes,
    string Category,
    DateTimeOffset LastModifiedUtc,
    long ItemCount = 1);

/// <summary>扫描结果聚合。</summary>
public sealed record ScanResult
{
    /// <summary>扫描开始时间（UTC）。</summary>
    public required DateTimeOffset StartedUtc { get; init; }

    /// <summary>扫描耗时。</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>命中项集合。</summary>
    public required IReadOnlyList<ScannedItem> Items { get; init; }

    /// <summary>命中项总大小（字节）。</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>扫描是否被取消。</summary>
    public bool WasCancelled { get; init; }

    /// <summary>扫描过程中记录的非致命错误数。</summary>
    public int ErrorCount { get; init; }
}

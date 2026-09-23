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
/// <param name="LogicalSizeBytes">权威逻辑字节；仅在与 <paramref name="SizeBytes"/> 的可释放口径不同时设置。</param>
/// <param name="AllocatedSizeBytes">磁盘实际分配字节；未测量时为 <see langword="null"/>。</param>
/// <param name="MetadataSizeBytes">伴随元数据字节，例如回收站 <c>$I</c> 记录。</param>
/// <param name="InaccessibleItemCount">无法访问或无法精确枚举的位置数。</param>
/// <param name="MeasurementSource">统计来源说明。</param>
/// <param name="MeasurementDiscrepancyBytes">权威来源与辅助测量的字节差；非零时必须向用户解释。</param>
/// <param name="RuleId">Versioned rule identifier that produced this finding, when applicable.</param>
/// <param name="Provider">Provider or subsystem that owns the rule, when applicable.</param>
/// <param name="RuleVersion">Version of the rule definition that produced this finding.</param>
/// <param name="AccessState">Observed access state such as Verified, Partial, Inaccessible, or Protected.</param>
/// <param name="RegistryKeyPath">Typed registry key path. When set, cleanup must not parse <paramref name="Path"/> delimiters.</param>
/// <param name="RegistryValueName">Typed registry value name; null means the key itself, while an empty string means the default value.</param>
public sealed record ScannedItem(
    string Path,
    long SizeBytes,
    string Category,
    DateTimeOffset LastModifiedUtc,
    long ItemCount = 1,
    long? LogicalSizeBytes = null,
    long? AllocatedSizeBytes = null,
    long MetadataSizeBytes = 0,
    int InaccessibleItemCount = 0,
    string? MeasurementSource = null,
    long MeasurementDiscrepancyBytes = 0,
    string? RuleId = null,
    string? Provider = null,
    string? RuleVersion = null,
    string? AccessState = null,
    string? RegistryKeyPath = null,
    string? RegistryValueName = null);

/// <summary>扫描结果聚合。</summary>
public sealed record ScanResult
{
    /// <summary>扫描开始时间（UTC）。</summary>
    public required DateTimeOffset StartedUtc { get; init; }

    /// <summary>扫描耗时。</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>命中项集合。</summary>
    public required IReadOnlyList<ScannedItem> Items { get; init; }

    /// <summary>命中项逻辑总大小（字节）。</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// 命中项预计实际释放的磁盘分配字节。无法测量的项以逻辑大小作为保守回退，
    /// 具体项可通过 <see cref="ScannedItem.AllocatedSizeBytes"/> 判断口径是否完整。
    /// </summary>
    public long TotalAllocatedSizeBytes { get; init; }

    /// <summary>扫描是否被取消。</summary>
    public bool WasCancelled { get; init; }

    /// <summary>扫描过程中记录的非致命错误数。</summary>
    public int ErrorCount { get; init; }
}

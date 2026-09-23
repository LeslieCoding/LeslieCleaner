namespace LeslieCleaner.Modules.Cleaner.Analytics;

/// <summary>大文件条目。</summary>
/// <param name="Path">完整路径。</param>
/// <param name="SizeBytes">大小（字节）。</param>
/// <param name="LastWriteUtc">最后写入时间（UTC）。</param>
public sealed record LargeFileEntry(string Path, long SizeBytes, DateTimeOffset LastWriteUtc);

/// <summary>文件类型统计。</summary>
/// <param name="Extension">扩展名（小写，无扩展名为 "(none)"）。</param>
/// <param name="Count">文件数。</param>
/// <param name="TotalBytes">总大小。</param>
public sealed record FileTypeStat(string Extension, long Count, long TotalBytes);

/// <summary>Treemap 数据节点（面积 = SizeBytes；绘制在 UI 阶段）。</summary>
/// <param name="Name">节点名。</param>
/// <param name="Path">完整路径。</param>
/// <param name="SizeBytes">聚合大小（自身子树合计）。</param>
/// <param name="Children">子节点。</param>
public sealed record TreemapNode(string Name, string Path, long SizeBytes, IReadOnlyList<TreemapNode> Children);

/// <summary>分析结果。</summary>
/// <param name="TopFiles">Top N 大文件（按大小降序）。</param>
/// <param name="TypeStats">类型统计（按总大小降序）。</param>
/// <param name="Treemap">Treemap 根节点集合（每个扫描根一棵）。</param>
/// <param name="Suggestions">规则引擎清理建议（引用真实依据）。</param>
/// <param name="ScannedFiles">扫描文件总数。</param>
/// <param name="TotalBytes">扫描总大小。</param>
/// <param name="WasCancelled">是否被取消。</param>
public sealed record LargeFileAnalysisResult(
    IReadOnlyList<LargeFileEntry> TopFiles,
    IReadOnlyList<FileTypeStat> TypeStats,
    IReadOnlyList<TreemapNode> Treemap,
    IReadOnlyList<string> Suggestions,
    long ScannedFiles,
    long TotalBytes,
    bool WasCancelled);

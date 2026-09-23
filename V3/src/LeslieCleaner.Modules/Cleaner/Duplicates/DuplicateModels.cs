namespace LeslieCleaner.Modules.Cleaner.Duplicates;

/// <summary>一组重复文件。</summary>
/// <param name="ContentHash">全量内容哈希（SHA256 hex）。</param>
/// <param name="Files">组内文件路径（至少 2 个）。</param>
/// <param name="SizeEach">单文件大小（字节）。</param>
public sealed record DuplicateGroup(string ContentHash, IReadOnlyList<string> Files, long SizeEach)
{
    /// <summary>可释放字节（保留 1 份后的冗余）。</summary>
    public long WastedBytes => SizeEach * (Files.Count - 1);
}

/// <summary>相似图片组。</summary>
/// <param name="ReferenceImage">基准图片。</param>
/// <param name="SimilarImages">相似图片（含与基准的汉明距离，距离 ≤ 阈值）。</param>
public sealed record SimilarImageGroup(string ReferenceImage, IReadOnlyList<(string Path, int Distance)> SimilarImages);

/// <summary>智能保留策略。</summary>
public enum KeepPolicy
{
    /// <summary>保留最新（LastWriteTime 最大）。</summary>
    Newest,

    /// <summary>保留最旧。</summary>
    Oldest,

    /// <summary>保留最短路径。</summary>
    ShortestPath,

    /// <summary>指定目录优先（目录内文件优先保留）。</summary>
    PreferredDirectory,
}

/// <summary>媒体文件类别（视频/音频去重分层用）。</summary>
public enum MediaKind
{
    /// <summary>非媒体。</summary>
    None,

    /// <summary>视频。</summary>
    Video,

    /// <summary>音频。</summary>
    Audio,
}

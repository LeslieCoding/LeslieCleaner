namespace LeslieCleaner.Core.Models;

/// <summary>file_index 表行模型（Stage 4 Publisher 索引定位的数据载体）。</summary>
/// <param name="Path">文件完整路径。</param>
/// <param name="Name">文件名。</param>
/// <param name="Publisher">发布者（来自版本信息/目录推断），可为 <see langword="null"/>。</param>
/// <param name="ProductName">产品名，可为 <see langword="null"/>。</param>
/// <param name="SizeBytes">文件大小（字节）。</param>
/// <param name="ModifiedUtc">最后修改时间（UTC）。</param>
/// <param name="Hash">内容哈希，可为 <see langword="null"/>（按需计算）。</param>
/// <param name="IndexedAtUtc">索引写入时间（UTC）。</param>
public sealed record FileIndexRecord(
    string Path,
    string Name,
    string? Publisher,
    string? ProductName,
    long SizeBytes,
    DateTimeOffset ModifiedUtc,
    string? Hash,
    DateTimeOffset IndexedAtUtc);

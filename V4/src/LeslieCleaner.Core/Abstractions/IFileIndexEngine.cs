using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 文件索引引擎契约（Stage 4：Publisher 索引定位的基础设施）。
/// 维护 SQLite file_index 表之上的增量索引与秒级查询，实现位于后续阶段。
/// </summary>
public interface IFileIndexEngine
{
    /// <summary>
    /// 对指定目录集合建立/增量更新文件索引。实现方必须使用异步 IO 与并行枚举，
    /// 并遵守受限范围（禁止 Windows/WinSxS/System32/DriverStore 等目录）。
    /// </summary>
    /// <param name="roots">索引根目录集合。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次索引新增/更新的记录数。</returns>
    Task<long> IndexAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按发布者查询索引记录（走 idx_publisher 索引）。
    /// </summary>
    /// <param name="publisher">发布者名称（大小写不敏感）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<FileIndexRecord>> QueryByPublisherAsync(string publisher, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按产品名查询索引记录（走 idx_product 索引）。
    /// </summary>
    /// <param name="productName">产品名（大小写不敏感）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<FileIndexRecord>> QueryByProductAsync(string productName, CancellationToken cancellationToken = default);
    /// <summary>Searches every indexed metadata field using parameterized filters and stable pagination.</summary>
    Task<FileIndexSearchPage> SearchAsync(
        FileIndexSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a stable search in bounded pages rather than materializing the complete result set.</summary>
    IAsyncEnumerable<FileIndexRecord> StreamSearchAsync(
        FileIndexSearchQuery query,
        CancellationToken cancellationToken = default);
}

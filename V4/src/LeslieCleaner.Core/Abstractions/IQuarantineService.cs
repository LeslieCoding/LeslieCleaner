namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 隔离区服务契约。所有文件删除默认进入隔离区
/// （%ProgramData%\LeslieCleaner\Quarantine），默认保留 30 天，支持完整还原。
/// </summary>
public interface IQuarantineService
{
    /// <summary>
    /// 将单个文件移入隔离区，并写入 metadata.json（原始路径、大小、时间、原因、操作标识）。
    /// </summary>
    /// <param name="filePath">待隔离文件的绝对路径，必须存在。</param>
    /// <param name="reason">隔离原因（模块/操作描述），写入元数据用于审计。</param>
    /// <param name="operationId">关联的清理操作标识，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>隔离条目描述。</returns>
    Task<Models.QuarantineEntry> QuarantineFileAsync(
        string filePath,
        string reason,
        string? operationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量将文件移入隔离区。单个文件失败不中断整体，失败信息记录在返回条目的
    /// <see cref="Models.QuarantineEntry.Error"/> 中（该条目不计入成功）。
    /// </summary>
    /// <param name="filePaths">待隔离文件绝对路径集合。</param>
    /// <param name="reason">隔离原因。</param>
    /// <param name="operationId">关联的清理操作标识，可为 <see langword="null"/>。</param>
    /// <param name="progress">进度回调（已完成数/总数），可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功隔离的条目集合。</returns>
    Task<IReadOnlyList<Models.QuarantineEntry>> QuarantineFilesAsync(
        IReadOnlyList<string> filePaths,
        string reason,
        string? operationId = null,
        IProgress<Models.CleanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将隔离条目还原到其原始路径。
    /// </summary>
    /// <param name="entryId">隔离条目标识。</param>
    /// <param name="conflictPolicy">原始路径已存在同名文件时的处理策略，默认 <see cref="Models.RestoreConflictPolicy.Rename"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>还原后的实际文件路径。</returns>
    Task<string> RestoreAsync(
        Guid entryId,
        Models.RestoreConflictPolicy conflictPolicy = Models.RestoreConflictPolicy.Rename,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出隔离区全部条目（按隔离时间倒序）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Models.QuarantineEntry>> ListEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理超过保留期（默认 30 天，可由配置覆盖）的隔离条目，永久删除。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>被永久删除的条目数。</returns>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

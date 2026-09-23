namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 清理引擎契约。删除操作默认进入隔离区，任何清理都必须可回滚。
/// </summary>
public interface ICleanEngine
{
    /// <summary>
    /// 执行清理。文件类项目经 <see cref="IQuarantineService"/> 移入隔离区，
    /// 注册表类项目先经 <see cref="IBackupService"/> 备份；整体构成一次可回滚操作。
    /// </summary>
    /// <param name="request">清理请求，包含待清理项与选项。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理结果，含可用于回滚的操作标识。</returns>
    Task<Models.CleanResult> CleanAsync(
        Models.CleanRequest request,
        IProgress<Models.CleanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚一次清理操作：从隔离区还原文件、从备份恢复注册表。
    /// </summary>
    /// <param name="operationId"><see cref="Models.CleanResult.OperationId"/> 返回的操作标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>全部项目还原成功返回 <see langword="true"/>；存在失败项返回 <see langword="false"/>。</returns>
    Task<bool> RollbackAsync(string operationId, CancellationToken cancellationToken = default);
}

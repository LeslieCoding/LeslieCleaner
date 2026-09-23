namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 扫描引擎契约。所有扫描实现必须支持进度报告、暂停/继续/取消，
/// 禁止阻塞调用线程，禁止以全盘递归扫描作为主要实现方案。
/// </summary>
public interface IScanEngine
{
    /// <summary>获取当前是否处于暂停状态。</summary>
    bool IsPaused { get; }

    /// <summary>
    /// 执行扫描。
    /// </summary>
    /// <param name="options">扫描选项。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>；实现方应节流后按阶段上报。</param>
    /// <param name="cancellationToken">取消令牌，与 <see cref="Cancel"/> 等效。</param>
    /// <returns>扫描结果聚合。</returns>
    Task<Models.ScanResult> ScanAsync(
        Models.ScanOptions options,
        IProgress<Models.ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>暂停扫描。已发出的 IO 请求会完成，随后扫描挂起直到 <see cref="Resume"/>。</summary>
    void Pause();

    /// <summary>从暂停状态继续扫描。</summary>
    void Resume();

    /// <summary>取消扫描，等价于取消传入的 <see cref="CancellationToken"/>。</summary>
    void Cancel();
}

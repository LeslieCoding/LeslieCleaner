namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 卸载残留扫描契约。实现 Multi-Stage Residual Pipeline：
/// 安装数据库定位 → 规则库定位 → 安装日志定位 → Publisher 索引定位 → 受限深度扫描。
/// 目标秒级定位，禁止全盘递归扫描。
/// </summary>
public interface IResidualScanner
{
    /// <summary>
    /// 针对已卸载（或待卸载）的软件定位其残留文件与注册表项。
    /// </summary>
    /// <param name="query">残留查询条件（发布者、产品名、安装位置等）。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命中的残留项集合。</returns>
    Task<IReadOnlyList<Models.ResidualItem>> ScanAsync(
        Models.ResidualQuery query,
        IProgress<Models.ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

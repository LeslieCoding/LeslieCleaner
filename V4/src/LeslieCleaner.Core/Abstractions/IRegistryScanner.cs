namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 注册表扫描契约。按规则集（无效扩展名/COM/CLSID/字体/路径/卸载项）
/// 扫描注册表并产出带风险评级的命中项；只读，不做任何修改。
/// </summary>
public interface IRegistryScanner
{
    /// <summary>
    /// 执行全量规则扫描。性能目标 &lt; 3s。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命中项集合（按风险评级升序：Safe 在前）。</returns>
    Task<IReadOnlyList<Models.RegistryFinding>> ScanAsync(
        IProgress<Models.ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

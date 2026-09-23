namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 浏览器隐私数据清理契约。由 Modules.Browser 实现，
/// 供 Privacy Guard 以接口方式复用（模块间不直接引用）。
/// 登录状态保留约定与浏览器模块一致（Cookies 白名单、不动密码库）。
/// </summary>
public interface IBrowserPrivacyCleaner
{
    /// <summary>
    /// 清理浏览器隐私数据（Cookies（白名单保留）/历史/下载历史/表单数据）。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已清理项数。</returns>
    Task<int> CleanPrivacyDataAsync(
        IProgress<Models.CleanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

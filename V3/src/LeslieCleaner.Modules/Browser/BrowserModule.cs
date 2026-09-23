using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Browser;

/// <summary>Browser Cleaner 模块（圣经第一节功能清单第 4 条）。</summary>
public sealed class BrowserModule : IModule
{
    private readonly BrowserCleanerEngine _engine;

    /// <summary>以清理引擎创建模块。</summary>
    /// <param name="engine">浏览器清理引擎。</param>
    public BrowserModule(BrowserCleanerEngine engine) => _engine = engine;

    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.browser",
        "Browser Cleaner",
        new Version(2, 0, 0),
        "七浏览器缓存/Cookie/历史/表单清理，Cookie 白名单与登录状态保留，自动清理规则");

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BrowserProfile> profiles = await _engine.LocateProfilesAsync(cancellationToken).ConfigureAwait(false);
        return new ModuleHealth(
            Info.Id,
            ModuleStatus.Healthy,
            $"已发现浏览器配置 {profiles.Count} 个",
            DateTimeOffset.UtcNow);
    }
}

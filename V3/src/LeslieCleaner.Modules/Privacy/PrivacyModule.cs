using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Privacy;

/// <summary>Privacy Guard 模块（圣经第一节功能清单第 6 条）。</summary>
public sealed class PrivacyModule : IModule
{
    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.privacy",
        "Privacy Guard",
        new Version(2, 0, 0),
        "最近文件/剪贴板/搜索/运行/Office 历史/浏览器隐私/地理位置/语音缓存清理");

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ModuleHealth(Info.Id, ModuleStatus.Healthy, null, DateTimeOffset.UtcNow));
    }
}

/// <summary>LeslieCleaner.Modules.Privacy 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 Privacy Guard 模块。依赖 ICleanEngine（Cleaner 模块）与 IRegistryEngine 已注册。</summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerPrivacyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<PrivacyGuardEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, PrivacyModule>());
        return services;
    }
}

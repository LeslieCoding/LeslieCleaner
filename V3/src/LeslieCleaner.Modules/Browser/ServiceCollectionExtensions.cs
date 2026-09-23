using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Browser;

/// <summary>LeslieCleaner.Modules.Browser 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Browser Cleaner 模块：清理引擎（同时作为 <see cref="IBrowserPrivacyCleaner"/> 供
    /// Privacy Guard 复用）与模块本身。依赖 Infrastructure（隔离区/配置/路径/日志）已先行注册。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerBrowserModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<BrowserCleanerEngine>();
        services.TryAddSingleton<IBrowserPrivacyCleaner>(static provider =>
            provider.GetRequiredService<BrowserCleanerEngine>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, BrowserModule>());
        return services;
    }
}

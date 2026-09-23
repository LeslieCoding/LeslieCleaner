using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Modules.Cleaner.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Cleaner;

/// <summary>LeslieCleaner.Modules.Cleaner 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Cleaner 模块：Smart Scan 引擎、清理引擎、扫描环境与模块本身。
    /// 依赖 Infrastructure（隔离区/备份/注册表）与 Data 已先行注册。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerCleanerModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IScanEnvironment, SystemScanEnvironment>();
        }

        services.TryAddSingleton<SmartScanEngine>();
        services.TryAddSingleton<ICleanEngine, CleanEngine>();
        services.TryAddSingleton<Duplicates.DuplicateFinderEngine>();
        services.TryAddSingleton<Analytics.LargeFileAnalyzerEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, CleanerModule>());
        return services;
    }
}

using LeslieCleaner.Core.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Core;

/// <summary>LeslieCleaner.Core 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Core 层基础服务（线程原语等）。接口契约本身无需注册，
    /// 实现由 Infrastructure / Data 的 Add 扩展提供。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddTransient<PauseTokenSource>();
        return services;
    }
}

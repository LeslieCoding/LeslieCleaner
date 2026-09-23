using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Startup;

/// <summary>LeslieCleaner.Modules.Startup 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 Startup Manager 模块。</summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerStartupModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<StartupManagerEngine>();
#pragma warning disable CA1416 // Factory is registered only inside the OperatingSystem.IsWindows guard.
            services.TryAddSingleton<IStartupEntryOperations>(provider =>
                provider.GetRequiredService<StartupManagerEngine>());
#pragma warning restore CA1416
            services.TryAddSingleton<IDelayedStartupTaskController, WindowsDelayedStartupTaskController>();
            services.TryAddSingleton<IStartupSystemStateProbe, WindowsStartupSystemStateProbe>();
            services.TryAddSingleton<IStartupClock, SystemStartupClock>();
            services.TryAddSingleton<StartupVerifiedActionManager>();
            services.TryAddSingleton<StartupLifecycleManager>();
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, StartupModule>());
        return services;
    }
}

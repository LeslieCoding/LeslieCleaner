using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Execution;
using LeslieCleaner.Modules.Uninstaller.Residual;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Uninstaller;

/// <summary>Smart Uninstaller 模块（圣经第一节功能清单第 3 条，核心模块）。</summary>
public sealed class UninstallerModule : IModule
{
    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.uninstaller",
        "Smart Uninstaller",
        new Version(2, 0, 0),
        "全来源应用发现、批量卸载、Multi-Stage Residual Pipeline 秒级残留定位");

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

/// <summary>LeslieCleaner.Modules.Uninstaller 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 Smart Uninstaller 模块：应用发现、卸载执行器、残留扫描器。</summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerUninstallerModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            services.TryAddSingleton<InstalledAppDiscovery>();
            services.TryAddSingleton<UninstallExecutor>();
            services.TryAddSingleton<ResidualScannerEngine>();
            services.TryAddSingleton<IResidualScanner>(static provider =>
                provider.GetRequiredService<ResidualScannerEngine>());
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, UninstallerModule>());
        return services;
    }
}

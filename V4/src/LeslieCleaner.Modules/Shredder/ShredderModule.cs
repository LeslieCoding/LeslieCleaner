using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.Shredder;

/// <summary>File Shredder 模块（圣经第一节功能清单第 7 条）。</summary>
public sealed class ShredderModule : IModule
{
    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.shredder",
        "File Shredder",
        new Version(2, 0, 0),
        "DoD 3/7 遍与 Gutmann 35 遍安全粉碎，SSD 安全策略，任务队列，关键路径防护");

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

/// <summary>LeslieCleaner.Modules.Shredder 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 File Shredder 模块。</summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerShredderModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ShredderEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, ShredderModule>());
        return services;
    }
}

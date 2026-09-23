using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Modules.AI;

/// <summary>AI Optimizer 模块（圣经第一节功能清单第 10 条）。</summary>
public sealed class AiModule : IModule
{
    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.ai",
        "AI Optimizer",
        new Version(2, 0, 0),
        "系统健康评分、启动/隐私/存储优化建议、自动维护计划（规则引擎，全部基于真实数据）");

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

/// <summary>LeslieCleaner.Modules.AI 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 AI Optimizer 模块。</summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerAiModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<MaintenancePlanner>();
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule, AiModule>());
        return services;
    }
}

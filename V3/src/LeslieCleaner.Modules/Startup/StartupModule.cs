using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Startup;

/// <summary>Startup Manager 模块（圣经第一节功能清单第 5 条）。</summary>
public sealed class StartupModule : IModule
{
    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.startup",
        "Startup Manager",
        new Version(2, 0, 0),
        "注册表/启动文件夹/计划任务/服务/驱动启动项管理，备份式禁用，启动影响评分与建议");

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

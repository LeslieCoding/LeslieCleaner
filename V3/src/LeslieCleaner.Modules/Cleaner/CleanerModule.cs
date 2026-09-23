using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner.Scanning;

namespace LeslieCleaner.Modules.Cleaner;

/// <summary>Smart Scan / 清理模块（圣经第一节功能清单第 1 条）。</summary>
public sealed class CleanerModule : IModule
{
    private readonly IScanEnvironment _environment;

    /// <summary>以扫描环境创建模块。</summary>
    /// <param name="environment">扫描环境。</param>
    public CleanerModule(IScanEnvironment environment) => _environment = environment;

    /// <inheritdoc />
    public ModuleInfo Info { get; } = new(
        "lesliecleaner.cleaner",
        "Smart Scan",
        new Version(2, 0, 0),
        "十四类系统垃圾扫描与安全清理（临时文件/缓存/转储/注册表/浏览器/AI 工具缓存等）");

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 触发一次位置解析，提前暴露环境异常
        _ = _environment.ResolveLocation("Temp");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int availableTargets = 0;
        foreach (ScanTargetDefinition target in SmartScanTargets.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Id == SmartScanTargets.RegistryCategoryId)
            {
                availableTargets++;
                continue;
            }

            foreach (string pattern in target.CandidatePatterns)
            {
                int separatorIndex = pattern.IndexOf(':');
                if (separatorIndex > 0 &&
                    _environment.ExpandDirectories(pattern[..separatorIndex], pattern[(separatorIndex + 1)..]).Count > 0)
                {
                    availableTargets++;
                    break;
                }
            }
        }

        ModuleStatus status = availableTargets >= SmartScanTargets.All.Count / 2
            ? ModuleStatus.Healthy
            : ModuleStatus.Degraded;
        return Task.FromResult(new ModuleHealth(
            Info.Id, status,
            $"可用扫描目标 {availableTargets}/{SmartScanTargets.All.Count}",
            DateTimeOffset.UtcNow));
    }
}

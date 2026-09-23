using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 受信任插件目录管理契约。发现阶段只验证清单与程序集哈希，不加载或执行第三方代码。
/// </summary>
public interface IPluginManager
{
    /// <summary>发现并验证本地插件。</summary>
    Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>设置已通过验证插件的启用状态。</summary>
    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);
}

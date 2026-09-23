using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Manages validated plugin packages without executing third-party assemblies in the main process.</summary>
public interface IPluginManager
{
    /// <summary>Discovers installed plugins and validates package integrity.</summary>
    Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
    /// <summary>Validates and installs a package. New plugins remain disabled by default.</summary>
    Task<PluginOperationResult> InstallAsync(PluginInstallRequest request, CancellationToken cancellationToken = default);
    /// <summary>Updates a plugin after backing up its current files and restores them if replacement fails.</summary>
    Task<PluginOperationResult> UpdateAsync(PluginInstallRequest request, CancellationToken cancellationToken = default);
    /// <summary>Removes an installed plugin after checking reverse dependencies.</summary>
    Task<PluginOperationResult> RemoveAsync(PluginRemovalRequest request, CancellationToken cancellationToken = default);
    /// <summary>Changes the persisted enabled state of an installed, validated plugin.</summary>
    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);
    /// <summary>Returns persisted lifecycle audit records, newest first.</summary>
    Task<IReadOnlyList<PluginAuditRecord>> GetAuditAsync(CancellationToken cancellationToken = default);
}

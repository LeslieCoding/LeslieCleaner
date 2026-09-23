using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Persists plugin lifecycle state and immutable audit records.</summary>
public interface IPluginStateStore
{
    /// <summary>Returns all persisted plugin states.</summary>
    Task<IReadOnlyList<PluginStateRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates or replaces a plugin state.</summary>
    Task UpsertAsync(PluginStateRecord state, CancellationToken cancellationToken = default);
    /// <summary>Removes a plugin state.</summary>
    Task RemoveAsync(string pluginId, CancellationToken cancellationToken = default);
    /// <summary>Appends an audit record.</summary>
    Task AppendAuditAsync(PluginAuditRecord record, CancellationToken cancellationToken = default);
    /// <summary>Returns audit records newest first.</summary>
    Task<IReadOnlyList<PluginAuditRecord>> GetAuditAsync(CancellationToken cancellationToken = default);
}

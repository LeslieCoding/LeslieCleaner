using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Plugins;

/// <summary>Crash-safe JSON persistence for plugin lifecycle state and audit history.</summary>
public sealed class JsonPluginStateStore : IPluginStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a store below the application data directory.</summary>
    public JsonPluginStateStore(IPathService paths)
    {
        string directory = paths.EnsureDirectory(Path.Combine(paths.ProgramDataRoot, "Plugins"));
        _statePath = Path.Combine(directory, "plugin-state.json");
    }

    /// <summary>Performs the documented operation.</summary>
    public async Task<IReadOnlyList<PluginStateRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        PluginStoreDocument document = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return document.Plugins.ToArray();
    }

    /// <summary>Performs the documented operation.</summary>
    public Task UpsertAsync(PluginStateRecord state, CancellationToken cancellationToken = default) =>
        MutateAsync(document =>
        {
            document.Plugins.RemoveAll(item => string.Equals(item.PluginId, state.PluginId, StringComparison.OrdinalIgnoreCase));
            document.Plugins.Add(state);
        }, cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public Task RemoveAsync(string pluginId, CancellationToken cancellationToken = default) =>
        MutateAsync(document => document.Plugins.RemoveAll(item =>
            string.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)), cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public Task AppendAuditAsync(PluginAuditRecord record, CancellationToken cancellationToken = default) =>
        MutateAsync(document =>
        {
            document.Audit.Add(record);
            if (document.Audit.Count > 2_000)
            {
                document.Audit.RemoveRange(0, document.Audit.Count - 2_000);
            }
        }, cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public async Task<IReadOnlyList<PluginAuditRecord>> GetAuditAsync(CancellationToken cancellationToken = default)
    {
        PluginStoreDocument document = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return document.Audit.OrderByDescending(static item => item.Timestamp).ToArray();
    }

    private async Task MutateAsync(Action<PluginStoreDocument> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PluginStoreDocument document = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            mutation(document);
            string temporaryPath = _statePath + ".tmp";
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _statePath, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PluginStoreDocument> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<PluginStoreDocument> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath)) return new PluginStoreDocument();
        await using FileStream stream = new(_statePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<PluginStoreDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? new PluginStoreDocument();
    }

    private sealed class PluginStoreDocument
    {
        public List<PluginStateRecord> Plugins { get; init; } = [];
        public List<PluginAuditRecord> Audit { get; init; } = [];
    }
}

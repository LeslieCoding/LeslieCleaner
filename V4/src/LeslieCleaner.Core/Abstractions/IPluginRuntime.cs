using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Executes enabled plugins exclusively in disposable child processes.</summary>
public interface IPluginRuntime
{
    /// <summary>Starts and initializes a dedicated host process for one enabled plugin.</summary>
    Task<PluginRuntimeResult> StartAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>Executes one operation and enforces its deadline and resource budget.</summary>
    Task<PluginRuntimeResult> ExecuteAsync(string pluginId, PluginRuntimeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stops and removes a plugin host process.</summary>
    Task<PluginRuntimeResult> StopAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>Gets the latest process telemetry without loading plugin code.</summary>
    Task<PluginRuntimeTelemetry?> GetTelemetryAsync(string pluginId, CancellationToken cancellationToken = default);
}

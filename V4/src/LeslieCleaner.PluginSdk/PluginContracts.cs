using System.Text.Json;

namespace LeslieCleaner.PluginSdk;

/// <summary>Identifies the stable plugin API implemented by this assembly.</summary>
public static class PluginApi
{
    /// <summary>The exact contract version implemented by this SDK.</summary>
    public const string Version = "1.0";
}

/// <summary>The only contract a LeslieCleaner plugin must implement.</summary>
public interface IPlugin
{
    /// <summary>Initializes the plugin once inside the isolated host process.</summary>
    ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken);

    /// <summary>Executes one named plugin operation.</summary>
    ValueTask<PluginExecutionResponse> ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken);

    /// <summary>Stops background work and releases plugin-owned resources.</summary>
    ValueTask StopAsync(CancellationToken cancellationToken);
}

/// <summary>Host-controlled services exposed to a plugin.</summary>
public interface IPluginContext
{
    /// <summary>Opaque, short-lived permission grant created by the main process.</summary>
    PluginPermissionToken PermissionToken { get; }

    /// <summary>Requests a capability through the host permission boundary.</summary>
    ValueTask<PluginCapabilityResponse> RequestCapabilityAsync(
        PluginCapabilityRequest request,
        CancellationToken cancellationToken);
}

/// <summary>A command passed to a plugin.</summary>
public sealed record PluginExecutionRequest(string Operation, JsonElement Payload);

/// <summary>A command result returned by a plugin.</summary>
public sealed record PluginExecutionResponse(
    bool Succeeded,
    JsonElement Payload,
    string? ErrorCode = null,
    string? Message = null);

/// <summary>A privileged operation requested through the host rather than performed by the main process.</summary>
public sealed record PluginCapabilityRequest(
    string Permission,
    string Operation,
    string? Target = null,
    JsonElement Payload = default);

/// <summary>Result of a host-mediated capability request.</summary>
public sealed record PluginCapabilityResponse(
    bool Granted,
    JsonElement Payload,
    string? ErrorCode = null,
    string? Message = null);

/// <summary>An opaque grant and its effective least-privilege permission set.</summary>
public sealed record PluginPermissionToken(
    string Token,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAtUtc);

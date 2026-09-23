using System.Text.Json;

namespace LeslieCleaner.PluginSdk;

/// <summary>Versioned JSON command protocol transported over a local named pipe.</summary>
public static class PluginProtocol
{
    /// <summary>Current wire-protocol version.</summary>
    public const string Version = "1.0";

    /// <summary>Maximum accepted newline-delimited JSON frame.</summary>
    public const int MaxFrameCharacters = 1_048_576;

    /// <summary>Initialization command.</summary>
    public const string Initialize = "initialize";

    /// <summary>Execution command.</summary>
    public const string Execute = "execute";

    /// <summary>Graceful stop command.</summary>
    public const string Stop = "stop";
}

/// <summary>A single authenticated request frame.</summary>
public sealed record PluginHostCommand(
    string ProtocolVersion,
    Guid RequestId,
    string Command,
    string AuthenticationToken,
    JsonElement Payload);

/// <summary>A single response frame.</summary>
public sealed record PluginHostResponse(
    string ProtocolVersion,
    Guid RequestId,
    bool Succeeded,
    JsonElement Payload,
    string? ErrorCode = null,
    string? Message = null);

/// <summary>Payload used to create and initialize one isolated plugin instance.</summary>
public sealed record PluginInitializePayload(
    string PluginId,
    string AssemblyPath,
    string ApiVersion,
    PluginPermissionToken PermissionToken);

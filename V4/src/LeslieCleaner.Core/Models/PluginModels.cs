namespace LeslieCleaner.Core.Models;

/// <summary>Validation and lifecycle status of a plugin package.</summary>
public enum PluginTrustStatus
{
    /// <summary>Package validated and enabled.</summary>
    Ready,
    /// <summary>Package validated but disabled.</summary>
    Disabled,
    /// <summary>Manifest is missing or invalid.</summary>
    InvalidManifest,
    /// <summary>Declared entry assembly is missing.</summary>
    MissingAssembly,
    /// <summary>Assembly hash does not match the manifest.</summary>
    HashMismatch,
    /// <summary>Package contains an unsafe or escaping path.</summary>
    UnsafePath,
    /// <summary>Plugin API major version is incompatible.</summary>
    IncompatibleApi,
    /// <summary>A required plugin dependency is missing.</summary>
    MissingDependency,
    /// <summary>A requested permission is invalid or unapproved.</summary>
    PermissionDenied,
}

/// <summary>Validated plugin metadata presented to callers.</summary>
public sealed record PluginDescriptor(
    string Id,
    string Name,
    string Version,
    string Description,
    string ManifestPath,
    string AssemblyPath,
    PluginTrustStatus TrustStatus,
    string? Detail,
    string Author = "",
    string Type = "",
    string ApiVersion = "",
    IReadOnlyList<string>? Permissions = null,
    IReadOnlyList<PluginDependency>? Dependencies = null);

/// <summary>A dependency on another installed plugin.</summary>
public sealed record PluginDependency(string Id, string MinimumVersion);

/// <summary>Request to install or update a directory or ZIP package.</summary>
public sealed record PluginInstallRequest(
    string PackagePath,
    IReadOnlyCollection<string>? ApprovedPermissions = null,
    bool DangerousPermissionsConfirmed = false);

/// <summary>Request to remove a plugin and optionally its private data.</summary>
public sealed record PluginRemovalRequest(string PluginId, bool DeleteUserData = false);

/// <summary>Result of a plugin lifecycle operation.</summary>
public sealed record PluginOperationResult(
    bool Success,
    string? PluginId,
    string Message,
    string? ErrorCode = null,
    PluginDescriptor? Plugin = null,
    string? BackupPath = null,
    bool RolledBack = false);

/// <summary>Persisted plugin lifecycle state.</summary>
public sealed record PluginStateRecord(
    string PluginId,
    string Name,
    string Version,
    bool Enabled,
    string InstallPath,
    IReadOnlyList<string> ApprovedPermissions,
    DateTimeOffset InstalledAt,
    DateTimeOffset UpdatedAt);

/// <summary>Persisted plugin lifecycle audit entry.</summary>
public sealed record PluginAuditRecord(
    Guid OperationId,
    string Operation,
    string? PluginId,
    bool Success,
    string Message,
    DateTimeOffset Timestamp,
    string? BackupPath = null,
    bool RolledBack = false);


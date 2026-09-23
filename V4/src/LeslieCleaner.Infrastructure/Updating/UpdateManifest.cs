using System.Text.Json.Serialization;
using LeslieCleaner.Core.Threading;

namespace LeslieCleaner.Infrastructure.Updating;

/// <summary>Supported update release channels.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateChannel
{
    /// <summary>Recommended production channel.</summary>
    Stable,
    /// <summary>Preview testing channel.</summary>
    Beta,
    /// <summary>Internal development channel.</summary>
    Development,
}

/// <summary>Package selection strategy.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdatePackageKind
{
    /// <summary>Changed files from a specific version.</summary>
    Incremental,
    /// <summary>Complete application package.</summary>
    Full,
}

/// <summary>Outcome recorded in update history.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateOutcome
{
    /// <summary>Update check completed.</summary>
    Checked,
    /// <summary>Package downloaded and verified.</summary>
    Downloaded,
    /// <summary>Update installed successfully.</summary>
    Installed,
    /// <summary>Update failed.</summary>
    Failed,
    /// <summary>Update failed and was restored.</summary>
    RolledBack,
}

/// <summary>Update manifest compatibility constraints.</summary>
public sealed record UpdateCompatibility(
    [property: JsonPropertyName("minimumVersion")] string? MinimumVersion = null,
    [property: JsonPropertyName("operatingSystems")] IReadOnlyList<string>? OperatingSystems = null,
    [property: JsonPropertyName("architectures")] IReadOnlyList<string>? Architectures = null,
    [property: JsonPropertyName("requiredFiles")] IReadOnlyList<string>? RequiredFiles = null);

/// <summary>One complete or incremental package candidate.</summary>
public sealed record UpdatePackageDescriptor(
    [property: JsonPropertyName("kind")] UpdatePackageKind Kind,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("fromVersion")] string? FromVersion = null,
    [property: JsonPropertyName("files")] IReadOnlyList<UpdateFileEntry>? Files = null);

/// <summary>Declarative migration identity. The updater never executes manifest-provided scripts.</summary>
public sealed record UpdateMigrationDescriptor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("fromVersion")] string FromVersion,
    [property: JsonPropertyName("toVersion")] string ToVersion);

/// <summary>A single file in an update manifest.</summary>
public sealed record UpdateFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("url")] string Url);

/// <summary>A signed server update manifest.</summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("files")] IReadOnlyList<UpdateFileEntry> Files,
    [property: JsonPropertyName("releaseNotes")] string? ReleaseNotes,
    [property: JsonPropertyName("signature")] string? Signature = null,
    [property: JsonPropertyName("signingKeyId")] string? SigningKeyId = null,
    [property: JsonPropertyName("channel")] UpdateChannel Channel = UpdateChannel.Stable,
    [property: JsonPropertyName("mandatory")] bool Mandatory = false,
    [property: JsonPropertyName("compatibility")] UpdateCompatibility? Compatibility = null,
    [property: JsonPropertyName("packages")] IReadOnlyList<UpdatePackageDescriptor>? Packages = null,
    [property: JsonPropertyName("migrations")] IReadOnlyList<UpdateMigrationDescriptor>? Migrations = null,
    [property: JsonPropertyName("restartRequired")] bool RestartRequired = true);

/// <summary>Update check result including the selected package strategy.</summary>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    UpdateManifest? Manifest,
    IReadOnlyList<UpdateFileEntry> FilesToDownload,
    UpdatePackageKind PackageKind = UpdatePackageKind.Incremental,
    string? RejectionReason = null);

/// <summary>Live aggregate download telemetry.</summary>
public sealed record UpdateDownloadProgress(
    string CurrentFile,
    long BytesDownloaded,
    long TotalBytes,
    double BytesPerSecond,
    double Percent);

/// <summary>Controls pause, timeout and progress for a resumable download.</summary>
public sealed record UpdateDownloadOptions(
    PauseToken PauseToken,
    IProgress<UpdateDownloadProgress>? Progress = null,
    TimeSpan? Timeout = null,
    int BufferSize = 81920)
{
    /// <summary>Default non-paused download options.</summary>
    public static UpdateDownloadOptions Default { get; } = new(PauseToken.None);
}

/// <summary>Compatibility preflight result.</summary>
public sealed record UpdateCompatibilityResult(bool IsCompatible, string? Reason);

/// <summary>Persisted transparent update history entry.</summary>
public sealed record UpdateHistoryRecord(
    Guid Id,
    string OldVersion,
    string NewVersion,
    UpdateChannel Channel,
    UpdatePackageKind PackageKind,
    UpdateOutcome Outcome,
    DateTimeOffset CreatedUtc,
    string? Error,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    IReadOnlyList<string> ChangedFiles,
    bool MigrationSucceeded,
    bool RestartRequired);

/// <summary>Result of the data-protection and migration transaction.</summary>
public sealed record UpdateTransactionResult(bool Success, bool RolledBack, string? Error, string ReportPath);

/// <summary>Trusted application-owned database/config migration hook.</summary>
public interface IUpdateDataMigration
{
    /// <summary>Backs up database and configuration before any migration.</summary>
    Task<string> BackupAsync(string oldVersion, string newVersion, CancellationToken cancellationToken);

    /// <summary>Runs only application-owned, compiled migrations identified by the signed manifest.</summary>
    Task MigrateAsync(IReadOnlyList<UpdateMigrationDescriptor> migrations, CancellationToken cancellationToken);

    /// <summary>Verifies migrated application data.</summary>
    Task<bool> VerifyAsync(CancellationToken cancellationToken);

    /// <summary>Restores the backup after migration or verification failure.</summary>
    Task RestoreAsync(string backupId, CancellationToken cancellationToken);
}
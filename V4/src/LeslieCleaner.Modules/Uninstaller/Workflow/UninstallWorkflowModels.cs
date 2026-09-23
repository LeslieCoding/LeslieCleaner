using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Application-owned resource type stored in a software fingerprint.</summary>
public enum FingerprintResourceType
{
    /// <summary>A file.</summary>
    File,
    /// <summary>A directory.</summary>
    Folder,
    /// <summary>A registry key.</summary>
    Registry,
    /// <summary>A Windows service.</summary>
    Service,
    /// <summary>A startup entry.</summary>
    Startup,
    /// <summary>A scheduled task.</summary>
    Task,
    /// <summary>A configuration file or directory.</summary>
    Configuration,
}

/// <summary>One evidence-backed resource belonging to an application.</summary>
/// <param name="Type">Resource type.</param>
/// <param name="Value">Normalized resource identifier.</param>
/// <param name="Confidence">Ownership confidence from 0 to 100.</param>
/// <param name="Evidence">Short explanation of the ownership evidence.</param>
public sealed record FingerprintEntry(
    FingerprintResourceType Type,
    string Value,
    int Confidence,
    string Evidence);

/// <summary>Persistable software fingerprint used for targeted residual discovery and recovery.</summary>
/// <param name="Id">Stable fingerprint identifier.</param>
/// <param name="ApplicationId">Application identifier.</param>
/// <param name="ApplicationName">Application display name.</param>
/// <param name="Entries">Owned resources.</param>
/// <param name="CreatedUtc">UTC creation time.</param>
public sealed record ApplicationFingerprint(
    Guid Id,
    string ApplicationId,
    string ApplicationName,
    IReadOnlyList<FingerprintEntry> Entries,
    DateTimeOffset CreatedUtc);

/// <summary>Severity of a pre-uninstall safety finding.</summary>
public enum UninstallSafetySeverity
{
    /// <summary>Informational finding.</summary>
    Information,
    /// <summary>User should review the finding.</summary>
    Warning,
    /// <summary>Operation must not start.</summary>
    Blocking,
}

/// <summary>One pre-uninstall safety finding.</summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="Message">User-facing explanation.</param>
/// <param name="Severity">Finding severity.</param>
public sealed record UninstallSafetyFinding(
    string Code,
    string Message,
    UninstallSafetySeverity Severity);

/// <summary>Result of application existence, command, process, and backup-readiness checks.</summary>
/// <param name="CanProceed">Whether the workflow may start.</param>
/// <param name="RunningProcesses">Processes whose executable is inside the install directory.</param>
/// <param name="Findings">Safety findings.</param>
public sealed record UninstallSafetyAssessment(
    bool CanProceed,
    IReadOnlyList<string> RunningProcesses,
    IReadOnlyList<UninstallSafetyFinding> Findings);

/// <summary>Request for the safe uninstall workflow.</summary>
/// <param name="Application">Application to uninstall.</param>
/// <param name="UserConfirmed">Explicit confirmation obtained by the caller.</param>
/// <param name="AllowForcedCleanup">Allow confirmed residual cleanup when no official uninstaller exists.</param>
/// <param name="AdditionalFingerprintEntries">Extra service/startup/task/configuration evidence collected by platform scanners.</param>
public sealed record UninstallWorkflowRequest(
    InstalledApp Application,
    bool UserConfirmed,
    bool AllowForcedCleanup = false,
    IReadOnlyList<FingerprintEntry>? AdditionalFingerprintEntries = null);

/// <summary>Auditable uninstall result and recovery handles.</summary>
/// <param name="OperationId">Operation identifier.</param>
/// <param name="ApplicationId">Application identifier.</param>
/// <param name="ApplicationName">Application name.</param>
/// <param name="StartedUtc">UTC start time.</param>
/// <param name="FinishedUtc">UTC finish time.</param>
/// <param name="Method">Uninstall method.</param>
/// <param name="Success">Whether the requested workflow completed.</param>
/// <param name="ForcedCleanup">Whether forced residual cleanup was used.</param>
/// <param name="ResidualItemCount">Detected residual count.</param>
/// <param name="ResidualBytes">Detected residual bytes.</param>
/// <param name="BackupIds">Backups that can be restored.</param>
/// <param name="FingerprintId">Stored fingerprint identifier.</param>
/// <param name="RemainingItems">Items retained for safety or left after failure.</param>
/// <param name="Error">Failure explanation.</param>
public sealed record UninstallWorkflowReport(
    Guid OperationId,
    string ApplicationId,
    string ApplicationName,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string Method,
    bool Success,
    bool ForcedCleanup,
    int ResidualItemCount,
    long ResidualBytes,
    IReadOnlyList<Guid> BackupIds,
    Guid FingerprintId,
    IReadOnlyList<ResidualItem> RemainingItems,
    string? Error)
{
    /// <summary>Whether at least one verified backup is available for an explicit restore.</summary>
    public bool RestoreAvailable => BackupIds.Count > 0;
}


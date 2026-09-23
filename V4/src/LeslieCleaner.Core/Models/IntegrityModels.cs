namespace LeslieCleaner.Core.Models;

/// <summary>Kind of protected artifact covered by the startup integrity manifest.</summary>
public enum IntegrityArtifactKind
{
    /// <summary>Main executable or another executable shipped with the application.</summary>
    Executable,
    /// <summary>Core application library.</summary>
    CoreLibrary,
    /// <summary>Application configuration.</summary>
    Configuration,
    /// <summary>Application database.</summary>
    Database,
    /// <summary>Plugin manifest or other plugin metadata.</summary>
    PluginMetadata,
}

/// <summary>Verification state persisted for one protected file.</summary>
public enum FileIntegrityStatus
{
    /// <summary>The file matches its trusted baseline.</summary>
    Valid,
    /// <summary>The file does not exist.</summary>
    Missing,
    /// <summary>The file size or SHA-256 hash differs from the trusted baseline.</summary>
    Modified,
    /// <summary>The optional trusted signature is invalid.</summary>
    SignatureInvalid,
    /// <summary>The file could not be read or hashed.</summary>
    VerificationFailed,
    /// <summary>The manifest path failed boundary or reparse-point validation.</summary>
    UnsafePath,
}

/// <summary>A file selected by an installer or explicitly authorized user for a trusted baseline.</summary>
public sealed record TrustedIntegrityTarget(string FilePath, IntegrityArtifactKind Kind,
    string? SignatureBase64 = null);

/// <summary>Explicit request to establish or replace the trusted integrity baseline.</summary>
public sealed record TrustedBaselineRequest(string ApplicationRoot, string TrustedBackupRoot,
    IReadOnlyList<TrustedIntegrityTarget> Targets, bool ExplicitlyAuthorized,
    string AuthorizationReason, bool ReplaceExisting = false);

/// <summary>Result of an explicit baseline operation.</summary>
public sealed record TrustedBaselineResult(bool Succeeded, string Message, int ProtectedFileCount,
    DateTimeOffset CompletedUtc);

/// <summary>Persisted evidence corresponding to the FileIntegrityRecords architecture table.</summary>
public sealed record FileIntegrityRecord(Guid Id, string FilePath, IntegrityArtifactKind Kind,
    string ExpectedSha256, string? ActualSha256, long ExpectedSize, long? ActualSize,
    FileIntegrityStatus Status, SecurityRiskLevel RiskLevel, DateTimeOffset LastCheckTimeUtc,
    string Description);

/// <summary>Complete startup integrity verification result.</summary>
public sealed record StartupIntegrityReport(bool ManifestTrusted, IReadOnlyList<FileIntegrityRecord> Records,
    int HighRiskFailureCount, int LowRiskFailureCount, DateTimeOffset CompletedUtc)
{
    /// <summary>Whether every protected file passed verification.</summary>
    public bool IsValid => ManifestTrusted && HighRiskFailureCount == 0 && LowRiskFailureCount == 0;
}

/// <summary>Non-integrity conditions that must also pass before emergency read-only mode can be cleared.</summary>
public sealed record IntegrityRecoveryPrerequisites(bool DatabaseIntegrityValid, bool AuditIntegrityValid,
    bool PermissionPolicySafe, bool UpdateTrusted, bool PluginSecurityValid, bool EncryptionHealthy)
{
    /// <summary>Whether all independent recovery conditions are satisfied.</summary>
    public bool AllSatisfied => DatabaseIntegrityValid && AuditIntegrityValid && PermissionPolicySafe &&
        UpdateTrusted && PluginSecurityValid && EncryptionHealthy;
}

/// <summary>Result of restoring failed artifacts from the trusted backup.</summary>
public sealed record IntegrityRepairResult(bool Succeeded, bool EmergencyModeCleared, int RestoredFileCount,
    StartupIntegrityReport Verification, IReadOnlyList<string> Errors, DateTimeOffset CompletedUtc);

/// <summary>Inputs for the strictly weighted security health score.</summary>
public sealed record SecurityHealthFactors(bool IntegrityValid, bool PermissionSafe, bool UpdateTrusted,
    bool PluginSecurityValid, bool EncryptionHealthy);

/// <summary>Strict 30/20/20/15/15 security health score and contribution breakdown.</summary>
public sealed record SecurityHealthScore(int Score, int IntegrityPoints, int PermissionPoints,
    int UpdatePoints, int PluginPoints, int EncryptionPoints, SecurityRiskLevel RiskLevel);

/// <summary>Result of safely rotating the key for encrypted local artifacts.</summary>
public sealed record ProtectedArtifactRotationResult(bool Succeeded, string? PreviousKeyId,
    string? ActiveKeyId, int ReEncryptedArtifactCount, IReadOnlyList<string> Errors,
    DateTimeOffset CompletedUtc);
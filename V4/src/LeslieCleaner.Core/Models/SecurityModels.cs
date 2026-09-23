namespace LeslieCleaner.Core.Models;

/// <summary>Security risk communicated to the user and written to the security audit trail.</summary>
public enum SecurityRiskLevel
{
    /// <summary>No actionable security risk was detected.</summary>
    Safe,
    /// <summary>A low-risk condition should be reviewed.</summary>
    Notice,
    /// <summary>A significant risk requires user attention.</summary>
    Warning,
    /// <summary>A critical risk requires fail-safe protection.</summary>
    Critical,
}

/// <summary>Severity of a security audit event.</summary>
public enum SecurityAuditLevel
{
    /// <summary>Informational security activity.</summary>
    Information,
    /// <summary>A security condition requiring review.</summary>
    Warning,
    /// <summary>A critical security event.</summary>
    Critical,
}

/// <summary>Well-known security event types used by all security components.</summary>
public enum SecurityAuditEventType
{
    /// <summary>A component requested permission.</summary>
    PermissionRequest,
    /// <summary>A permission request was denied.</summary>
    PermissionDenied,
    /// <summary>A permission request was granted.</summary>
    PermissionGranted,
    /// <summary>An integrity verification failed.</summary>
    IntegrityFailure,
    /// <summary>An explicitly authorized trusted integrity baseline was established.</summary>
    IntegrityBaselineEstablished,
    /// <summary>A trusted integrity repair completed and passed re-verification.</summary>
    IntegrityRepairSucceeded,
    /// <summary>A trusted integrity repair failed or recovery conditions remained unsatisfied.</summary>
    IntegrityRepairFailed,
    /// <summary>An encryption operation failed.</summary>
    EncryptionError,
    /// <summary>An operation targeting a protected path was blocked.</summary>
    ProtectedPathBlocked,
    /// <summary>The application entered emergency read-only mode.</summary>
    EmergencyModeEntered,
    /// <summary>The application recovered from emergency mode.</summary>
    EmergencyModeRecovered,
    /// <summary>A complete security assessment was recorded.</summary>
    SecurityAssessment,
}

/// <summary>Resource family controlled by the least-privilege permission policy.</summary>
public enum SecurityPermissionCategory
{
    /// <summary>File-system resources.</summary>
    File,
    /// <summary>Windows Registry resources.</summary>
    Registry,
    /// <summary>Operating-system processes.</summary>
    Process,
    /// <summary>Network endpoints.</summary>
    Network,
}

/// <summary>Actions supported by the security permission model.</summary>
public enum SecurityPermissionAction
{
    /// <summary>Read an existing resource.</summary>
    Read,
    /// <summary>Write resource data.</summary>
    Write,
    /// <summary>Delete a resource.</summary>
    Delete,
    /// <summary>Modify resource metadata or state.</summary>
    Modify,
    /// <summary>Start a process or operation.</summary>
    Start,
    /// <summary>Stop a process or operation.</summary>
    Stop,
    /// <summary>Monitor a resource.</summary>
    Monitor,
    /// <summary>Connect to a network endpoint.</summary>
    Connect,
    /// <summary>Upload data to a network endpoint.</summary>
    Upload,
    /// <summary>Download data from a network endpoint.</summary>
    Download,
}

/// <summary>A single permission request. Requests are denied unless an active matching grant exists.</summary>
public sealed record SecurityPermissionRequest(string Subject, SecurityPermissionCategory Category,
    SecurityPermissionAction Action, string Resource, string Purpose);

/// <summary>An explicit, narrowly scoped permission grant.</summary>
public sealed record SecurityPermissionGrant(string Subject, SecurityPermissionCategory Category,
    SecurityPermissionAction Action, string Resource, bool IncludeDescendants = false,
    DateTimeOffset? ExpiresUtc = null);

/// <summary>Result of evaluating a permission request.</summary>
public sealed record SecurityPermissionDecision(bool Allowed, string NormalizedResource, string Reason,
    SecurityRiskLevel RiskLevel, string Recommendation, DateTimeOffset EvaluatedUtc);

/// <summary>Result of canonicalizing and checking a file-system path.</summary>
public sealed record PathSecurityDecision(bool Allowed, string? NormalizedPath, string Reason,
    SecurityRiskLevel RiskLevel, string Recommendation);

/// <summary>Explicit state of the fail-safe emergency operating mode.</summary>
public sealed record EmergencySecurityState(bool IsReadOnly, string Reason,
    IReadOnlyList<string> RecoveryConditions, DateTimeOffset ChangedUtc)
{
    /// <summary>The initial fail-safe state used until the first successful assessment.</summary>
    public static EmergencySecurityState AwaitingAssessment { get; } = new(true,
        "Security startup assessment has not completed.",
        ["Run a fresh security assessment and pass application, database, and audit integrity checks."],
        DateTimeOffset.UtcNow);
}

/// <summary>Public representation of an entry in the tamper-evident security audit trail.</summary>
public sealed record SecurityAuditEvent(DateTimeOffset TimeUtc, SecurityAuditEventType EventType,
    SecurityAuditLevel Level, string Source, string Result, string Description, string Recommendation);

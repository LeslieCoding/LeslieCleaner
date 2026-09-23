using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Fail-safe operation gate with an explicit emergency read-only lifecycle.</summary>
public sealed class SecurityOperationGateV4 : ISecurityOperationGate, IEmergencySecurityGate
{
    private EmergencySecurityState _state = EmergencySecurityState.AwaitingAssessment;

    /// <inheritdoc />
    public bool IsSensitiveOperationAllowed => !Volatile.Read(ref _state).IsReadOnly;

    /// <inheritdoc />
    public EmergencySecurityState EmergencyState => Volatile.Read(ref _state);

    /// <inheritdoc />
    public void Apply(SecurityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (assessment.ApplicationIntegrityValid && assessment.DatabaseIntegrityValid && assessment.AuditIntegrityValid)
        {
            Volatile.Write(ref _state, new EmergencySecurityState(false, string.Empty, [], DateTimeOffset.UtcNow));
            return;
        }

        var recovery = new List<string>();
        if (!assessment.ApplicationIntegrityValid) recovery.Add("Restore trusted application files and pass application integrity verification.");
        if (!assessment.DatabaseIntegrityValid) recovery.Add("Repair or restore the database and pass database integrity verification.");
        if (!assessment.AuditIntegrityValid) recovery.Add("Restore a trusted audit chain and pass audit verification.");
        recovery.Add("Run a fresh security assessment; read-only mode cannot be cleared manually.");
        EnterEmergencyReadOnly(assessment.Summary, recovery);
    }

    /// <inheritdoc />
    public void EnterEmergencyReadOnly(string reason, IReadOnlyList<string> recoveryConditions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(recoveryConditions);
        if (recoveryConditions.Count == 0 || recoveryConditions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one explicit recovery condition is required.", nameof(recoveryConditions));
        Volatile.Write(ref _state, new EmergencySecurityState(true, reason.Trim(),
            recoveryConditions.Select(static value => value.Trim()).ToArray(), DateTimeOffset.UtcNow));
    }

    /// <inheritdoc />
    public void EnsureSensitiveOperationAllowed(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        EmergencySecurityState state = EmergencyState;
        if (state.IsReadOnly)
            throw new LeslieCleanerException($"Emergency read-only mode blocked '{operationName}': {state.Reason} Recovery: {string.Join(" ", state.RecoveryConditions)}");
    }
}

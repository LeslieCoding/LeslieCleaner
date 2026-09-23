using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Composes integrity assessment, permission policy, emergency gate, and unified audit capabilities.</summary>
public sealed class AuditedSecurityManager : ISecurityManager, ISecurityManagerV4
{
    private readonly SecurityManager _inner;
    private readonly ISecurityPermissionPolicy _permissionPolicy;
    private readonly SecurityAuditStore _auditStore;
    private readonly ISecurityOperationGate _operationGate;
    private readonly ILogService _logService;

    /// <summary>Creates the composed V4 security manager.</summary>
    public AuditedSecurityManager(SecurityManager inner, ISecurityPermissionPolicy permissionPolicy,
        SecurityAuditStore auditStore, ISecurityOperationGate operationGate, ILogService logService)
    {
        _inner = inner;
        _permissionPolicy = permissionPolicy;
        _auditStore = auditStore;
        _operationGate = operationGate;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<SecurityAssessment> AssessAsync(CancellationToken cancellationToken = default)
    {
        SecurityAssessment assessment = await _inner.AssessAsync(cancellationToken).ConfigureAwait(false);
        _operationGate.Apply(assessment);
        SecurityRiskLevel risk = assessment.GetRiskLevel();
        string recommendation = string.Join(" ", assessment.GetRecommendations());
        await _auditStore.AppendAsync(new SecurityAuditEvent(assessment.EvaluatedUtc,
            assessment.ApplicationIntegrityValid && assessment.DatabaseIntegrityValid && assessment.AuditIntegrityValid
                ? SecurityAuditEventType.SecurityAssessment : SecurityAuditEventType.IntegrityFailure,
            risk == SecurityRiskLevel.Critical ? SecurityAuditLevel.Critical :
            risk == SecurityRiskLevel.Safe ? SecurityAuditLevel.Information : SecurityAuditLevel.Warning,
            nameof(AuditedSecurityManager), risk.ToString(), assessment.Summary, recommendation), cancellationToken)
            .ConfigureAwait(false);
        return assessment;
    }

    /// <inheritdoc />
    public Task<bool> VerifyAuditAsync(CancellationToken cancellationToken = default) =>
        _inner.VerifyAuditAsync(cancellationToken);

    /// <inheritdoc />
    public void ReplacePermissionGrants(IEnumerable<SecurityPermissionGrant> grants) =>
        _permissionPolicy.ReplaceGrants(grants);

    /// <inheritdoc />
    public async Task<SecurityPermissionDecision> RequestPermissionAsync(SecurityPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SecurityPermissionDecision decision = _permissionPolicy.Evaluate(request);
        SecurityAuditEventType eventType = decision.Allowed
            ? SecurityAuditEventType.PermissionGranted : SecurityAuditEventType.PermissionDenied;
        await _auditStore.AppendAsync(new SecurityAuditEvent(DateTimeOffset.UtcNow, eventType,
            decision.Allowed ? SecurityAuditLevel.Information : SecurityAuditLevel.Warning, request.Subject,
            decision.Allowed ? "Allowed" : "Denied", $"{request.Category}/{request.Action}: {decision.Reason}",
            decision.Recommendation), cancellationToken).ConfigureAwait(false);
        _logService.LogEvent(decision.Allowed ? LogEventKind.UserAction : LogEventKind.Exception,
            "Security permission decision", new Dictionary<string, object?>
            {
                ["EventType"] = eventType.ToString(), ["RiskLevel"] = decision.RiskLevel.ToString(),
                ["Source"] = request.Subject, ["Result"] = decision.Allowed ? "Allowed" : "Denied",
                ["Category"] = request.Category.ToString(), ["Action"] = request.Action.ToString(),
                ["Resource"] = decision.NormalizedResource, ["Recommendation"] = decision.Recommendation,
            });
        return decision;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityAuditEvent>> GetAuditEventsAsync(CancellationToken cancellationToken = default) =>
        _auditStore.ReadAsync(cancellationToken);
}

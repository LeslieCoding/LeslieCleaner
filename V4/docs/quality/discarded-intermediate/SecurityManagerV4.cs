using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Audited facade over the V4 default-deny permission policy.</summary>
public sealed class SecurityManagerV4 : ISecurityManagerV4
{
    private readonly ISecurityPermissionPolicy _permissionPolicy;
    private readonly SecurityManager _securityManager;
    private readonly ILogService _logService;

    /// <summary>Creates the V4 security facade over the existing integrity manager.</summary>
    public SecurityManagerV4(ISecurityPermissionPolicy permissionPolicy, SecurityManager securityManager,
        ILogService logService)
    {
        _permissionPolicy = permissionPolicy;
        _securityManager = securityManager;
        _logService = logService;
    }

    /// <inheritdoc />
    public void ReplacePermissionGrants(IEnumerable<SecurityPermissionGrant> grants) =>
        _permissionPolicy.ReplaceGrants(grants);

    /// <inheritdoc />
    public async Task<SecurityPermissionDecision> RequestPermissionAsync(SecurityPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SecurityPermissionDecision decision = _permissionPolicy.Evaluate(request);
        var audit = new SecurityAuditEvent(DateTimeOffset.UtcNow,
            decision.Allowed ? SecurityAuditEventType.PermissionGranted : SecurityAuditEventType.PermissionDenied,
            decision.Allowed ? SecurityAuditLevel.Information : SecurityAuditLevel.Warning,
            request.Subject, decision.Allowed ? "Allowed" : "Denied",
            $"{request.Category}/{request.Action}: {decision.Reason}", decision.Recommendation);
        await _securityManager.AppendSecurityAuditAsync(audit, cancellationToken).ConfigureAwait(false);
        _logService.LogEvent(decision.Allowed ? LogEventKind.UserAction : LogEventKind.Exception,
            "Security permission decision", new Dictionary<string, object?>
            {
                ["EventType"] = audit.EventType.ToString(),
                ["Level"] = audit.Level.ToString(),
                ["Source"] = audit.Source,
                ["Result"] = audit.Result,
                ["Category"] = request.Category.ToString(),
                ["Action"] = request.Action.ToString(),
                ["Resource"] = decision.NormalizedResource,
                ["Recommendation"] = decision.Recommendation,
            });
        return decision;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityAuditEvent>> GetAuditEventsAsync(CancellationToken cancellationToken = default) =>
        _securityManager.GetSecurityAuditEventsAsync(cancellationToken);
}

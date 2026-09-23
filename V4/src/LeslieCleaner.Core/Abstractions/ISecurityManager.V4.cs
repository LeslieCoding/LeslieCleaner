using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>V4 permission and unified audit capabilities exposed by the security manager.</summary>
public interface ISecurityManagerV4
{
    /// <summary>Evaluates and audits a file, registry, process, or network permission request.</summary>
    Task<SecurityPermissionDecision> RequestPermissionAsync(SecurityPermissionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces explicit least-privilege grants. Empty means deny all.</summary>
    void ReplacePermissionGrants(IEnumerable<SecurityPermissionGrant> grants);

    /// <summary>Reads the unified tamper-evident security audit events.</summary>
    Task<IReadOnlyList<SecurityAuditEvent>> GetAuditEventsAsync(CancellationToken cancellationToken = default);
}

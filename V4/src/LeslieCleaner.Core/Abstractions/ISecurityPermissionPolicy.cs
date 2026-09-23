using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Default-deny, least-privilege policy for security permission requests.</summary>
public interface ISecurityPermissionPolicy
{
    /// <summary>Returns an authorization decision without performing the requested operation.</summary>
    SecurityPermissionDecision Evaluate(SecurityPermissionRequest request);

    /// <summary>Atomically replaces all explicit grants.</summary>
    void ReplaceGrants(IEnumerable<SecurityPermissionGrant> grants);
}

using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Thread-safe default-deny policy with exact subject, category, action, and resource grants.</summary>
public sealed class SecurityPermissionPolicy : ISecurityPermissionPolicy
{
    private readonly IProtectedPathPolicy _pathPolicy;
    private SecurityPermissionGrant[] _grants = [];

    /// <summary>Creates a default-deny policy.</summary>
    public SecurityPermissionPolicy(IProtectedPathPolicy pathPolicy) => _pathPolicy = pathPolicy;

    /// <inheritdoc />
    public void ReplaceGrants(IEnumerable<SecurityPermissionGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        SecurityPermissionGrant[] snapshot = grants.Select(ValidateGrant).ToArray();
        Volatile.Write(ref _grants, snapshot);
    }

    /// <inheritdoc />
    public SecurityPermissionDecision Evaluate(SecurityPermissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Resource) ||
            string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Denied(request.Resource ?? string.Empty, "Permission request fields must be explicit and non-empty.");
        }

        if (request.Category == SecurityPermissionCategory.Network && !IsValidNetworkResource(request.Resource))
        {
            return Denied(request.Resource.Trim(), "Network resources must be absolute HTTPS URIs without user information.");
        }

        string normalized = NormalizeResource(request.Category, request.Resource, out PathSecurityDecision? pathDecision);
        if (pathDecision is { Allowed: false })
        {
            return Denied(pathDecision.NormalizedPath ?? request.Resource, pathDecision.Reason);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        SecurityPermissionGrant? match = Volatile.Read(ref _grants).FirstOrDefault(grant =>
            (grant.ExpiresUtc is null || grant.ExpiresUtc > now) && Matches(grant, request, normalized));
        if (match is null)
        {
            return Denied(normalized, "No explicit least-privilege grant matches this request.");
        }

        return new SecurityPermissionDecision(true, normalized, "An active explicit grant matches this request.",
            SecurityRiskLevel.Safe, "Proceed only for the stated purpose.", now);
    }

    private SecurityPermissionGrant ValidateGrant(SecurityPermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (string.IsNullOrWhiteSpace(grant.Subject) || string.IsNullOrWhiteSpace(grant.Resource))
            throw new ArgumentException("Permission grants require a subject and resource.", nameof(grant));
        if (grant.Category == SecurityPermissionCategory.Network && !IsValidNetworkResource(grant.Resource))
            throw new ArgumentException("Network grant resources must be absolute HTTPS URIs without user information.", nameof(grant));
        string normalized = NormalizeResource(grant.Category, grant.Resource, out PathSecurityDecision? pathDecision);
        if (pathDecision is { Allowed: false }) throw new ArgumentException(pathDecision.Reason, nameof(grant));
        return grant with { Subject = grant.Subject.Trim(), Resource = normalized };
    }

    private string NormalizeResource(SecurityPermissionCategory category, string resource,
        out PathSecurityDecision? pathDecision)
    {
        pathDecision = null;
        if (category == SecurityPermissionCategory.File)
        {
            pathDecision = _pathPolicy.Evaluate(resource);
            return pathDecision.NormalizedPath ?? resource.Trim();
        }

        if (category == SecurityPermissionCategory.Network)
        {
            if (!Uri.TryCreate(resource, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
                return resource.Trim();
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
        }

        return resource.Trim();
    }

    private static bool IsValidNetworkResource(string resource) =>
        Uri.TryCreate(resource, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool Matches(SecurityPermissionGrant grant, SecurityPermissionRequest request, string normalized) =>
        string.Equals(grant.Subject, request.Subject.Trim(), StringComparison.OrdinalIgnoreCase) &&
        grant.Category == request.Category && grant.Action == request.Action &&
        (string.Equals(grant.Resource, normalized, StringComparison.OrdinalIgnoreCase) ||
         grant.IncludeDescendants && IsChildResource(grant, normalized));

    private static bool IsChildResource(SecurityPermissionGrant grant, string candidate)
    {
        char separator = grant.Category == SecurityPermissionCategory.File ? Path.DirectorySeparatorChar : '/';
        return candidate.StartsWith(grant.Resource.TrimEnd(separator) + separator, StringComparison.OrdinalIgnoreCase);
    }

    private static SecurityPermissionDecision Denied(string resource, string reason) => new(false, resource, reason,
        SecurityRiskLevel.Warning, "Request the smallest explicit grant required for this operation.", DateTimeOffset.UtcNow);
}

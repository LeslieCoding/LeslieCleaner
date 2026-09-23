using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Registers the V4 default-deny permission and protected-path policies.</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>Adds security policy services without replacing host overrides.</summary>
    public static IServiceCollection AddLeslieCleanerSecurityPolicies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IProtectedPathPolicy, ProtectedPathPolicy>();
        services.TryAddSingleton<ISecurityPermissionPolicy, SecurityPermissionPolicy>();
        return services;
    }
}

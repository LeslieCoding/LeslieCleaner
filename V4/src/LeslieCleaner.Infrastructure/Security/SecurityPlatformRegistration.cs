using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Creates a complete V4 security platform registration for hosts and tests.</summary>
public static class SecurityPlatformRegistration
{
    /// <summary>Replaces legacy security registrations with fail-safe V4 services.</summary>
    public static IServiceCollection AddLeslieCleanerSecurityPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLeslieCleanerSecurityPolicies();
        services.AddSingleton<SecurityAuditStore>();
        services.AddSingleton<ISecurityOperationGate, SecurityOperationGateV4>();
        services.AddSingleton<IEmergencySecurityGate>(static provider =>
            (IEmergencySecurityGate)provider.GetRequiredService<ISecurityOperationGate>());
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IEncryptionService, AesGcmEncryptionService>();
            services.TryAddSingleton(new IntegrityRepairOptions());
            services.TryAddSingleton<IIntegrityRepairService, IntegrityRepairService>();
            services.TryAddSingleton<IProtectedArtifactKeyRotationService, ProtectedArtifactKeyRotationService>();
        }
        services.AddSingleton<SecurityManager>();
        services.AddSingleton<AuditedSecurityManager>();
        services.AddSingleton<ISecurityManager>(static provider => provider.GetRequiredService<AuditedSecurityManager>());
        services.AddSingleton<ISecurityManagerV4>(static provider => provider.GetRequiredService<AuditedSecurityManager>());
        return services;
    }
}

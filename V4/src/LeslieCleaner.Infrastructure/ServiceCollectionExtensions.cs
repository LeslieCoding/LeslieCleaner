using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Infrastructure.Monitoring;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Infrastructure;

/// <summary>Provides infrastructure dependency injection registrations.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all infrastructure services.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddLeslieCleanerInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPathService, PathService>();
        services.TryAddSingleton(new LoggingOptions());
        services.TryAddSingleton<ILoggingBootstrapper, SerilogLoggingBootstrapper>();
        services.TryAddSingleton<ILogService>(static provider =>
            new SerilogLogService(provider.GetRequiredService<ILoggingBootstrapper>().Bootstrap()));
        services.TryAddSingleton<IConfigService>(static provider =>
        {
            var pathService = provider.GetRequiredService<IPathService>();
            string configFile = Path.Combine(
                pathService.EnsureDirectory(pathService.ConfigDirectory), "settings.json");
            return new JsonConfigService(
                configFile,
                provider.GetRequiredService<ILogService>(),
                provider.GetService<IEncryptionService>());
        });
        services.TryAddSingleton<IQuarantineService, QuarantineService>();
        services.TryAddSingleton<IFavoriteService, FavoriteService>();
        services.TryAddSingleton<IRecentService, RecentService>();
        services.TryAddSingleton<IFileIndexEngine, FileIndexEngine>();
        services.TryAddSingleton(new PerformanceMonitorOptions());
        services.TryAddSingleton<IPerformanceMetricCollector, ProcessPerformanceMetricCollector>();
        services.TryAddSingleton<IPerformanceMonitor, Monitoring.PerformanceMonitor>();
        services.TryAddSingleton<CrashLoopProtector>();
        services.TryAddSingleton<LogArchiveMaintenance>();
        services.TryAddSingleton<Diagnostics.DiagnosticsService>();
        services.TryAddSingleton<Updating.UpdateService>();
        services.TryAddSingleton<IUpdateManager, Updating.UpdateManager>();
        services.TryAddSingleton<IPluginStateStore, JsonPluginStateStore>();
        services.TryAddSingleton<IPluginManager, PluginManager>();
        services.AddLeslieCleanerSecurityPolicies();
        services.TryAddSingleton<SecurityAuditStore>();
        services.TryAddSingleton<ISecurityOperationGate, SecurityOperationGateV4>();
        services.TryAddSingleton<IEmergencySecurityGate>(p => (IEmergencySecurityGate)p.GetRequiredService<ISecurityOperationGate>());
        services.TryAddSingleton<SecurityManager>();
        services.TryAddSingleton<AuditedSecurityManager>();
        services.TryAddSingleton<ISecurityManager>(p => p.GetRequiredService<AuditedSecurityManager>());
        services.TryAddSingleton<ISecurityManagerV4>(p => p.GetRequiredService<AuditedSecurityManager>());
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IEncryptionService, AesGcmEncryptionService>();
            services.TryAddSingleton(new IntegrityRepairOptions());
            services.TryAddSingleton<IIntegrityRepairService, IntegrityRepairService>();
            services.TryAddSingleton<IProtectedArtifactKeyRotationService, ProtectedArtifactKeyRotationService>();
            services.TryAddSingleton<ICleanupTargetPolicy, WindowsCleanupTargetPolicy>();
            services.TryAddSingleton<IBackupService, RegistryBackupService>();
            services.TryAddSingleton<ISystemRestorePointService, SystemRestorePointService>();
            services.TryAddSingleton<ISystemRecycleBinService, SystemRecycleBinService>();
            services.TryAddSingleton<IRegistryEngine, RegistryEngine>();
            services.TryAddSingleton<IRegistryScanner, RegistryScanner>();
        }

        return services;
    }
}






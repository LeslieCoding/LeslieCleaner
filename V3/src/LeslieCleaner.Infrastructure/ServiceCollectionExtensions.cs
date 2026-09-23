using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Infrastructure;

/// <summary>LeslieCleaner.Infrastructure 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Infrastructure 层全部服务：日志（Serilog）、路径、隔离区、注册表备份、
    /// 系统还原点、配置热更新。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPathService, PathService>();
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
        services.TryAddSingleton<IFileIndexEngine, FileIndexEngine>();
        services.TryAddSingleton<Diagnostics.DiagnosticsService>();
        services.TryAddSingleton<Updating.UpdateService>();
        services.TryAddSingleton<IUpdateManager, Updating.UpdateManager>();
        services.TryAddSingleton<IPluginManager, PluginManager>();
        services.TryAddSingleton<ISecurityOperationGate, SecurityOperationGate>();
        services.TryAddSingleton<ISecurityManager, SecurityManager>();
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IEncryptionService, AesGcmEncryptionService>();
            services.TryAddSingleton<IBackupService, RegistryBackupService>();
            services.TryAddSingleton<ISystemRestorePointService, SystemRestorePointService>();
            services.TryAddSingleton<ISystemRecycleBinService, SystemRecycleBinService>();
            services.TryAddSingleton<IRegistryEngine, RegistryEngine>();
            services.TryAddSingleton<IRegistryScanner, RegistryScanner>();
        }

        return services;
    }
}

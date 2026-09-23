using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Data;

/// <summary>LeslieCleaner.Data 服务注册扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Data 层服务：SQLite 数据库（%ProgramData%\LeslieCleaner\Data\LeslieCleaner.db）、
    /// 文件索引仓储、残留规则导入器。数据库在首次使用前须调用
    /// <see cref="IDatabase.InitializeAsync"/>（幂等）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLeslieCleanerData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDatabase>(static provider =>
        {
            var pathService = provider.GetRequiredService<IPathService>();
            string databaseFile = Path.Combine(
                pathService.EnsureDirectory(pathService.DataDirectory), "LeslieCleaner.db");
            return new SqliteDatabase(databaseFile, provider.GetRequiredService<ILogService>());
        });
        services.TryAddSingleton<FileIndexRepository>();
        services.TryAddSingleton<ResidualRuleImporter>();

        return services;
    }
}

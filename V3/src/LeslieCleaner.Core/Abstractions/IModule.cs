namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 功能模块统一契约。所有业务模块（Cleaner/Registry/Uninstaller/Browser/Privacy/Startup/Shredder/AI）
/// 均通过该接口接入宿主，实现模块间解耦与插件化扩展。
/// </summary>
public interface IModule
{
    /// <summary>获取模块元数据（标识、名称、版本、描述等）。</summary>
    Models.ModuleInfo Info { get; }

    /// <summary>
    /// 初始化模块。宿主在模块被使用前调用一次，实现方应在此完成惰性资源准备。
    /// </summary>
    /// <param name="cancellationToken">取消令牌，初始化必须可取消。</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行模块健康检查，供系统健康评分与自检使用。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模块健康状态。</returns>
    Task<Models.ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}

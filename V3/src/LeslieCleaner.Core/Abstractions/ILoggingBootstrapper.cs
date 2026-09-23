namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// Serilog 配置集中引导契约。由 Infrastructure 实现，
/// 统一配置按天滚动文件 Sink 与异步写入，App 启动时调用一次。
/// </summary>
public interface ILoggingBootstrapper
{
    /// <summary>
    /// 构建并返回配置完成的 Serilog 日志器（同时设为 <see cref="Serilog.Log.Logger"/>）。
    /// </summary>
    /// <returns>进程级根日志器。</returns>
    Serilog.ILogger Bootstrap();
}

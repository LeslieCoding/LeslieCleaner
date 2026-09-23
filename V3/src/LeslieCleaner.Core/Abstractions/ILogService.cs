namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 结构化日志服务契约。统一记录开发圣经第六节定义的九类事件，
/// 由 Infrastructure 基于 Serilog 实现（按天滚动文件 + 异步 Sink）。
/// </summary>
public interface ILogService
{
    /// <summary>
    /// 写入一条结构化事件日志。
    /// </summary>
    /// <param name="kind">事件类别（ScanStart/DeleteFinish/Exception 等九类之一）。</param>
    /// <param name="message">事件描述。</param>
    /// <param name="properties">结构化属性，键名建议使用 PascalCase；可为 <see langword="null"/>。</param>
    void LogEvent(Models.LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>
    /// 记录异常（事件类别固定为 <see cref="Models.LogEventKind.Exception"/>）。所有被捕获的异常必须经此方法留痕。
    /// </summary>
    /// <param name="exception">异常实例。</param>
    /// <param name="context">异常发生的上下文描述（模块/操作）。</param>
    /// <param name="properties">结构化属性，可为 <see langword="null"/>。</param>
    void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null);
}

using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Serilog;
using Serilog.Events;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>
/// 基于 Serilog 的 <see cref="ILogService"/> 实现。
/// 事件类别以结构化属性 EventKind 写入，便于检索与聚合。
/// </summary>
public sealed class SerilogLogService : ILogService
{
    private readonly Serilog.ILogger _logger;

    /// <summary>以 Serilog 日志器创建服务。</summary>
    /// <param name="logger">已配置的 Serilog 日志器。</param>
    public SerilogLogService(Serilog.ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public void LogEvent(LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Serilog.ILogger contextual = BuildContext(kind, properties);
        contextual.Write(ToLevel(kind), "{Message}", message);
    }

    /// <inheritdoc />
    public void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);
        Serilog.ILogger contextual = BuildContext(LogEventKind.Exception, properties);
        contextual.Error(exception, "[{Context}] 异常", context);
    }

    private Serilog.ILogger BuildContext(LogEventKind kind, IReadOnlyDictionary<string, object?>? properties)
    {
        Serilog.ILogger contextual = _logger.ForContext("EventKind", kind.ToString());
        if (properties is not null)
        {
            foreach (KeyValuePair<string, object?> pair in properties)
            {
                contextual = contextual.ForContext(pair.Key, pair.Value, destructureObjects: true);
            }
        }

        return contextual;
    }

    private static LogEventLevel ToLevel(LogEventKind kind) => kind switch
    {
        LogEventKind.Exception => LogEventLevel.Error,
        LogEventKind.DeleteStart or LogEventKind.DeleteFinish => LogEventLevel.Warning,
        _ => LogEventLevel.Information,
    };
}

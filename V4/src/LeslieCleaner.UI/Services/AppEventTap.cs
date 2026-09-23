using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.UI.Services;

/// <summary>
/// 日志事件分接头（ILogService 装饰器）：转发全部写日志调用到内层实现，
/// 同时以事件形式广播事件流，供 History Center / Notification Center 订阅
/// （"历史记录来源：既有日志事件流"，零业务逻辑侵入）。
/// </summary>
public sealed class AppEventTap : ILogService
{
    private readonly ILogService _inner;

    /// <summary>以内层日志服务创建分接头。</summary>
    /// <param name="inner">被包装的真实日志服务。</param>
    public AppEventTap(ILogService inner) => _inner = inner;

    /// <summary>事件流广播（触发线程与调用方一致，订阅方自行封送 UI 线程）。</summary>
    public event Action<LogEventKind, string, IReadOnlyDictionary<string, object?>?>? EventEmitted;

    /// <inheritdoc />
    public void LogEvent(LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        _inner.LogEvent(kind, message, properties);
        EventEmitted?.Invoke(kind, message, properties);
    }

    /// <inheritdoc />
    public void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null)
    {
        _inner.LogException(exception, context, properties);
        EventEmitted?.Invoke(LogEventKind.Exception, $"{context}: {exception.Message}", properties);
    }
}

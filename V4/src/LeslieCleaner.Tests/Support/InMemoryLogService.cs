using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Tests.Support;

/// <summary>内存日志服务：捕获事件供断言，同时满足"异常必须记日志"的依赖注入要求。</summary>
public sealed class InMemoryLogService : ILogService
{
    private readonly List<(LogEventKind Kind, string Message)> _events = [];
    private readonly List<Exception> _exceptions = [];
    private readonly object _gate = new();

    public IReadOnlyList<(LogEventKind Kind, string Message)> Events
    {
        get { lock (_gate) { return _events.ToArray(); } }
    }

    public IReadOnlyList<Exception> Exceptions
    {
        get { lock (_gate) { return _exceptions.ToArray(); } }
    }

    public void LogEvent(LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        lock (_gate)
        {
            _events.Add((kind, message));
        }
    }

    public void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null)
    {
        lock (_gate)
        {
            _exceptions.Add(exception);
            _events.Add((LogEventKind.Exception, $"{context}: {exception.Message}"));
        }
    }

    public bool HasEvent(LogEventKind kind) => Events.Any(e => e.Kind == kind);
}

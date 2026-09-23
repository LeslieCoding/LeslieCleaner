using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>
/// 全局异常处理工具。App 启动时调用 <see cref="Attach"/> 挂钩
/// 未处理异常与未观察任务异常，统一记录日志，避免静默崩溃。
/// </summary>
public static class GlobalExceptionHandler
{
    /// <summary>
    /// 挂钩进程级未处理异常（AppDomain）与未观察任务异常（TaskScheduler）。
    /// 幂等：重复调用不会重复挂钩。
    /// </summary>
    /// <param name="logService">日志服务。</param>
    public static void Attach(ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(logService);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Handle(exception, "AppDomain.UnhandledException", logService);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Handle(args.Exception, "TaskScheduler.UnobservedTaskException", logService);
            args.SetObserved();
        };
    }

    /// <summary>
    /// 记录一个异常（事件类别 Exception）。供 catch 块统一调用。
    /// </summary>
    /// <param name="exception">异常实例。</param>
    /// <param name="context">上下文描述。</param>
    /// <param name="logService">日志服务。</param>
    public static void Handle(Exception exception, string context, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logService);
        logService.LogException(exception, context);
    }

    /// <summary>
    /// 观察一个 "即发即弃" 任务：任何异常都会被记录而不是被吞掉。
    /// </summary>
    /// <param name="task">待观察任务。</param>
    /// <param name="context">上下文描述。</param>
    /// <param name="logService">日志服务。</param>
    public static void Observe(Task task, string context, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(logService);
        task.ContinueWith(
            t => Handle(t.Exception is { } fault ? fault : new InvalidOperationException("未知任务异常"), context, logService),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 构造用于日志的 UserAction 事件属性字典。
    /// </summary>
    /// <param name="action">用户操作名称。</param>
    /// <param name="target">操作目标，可为 <see langword="null"/>。</param>
    public static IReadOnlyDictionary<string, object?> UserActionProperties(string action, string? target = null) =>
        new Dictionary<string, object?> { ["Action"] = action, ["Target"] = target };

    /// <summary>
    /// 快捷记录一次用户操作（事件类别 UserAction）。
    /// </summary>
    /// <param name="logService">日志服务。</param>
    /// <param name="action">用户操作名称。</param>
    /// <param name="target">操作目标，可为 <see langword="null"/>。</param>
    public static void LogUserAction(ILogService logService, string action, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(logService);
        logService.LogEvent(LogEventKind.UserAction, $"用户操作: {action}", UserActionProperties(action, target));
    }
}

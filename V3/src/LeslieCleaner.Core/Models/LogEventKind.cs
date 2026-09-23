namespace LeslieCleaner.Core.Models;

/// <summary>日志事件类别（开发圣经第六节规定的九类必记事件）。</summary>
public enum LogEventKind
{
    /// <summary>扫描开始。</summary>
    ScanStart,

    /// <summary>扫描完成。</summary>
    ScanFinish,

    /// <summary>删除开始。</summary>
    DeleteStart,

    /// <summary>删除完成。</summary>
    DeleteFinish,

    /// <summary>还原开始。</summary>
    RestoreStart,

    /// <summary>还原完成。</summary>
    RestoreFinish,

    /// <summary>异常。</summary>
    Exception,

    /// <summary>性能指标。</summary>
    PerformanceMetrics,

    /// <summary>用户操作。</summary>
    UserAction,
}

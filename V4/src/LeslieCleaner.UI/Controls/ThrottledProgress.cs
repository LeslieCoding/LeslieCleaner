namespace LeslieCleaner.UI.Controls;

/// <summary>
/// 节流进度回调包装器：高频引擎进度合并为最小间隔一次（默认 100ms），
/// 防止 UI 高频重绘；最后一次值始终透传（完成态不丢）。
/// </summary>
/// <typeparam name="T">进度类型。</typeparam>
public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minInterval;
    private readonly object _gate = new();
    private long _lastReportTicks;
    private T? _pending;

    /// <summary>以内部回调与最小间隔创建节流包装。</summary>
    /// <param name="inner">实际进度回调。</param>
    /// <param name="minInterval">最小报告间隔（默认 100ms）。</param>
    public ThrottledProgress(IProgress<T> inner, TimeSpan? minInterval = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _minInterval = minInterval ?? TimeSpan.FromMilliseconds(100);
    }

    /// <inheritdoc />
    public void Report(T value)
    {
        bool pass = false;
        lock (_gate)
        {
            long now = Environment.TickCount64;
            if (now - _lastReportTicks >= _minInterval.TotalMilliseconds || _lastReportTicks == 0)
            {
                _lastReportTicks = now;
                _pending = default;
                pass = true;
            }
            else
            {
                _pending = value;
            }
        }

        if (pass)
        {
            _inner.Report(value);
        }
    }

    /// <summary>冲刷挂起的最后值（完成时调用，保证最终态到达 UI）。</summary>
    public void Flush()
    {
        T? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = default;
        }

        if (pending is not null)
        {
            _inner.Report(pending);
        }
    }
}

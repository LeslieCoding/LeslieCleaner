namespace LeslieCleaner.Core.Threading;

/// <summary>
/// 暂停/继续原语。与 <see cref="CancellationTokenSource"/> 配对使用，
/// 为扫描/清理循环提供 Pause/Resume 能力（圣经线程架构要求）。
/// 线程安全，可重复使用。
/// </summary>
public sealed class PauseTokenSource
{
    private volatile TaskCompletionSource _pausedGate = CreateCompletedGate();

    private static TaskCompletionSource CreateCompletedGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }

    /// <summary>获取当前是否处于暂停状态。</summary>
    public bool IsPaused => !_pausedGate.Task.IsCompleted;

    /// <summary>获取与当前状态对应的令牌。</summary>
    public PauseToken Token => new(this);

    /// <summary>进入暂停状态；已在执行的迭代完成后挂起。</summary>
    public void Pause()
    {
        if (_pausedGate.Task.IsCompleted)
        {
            Interlocked.Exchange(ref _pausedGate, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }

    /// <summary>解除暂停，放行所有等待者。</summary>
    public void Resume() => _pausedGate.TrySetResult();

    internal Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        Task gate = _pausedGate.Task;
        return gate.IsCompleted ? Task.CompletedTask : gate.WaitAsync(cancellationToken);
    }
}

/// <summary><see cref="PauseTokenSource"/> 的轻量令牌快照。</summary>
/// <param name="source">来源；为 <see langword="null"/> 表示永不暂停。</param>
public readonly struct PauseToken(PauseTokenSource? source)
{
    /// <summary>永不暂停的默认令牌。</summary>
    public static PauseToken None => new(null);

    /// <summary>
    /// 若处于暂停状态则异步等待直至继续；同时响应取消令牌。
    /// 应在每个工作迭代开始时调用。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task WaitWhilePausedAsync(CancellationToken cancellationToken = default) =>
        source?.WaitWhilePausedAsync(cancellationToken) ?? Task.CompletedTask;
}

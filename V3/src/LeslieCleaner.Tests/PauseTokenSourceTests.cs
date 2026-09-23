using LeslieCleaner.Core.Threading;

namespace LeslieCleaner.Tests;

public sealed class PauseTokenSourceTests
{
    [Fact]
    public async Task Pause_BlocksUntilResume()
    {
        var source = new PauseTokenSource();
        source.Pause();

        Task wait = source.Token.WaitWhilePausedAsync();
        var completed = await Task.WhenAny(wait, Task.Delay(200));
        Assert.NotSame(wait, completed);
        Assert.True(source.IsPaused);

        source.Resume();
        await wait;
        Assert.False(source.IsPaused);
    }

    [Fact]
    public async Task Resumed_WaitsCompleteImmediately()
    {
        var source = new PauseTokenSource();

        await source.Token.WaitWhilePausedAsync();
        await PauseToken.None.WaitWhilePausedAsync();
    }

    [Fact]
    public async Task Paused_HonorsCancellation()
    {
        var source = new PauseTokenSource();
        source.Pause();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.Token.WaitWhilePausedAsync(cts.Token));
    }
}

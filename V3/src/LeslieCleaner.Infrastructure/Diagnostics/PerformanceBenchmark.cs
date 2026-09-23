using System.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Diagnostics;

/// <summary>
/// 性能度量工具。对操作计时并产出 <see cref="BenchmarkResult"/>，
/// 结果由调用方以 PerformanceMetrics 事件写入日志。
/// </summary>
public static class PerformanceBenchmark
{
    /// <summary>
    /// 度量一个异步操作。计时覆盖 <paramref name="action"/> 的完整执行；
    /// <paramref name="iterations"/> 仅声明该操作内部处理的工作量，用于计算均值。
    /// </summary>
    /// <param name="name">度量名称。</param>
    /// <param name="iterations">操作内部处理的工作量（如文件数、行数）。</param>
    /// <param name="action">被度量的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>度量结果。</returns>
    public static async Task<BenchmarkResult> MeasureAsync(
        string name,
        long iterations,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegative(iterations);

        long startTimestamp = Stopwatch.GetTimestamp();
        await action(cancellationToken).ConfigureAwait(false);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        return new BenchmarkResult(name, iterations, elapsed, DateTimeOffset.UtcNow);
    }
}

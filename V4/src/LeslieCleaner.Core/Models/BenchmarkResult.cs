namespace LeslieCleaner.Core.Models;

/// <summary>性能度量结果（日志事件类别 PerformanceMetrics 的载荷）。</summary>
/// <param name="Name">度量名称。</param>
/// <param name="Iterations">迭代次数。</param>
/// <param name="TotalDuration">总耗时。</param>
/// <param name="MeasuredUtc">度量时间（UTC）。</param>
public sealed record BenchmarkResult(string Name, long Iterations, TimeSpan TotalDuration, DateTimeOffset MeasuredUtc)
{
    /// <summary>单次迭代平均耗时。</summary>
    public TimeSpan AveragePerIteration =>
        Iterations > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / Iterations) : TimeSpan.Zero;
}

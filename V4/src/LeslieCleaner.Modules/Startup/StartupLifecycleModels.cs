using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Startup.Classification;

namespace LeslieCleaner.Modules.Startup;

/// <summary>启动性能数据的可信度。</summary>
public enum StartupDataQuality
{
    /// <summary>由受控启发式规则估算，不能当作真实测量。</summary>
    Estimated,

    /// <summary>由外部测量源提供。</summary>
    Measured,

    /// <summary>当前没有可用数据。</summary>
    Unavailable,
}

/// <summary>启动扫描请求。</summary>
/// <param name="ForceRefresh">是否跳过七日缓存并显式刷新。</param>
public sealed record StartupScanRequest(bool ForceRefresh = false);

/// <summary>启动扫描快照。</summary>
/// <param name="Items">启动项。</param>
/// <param name="CapturedUtc">快照生成时间。</param>
/// <param name="ExpiresUtc">快照过期时间。</param>
/// <param name="FromCache">本次是否直接使用缓存。</param>
public sealed record StartupScanSnapshot(
    IReadOnlyList<StartupItem> Items,
    DateTimeOffset CapturedUtc,
    DateTimeOffset ExpiresUtc,
    bool FromCache);

/// <summary>延迟启动允许的固定时长。</summary>
public enum StartupDelayOption
{
    /// <summary>30 秒。</summary>
    Seconds30 = 30,

    /// <summary>60 秒。</summary>
    Seconds60 = 60,

    /// <summary>120 秒。</summary>
    Seconds120 = 120,

    /// <summary>300 秒。</summary>
    Seconds300 = 300,
}

/// <summary>延迟启动请求。</summary>
/// <param name="Detail">含分类和保护级别的启动项详情。</param>
/// <param name="Delay">延迟时长。</param>
/// <param name="UserConfirmed">用户是否已明确确认。</param>
/// <param name="Timeout">创建任务的超时；默认 30 秒。</param>
public sealed record DelayedStartupRequest(
    StartupItemDetail Detail,
    StartupDelayOption Delay,
    bool UserConfirmed,
    TimeSpan? Timeout = null);

/// <summary>交给受控任务执行器的规范化任务。</summary>
/// <param name="TaskName">程序生成的稳定任务名。</param>
/// <param name="ExecutablePath">规范化后的绝对可执行文件路径。</param>
/// <param name="Arguments">不经 shell 展开的参数文本。</param>
/// <param name="Delay">登录后延迟。</param>
/// <param name="Description">任务说明。</param>
public sealed record DelayedStartupTaskSpec(
    string TaskName,
    string ExecutablePath,
    string Arguments,
    TimeSpan Delay,
    string Description);

/// <summary>延迟启动操作结果。</summary>
/// <param name="ItemId">启动项标识。</param>
/// <param name="Success">是否成功。</param>
/// <param name="AlreadyConfigured">是否为幂等命中。</param>
/// <param name="OriginalEntryRestored">失败时原入口是否已自动恢复。</param>
/// <param name="Error">失败原因。</param>
public sealed record DelayedStartupResult(
    string ItemId,
    bool Success,
    bool AlreadyConfigured,
    bool OriginalEntryRestored,
    string? Error);

/// <summary>已持久化的受控延迟启动记录。</summary>
/// <param name="ItemId">原启动项标识。</param>
/// <param name="Name">显示名。</param>
/// <param name="Task">受控任务规范。</param>
/// <param name="CreatedUtc">创建时间。</param>
/// <param name="OriginalItem">用于恢复后真实状态复查的原启动项快照。</param>
public sealed record DelayedStartupRecord(
    string ItemId,
    string Name,
    DelayedStartupTaskSpec Task,
    DateTimeOffset CreatedUtc,
    StartupItem? OriginalItem = null);

/// <summary>启动项生命周期操作类型。</summary>
public enum StartupOperationKind
{
    /// <summary>实际扫描。</summary>
    Scan,

    /// <summary>缓存命中。</summary>
    CachedScan,

    /// <summary>配置延迟启动。</summary>
    Delay,

    /// <summary>恢复延迟启动。</summary>
    RestoreDelay,

    /// <summary>缓存失效。</summary>
    CacheInvalidated,
}

/// <summary>一条启动管理操作记录。</summary>
/// <param name="TimestampUtc">记录时间。</param>
/// <param name="Kind">操作类型。</param>
/// <param name="ItemId">关联启动项；扫描等全局操作为 <see langword="null"/>。</param>
/// <param name="Succeeded">是否成功。</param>
/// <param name="Message">不含命令行和私人路径的摘要。</param>
public sealed record StartupOperationRecord(
    DateTimeOffset TimestampUtc,
    StartupOperationKind Kind,
    string? ItemId,
    bool Succeeded,
    string Message);

/// <summary>一次外部提供的开机性能观测。</summary>
/// <param name="ObservedUtc">观测时间。</param>
/// <param name="BootDuration">开机时长；不可用时为 <see langword="null"/>。</param>
/// <param name="Quality">数据可信度。</param>
/// <param name="Source">数据来源说明。</param>
public sealed record StartupBootObservation(
    DateTimeOffset ObservedUtc,
    TimeSpan? BootDuration,
    StartupDataQuality Quality,
    string Source);

/// <summary>开机趋势摘要。</summary>
/// <param name="Points">按时间升序的真实/估计观测。</param>
/// <param name="Quality">整体数据可信度。</param>
/// <param name="Change">首末有效观测的时长变化；不可计算时为 <see langword="null"/>。</param>
/// <param name="Explanation">明确说明测量、估计或不可用状态。</param>
public sealed record StartupTrend(
    IReadOnlyList<StartupBootObservation> Points,
    StartupDataQuality Quality,
    TimeSpan? Change,
    string Explanation);

/// <summary>可导出的启动优化报告。</summary>
/// <param name="GeneratedUtc">生成时间。</param>
/// <param name="CurrentItemCount">最近扫描项数；无快照时为 0。</param>
/// <param name="DisabledItemCount">当前由模块备份管理的禁用项数。</param>
/// <param name="DelayedItemCount">受控延迟项数。</param>
/// <param name="EstimatedImpactScoreBefore">基于现有影响评级的估算分。</param>
/// <param name="EstimatedImpactScoreAfter">应用当前禁用/延迟状态后的估算分。</param>
/// <param name="ImpactQuality">影响分数据可信度，当前规则引擎只能是 Estimated 或 Unavailable。</param>
/// <param name="BootTrend">开机趋势。</param>
/// <param name="RecentOperations">近期操作。</param>
public sealed record StartupOptimizationReport(
    DateTimeOffset GeneratedUtc,
    int CurrentItemCount,
    int DisabledItemCount,
    int DelayedItemCount,
    double? EstimatedImpactScoreBefore,
    double? EstimatedImpactScoreAfter,
    StartupDataQuality ImpactQuality,
    StartupTrend BootTrend,
    IReadOnlyList<StartupOperationRecord> RecentOperations);

/// <summary>启动项的最小操作契约，供生命周期编排与测试替身使用。</summary>
public interface IStartupEntryOperations
{
    /// <summary>执行一次真实扫描。</summary>
    Task<IReadOnlyList<StartupItem>> EnumerateAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>以可恢复方式禁用原启动入口。</summary>
    Task<StartupChangeResult> DisableAsync(StartupItem item, CancellationToken cancellationToken = default);

    /// <summary>从备份恢复原启动入口。</summary>
    Task<StartupChangeResult> EnableAsync(string itemId, CancellationToken cancellationToken = default);
}

/// <summary>受控延迟任务执行器；实现不得通过 shell 解释用户输入。</summary>
public interface IDelayedStartupTaskController
{
    /// <summary>创建或更新受控任务。</summary>
    Task CreateAsync(DelayedStartupTaskSpec task, CancellationToken cancellationToken);

    /// <summary>删除受控任务；不存在视为成功。</summary>
    Task DeleteAsync(string taskName, CancellationToken cancellationToken);

    /// <summary>验证受控任务是否存在。</summary>
    Task<bool> ExistsAsync(string taskName, CancellationToken cancellationToken);
}

/// <summary>可测试时钟。</summary>
public interface IStartupClock
{
    /// <summary>当前 UTC 时间。</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>系统 UTC 时钟。</summary>
public sealed class SystemStartupClock : IStartupClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

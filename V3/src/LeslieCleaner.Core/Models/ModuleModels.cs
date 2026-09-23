namespace LeslieCleaner.Core.Models;

/// <summary>功能模块元数据。</summary>
/// <param name="Id">模块唯一标识（建议反向域名风格，如 "lesliecleaner.cleaner"）。</param>
/// <param name="Name">模块显示名称。</param>
/// <param name="Version">模块版本。</param>
/// <param name="Description">模块功能描述。</param>
public sealed record ModuleInfo(string Id, string Name, Version Version, string Description);

/// <summary>模块健康状态。</summary>
public enum ModuleStatus
{
    /// <summary>健康。</summary>
    Healthy,

    /// <summary>降级（可用但部分能力受损）。</summary>
    Degraded,

    /// <summary>不可用。</summary>
    Unhealthy,
}

/// <summary>模块健康检查结果。</summary>
/// <param name="ModuleId">模块标识。</param>
/// <param name="Status">健康状态。</param>
/// <param name="Message">附加说明，可为 <see langword="null"/>。</param>
/// <param name="CheckedUtc">检查时间（UTC）。</param>
public sealed record ModuleHealth(string ModuleId, ModuleStatus Status, string? Message, DateTimeOffset CheckedUtc);

/// <summary>系统健康评分（AI Optimizer 输出，0-100）。</summary>
/// <param name="Value">分值，0 最差，100 最佳。</param>
/// <param name="Summary">评分摘要说明。</param>
/// <param name="Factors">扣分因素列表。</param>
/// <param name="EvaluatedUtc">评估时间（UTC）。</param>
public sealed record HealthScore(
    int Value,
    string Summary,
    IReadOnlyList<string> Factors,
    DateTimeOffset EvaluatedUtc);

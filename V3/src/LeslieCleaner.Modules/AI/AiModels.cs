namespace LeslieCleaner.Modules.AI;

/// <summary>
/// 系统健康指标（全部来自真实扫描/系统数据，禁止伪数据）。
/// </summary>
/// <param name="JunkBytes">Smart Scan 可清理垃圾总量（字节）。</param>
/// <param name="HighImpactStartupCount">高影响启动项数量。</param>
/// <param name="RegistryIssueCount">注册表问题项数量。</param>
/// <param name="DiskFreePercent">系统盘剩余空间百分比（0-100）。</param>
/// <param name="PrivacyItemCount">隐私项数量。</param>
/// <param name="TopLargeFileBytes">最大单文件字节数。</param>
public sealed record SystemMetrics(
    long JunkBytes,
    int HighImpactStartupCount,
    int RegistryIssueCount,
    double DiskFreePercent,
    int PrivacyItemCount,
    long TopLargeFileBytes);

/// <summary>自动维护计划（配置驱动）。</summary>
/// <param name="Name">计划名。</param>
/// <param name="IntervalDays">间隔天数。</param>
/// <param name="ModuleIds">执行的模块标识集合。</param>
/// <param name="Enabled">是否启用。</param>
/// <param name="LastRunUtc">上次执行时间；从未执行为 <see langword="null"/>。</param>
public sealed record MaintenancePlan(
    string Name,
    int IntervalDays,
    IReadOnlyList<string> ModuleIds,
    bool Enabled,
    DateTimeOffset? LastRunUtc);

/// <summary>AI 分析报告。</summary>
/// <param name="Score">健康评分（0-100）。</param>
/// <param name="StartupSuggestions">启动优化建议。</param>
/// <param name="PrivacySuggestions">隐私风险分析。</param>
/// <param name="StorageSuggestions">存储优化建议。</param>
public sealed record AiAnalysisReport(
    int Score,
    IReadOnlyList<string> StartupSuggestions,
    IReadOnlyList<string> PrivacySuggestions,
    IReadOnlyList<string> StorageSuggestions);

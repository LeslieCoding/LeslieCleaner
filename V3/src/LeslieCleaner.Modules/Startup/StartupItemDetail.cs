using LeslieCleaner.Modules.Startup.Classification;

namespace LeslieCleaner.Modules.Startup;

/// <summary>
/// 启动项完整详情（Startup Manager v2 数据模型）。
/// 影响估计与 CPU/磁盘/内存数值均为启发式估计值（UI 需标注）。
/// </summary>
/// <param name="Item">基础启动项。</param>
/// <param name="FilePath">可执行文件路径（无文件项为 <see langword="null"/>）。</param>
/// <param name="FileSizeBytes">文件大小（无文件为 0）。</param>
/// <param name="Signature">签名验证结果。</param>
/// <param name="Classification">来源分类。</param>
/// <param name="Impact">影响评估（估计值）。</param>
/// <param name="Advice">AI 建议（引用真实依据）。</param>
/// <param name="Sha256">文件 SHA256（无文件/计算失败为 <see langword="null"/>）。</param>
/// <param name="InstallDate">安装日期（注册表来源项）。</param>
/// <param name="ModifiedUtc">文件最后修改时间。</param>
/// <param name="Protection">禁用保护级别。</param>
public sealed record StartupItemDetail(
    StartupItem Item,
    string? FilePath,
    long FileSizeBytes,
    SignatureInfo Signature,
    StartupClassification Classification,
    StartupImpactEstimate Impact,
    string Advice,
    string? Sha256,
    DateOnly? InstallDate,
    DateTimeOffset? ModifiedUtc,
    DisableProtection Protection);

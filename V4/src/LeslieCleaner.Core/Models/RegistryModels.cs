namespace LeslieCleaner.Core.Models;

/// <summary>注册表无效项类别。</summary>
public enum RegistryIssueKind
{
    /// <summary>无效卸载项（DisplayIcon/InstallLocation 指向不存在路径）。</summary>
    InvalidUninstallEntry,

    /// <summary>无效扩展名（指向不存在的 ProgID）。</summary>
    InvalidFileExtension,

    /// <summary>无效 COM/CLSID（InprocServer32 指向不存在文件）。</summary>
    InvalidComServer,

    /// <summary>失效字体引用（字体文件不存在）。</summary>
    InvalidFontReference,

    /// <summary>失效路径值（值内容指向不存在路径）。</summary>
    InvalidPathValue,
}

/// <summary>注册表项五级风险评级。</summary>
public enum RegistryRiskLevel
{
    /// <summary>明确的低影响残留，可提供一键修复。</summary>
    Safe,

    /// <summary>低风险，修复前需要标准确认。</summary>
    Low,

    /// <summary>中等风险，需展示详细原因后确认。</summary>
    Medium,

    /// <summary>中等风险的旧名，仅保留源代码兼容。</summary>
        Moderate = Medium,

    /// <summary>高风险，默认禁用，仅高级确认后可修复。</summary>
    High,

    /// <summary>高风险的旧名，仅保留源代码兼容。</summary>
        Risky = High,

    /// <summary>系统关键或受保护目标，禁止修复。</summary>
    Critical,
}

/// <summary>注册表修复前的最低用户确认级别。</summary>
public enum RegistryConfirmationRequirement
{
    /// <summary>允许一键修复。</summary>
    None,
    /// <summary>标准确认。</summary>
    Standard,
    /// <summary>展示风险详情后确认。</summary>
    Detailed,
    /// <summary>高级确认，不得默认选中。</summary>
    Advanced,
    /// <summary>禁止修复。</summary>
    Forbidden,
}

/// <summary>支持误报控制的多源证据。</summary>
public sealed record RegistryDetectionEvidence(
    bool PathExists = false,
    bool ApplicationInstalled = false,
    bool SignatureValid = false,
    bool PublisherMatched = false,
    bool RecentlyUsed = false,
    string RegistryView = "Unknown")
{
    /// <summary>任一关联证据表明应保留该项。</summary>
    public bool RequiresRetention =>
        PathExists || ApplicationInstalled || SignatureValid || PublisherMatched || RecentlyUsed;
}

/// <summary>一条注册表扫描命中项。</summary>
/// <param name="KeyPath">完整注册表键路径。</param>
/// <param name="ValueName">相关值名；键级问题为 <see langword="null"/>。</param>
/// <param name="Issue">问题类别。</param>
/// <param name="Risk">风险评级。</param>
/// <param name="Description">人类可读描述。</param>
/// <param name="BrokenTarget">失效目标（缺失的文件/ProgID），可为 <see langword="null"/>。</param>
public sealed record RegistryFinding(
    string KeyPath,
    string? ValueName,
    RegistryIssueKind Issue,
    RegistryRiskLevel Risk,
    string Description,
    string? BrokenTarget)
{
    /// <summary>0–100 数值风险分，分数越高风险越高。</summary>
    public int RiskScore { get; init; } = Risk switch
    {
        RegistryRiskLevel.Safe => 10,
        RegistryRiskLevel.Low => 30,
        RegistryRiskLevel.Medium => 50,
        RegistryRiskLevel.High => 75,
        _ => 100,
    };

    /// <summary>风险分的可读解释。</summary>
    public string RiskExplanation { get; init; } = Description;

    /// <summary>建议操作。</summary>
    public string Recommendation { get; init; } = "保留备份并按风险级别确认后处理";

    /// <summary>多源确认与误报控制证据。</summary>
    public RegistryDetectionEvidence Evidence { get; init; } = new();

    /// <summary>是否命中 WRP/TrustedInstaller 或显式系统保护区。</summary>
    public bool IsSystemProtected { get; init; }

    /// <summary>系统保护区和 Critical 项恒不可修复。</summary>
    public bool CanRepair => !IsSystemProtected && Risk != RegistryRiskLevel.Critical;

    /// <summary>仅 Safe 项允许一键修复。</summary>
    public bool CanRepairAutomatically => CanRepair && Risk == RegistryRiskLevel.Safe;

    /// <summary>强制确认契约。</summary>
    public RegistryConfirmationRequirement ConfirmationRequirement => IsSystemProtected
        ? RegistryConfirmationRequirement.Forbidden
        : Risk switch
        {
            RegistryRiskLevel.Safe => RegistryConfirmationRequirement.None,
            RegistryRiskLevel.Low => RegistryConfirmationRequirement.Standard,
            RegistryRiskLevel.Medium => RegistryConfirmationRequirement.Detailed,
            RegistryRiskLevel.High => RegistryConfirmationRequirement.Advanced,
            _ => RegistryConfirmationRequirement.Forbidden,
        };
}

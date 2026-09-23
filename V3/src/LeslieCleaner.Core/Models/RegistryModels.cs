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

/// <summary>注册表项风险评级（Safe 默认勾选，Moderate 提示，Risky 默认不勾选）。</summary>
public enum RegistryRiskLevel
{
    /// <summary>指向用户目录下的缺失文件，删除安全。</summary>
    Safe,

    /// <summary>指向系统路径（Program Files/Windows）下的缺失文件，需谨慎。</summary>
    Moderate,

    /// <summary>无法判定，默认不勾选。</summary>
    Risky,
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
    string? BrokenTarget);

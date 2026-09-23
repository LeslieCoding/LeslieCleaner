namespace LeslieCleaner.Core.Models;

/// <summary>残留命中类型。</summary>
public enum ResidualKind
{
    /// <summary>文件。</summary>
    File,

    /// <summary>目录。</summary>
    Directory,

    /// <summary>注册表键。</summary>
    RegistryKey,
}

/// <summary>残留来源阶段（Multi-Stage Residual Pipeline）。</summary>
public enum ResidualSourceStage
{
    /// <summary>Stage 1：安装数据库定位。</summary>
    InstallDatabase,

    /// <summary>Stage 2：已知规则库定位。</summary>
    KnownRules,

    /// <summary>Stage 3：安装日志定位。</summary>
    InstallLogs,

    /// <summary>Stage 4：Publisher 索引定位。</summary>
    PublisherIndex,

    /// <summary>Stage 5：受限深度扫描。</summary>
    DeepScan,
}

/// <summary>卸载残留查询条件。</summary>
/// <param name="Publisher">发布者名称，可为 <see langword="null"/>。</param>
/// <param name="ProductName">产品名，可为 <see langword="null"/>。</param>
/// <param name="InstallLocation">原安装位置，可为 <see langword="null"/>。</param>
/// <param name="AllowDeepScan">是否允许 Stage 5 受限深度扫描（仅在前面阶段无结果时应启用）。</param>
public sealed record ResidualQuery(
    string? Publisher,
    string? ProductName,
    string? InstallLocation,
    bool AllowDeepScan = false);

/// <summary>单个残留命中项。</summary>
/// <param name="Path">残留路径。</param>
/// <param name="Kind">命中类型。</param>
/// <param name="SizeBytes">占用字节数（注册表键为 0）。</param>
/// <param name="Source">命中来源阶段。</param>
/// <param name="Confidence">置信度 0.0-1.0。</param>
public sealed record ResidualItem(
    string Path,
    ResidualKind Kind,
    long SizeBytes,
    ResidualSourceStage Source,
    double Confidence);

/// <summary>残留定位规则（residual_rules 表 / 种子 JSON 的模型）。</summary>
/// <param name="Publisher">发布者名称（与 Product 共同主键）。</param>
/// <param name="Product">产品名；发布者级通用规则为 <see langword="null"/>。</param>
/// <param name="Paths">候选文件/目录路径模板，支持 %AppData% 等环境变量占位。</param>
/// <param name="RegistryKeys">候选注册表键路径。</param>
public sealed record ResidualRule(
    string Publisher,
    string? Product,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> RegistryKeys);

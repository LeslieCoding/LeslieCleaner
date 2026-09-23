namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>扫描目标安全级别。</summary>
public enum ScanSafetyLevel
{
    /// <summary>安全级：自动生成垃圾，默认勾选。</summary>
    Default,

    /// <summary>可选级：删除可能影响性能预取/系统行为，默认不勾选。</summary>
    Optional,
}

/// <summary>
/// 一类 Smart Scan 扫描目标。候选目录以 "位置键:相对路径" 描述，
/// 相对路径的单个路径段可为 <c>*</c> 通配（匹配任意一级子目录）。
/// </summary>
/// <param name="Id">目标标识（亦为 <c>ScannedItem.Category</c>）。</param>
/// <param name="DisplayName">显示名称。</param>
/// <param name="Safety">安全级别。</param>
/// <param name="CandidatePatterns">候选目录模式集合。</param>
/// <param name="FileFilters">文件名过滤（如 thumbcache_*.db）；空数组表示全部文件。</param>
public sealed record ScanTargetDefinition(
    string Id,
    string DisplayName,
    ScanSafetyLevel Safety,
    IReadOnlyList<string> CandidatePatterns,
    IReadOnlyList<string> FileFilters);

/// <summary>
/// 扫描环境抽象：把位置键（Temp/Windows/LocalAppData 等）解析为实际路径并展开通配目录。
/// 生产实现走环境变量与 <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>；
/// 测试实现指向临时目录，使扫描引擎完全不依赖真实系统路径。
/// </summary>
public interface IScanEnvironment
{
    /// <summary>
    /// 解析位置键为目录路径；未知键返回 <see langword="null"/>。
    /// </summary>
    /// <param name="locationKey">位置键，如 "Temp"、"LocalAppData"。</param>
    string? ResolveLocation(string locationKey);

    /// <summary>
    /// 展开候选目录模式，返回实际存在的目录集合（不存在自动跳过）。
    /// </summary>
    /// <param name="locationKey">位置键。</param>
    /// <param name="relativePattern">相对路径模式，单段可为 <c>*</c> 通配。</param>
    IReadOnlyList<string> ExpandDirectories(string locationKey, string relativePattern);
}

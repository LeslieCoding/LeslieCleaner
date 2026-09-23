namespace LeslieCleaner.Core.Models;

/// <summary>持久化的注册表扫描快照。</summary>
/// <param name="Fingerprint">目标注册表值及外部路径依赖的 SHA-256 指纹。</param>
/// <param name="CreatedUtc">产生时间（UTC）。</param>
/// <param name="ExpiresUtc">过期时间（UTC），默认七天。</param>
/// <param name="Findings">已完成风险分析的结果。</param>
public sealed record RegistryScanCacheSnapshot(
    string Fingerprint,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    IReadOnlyList<RegistryFinding> Findings)
{
    /// <summary>在指定时刻是否可用。指纹必须完全一致且未过期。</summary>
    public bool IsUsable(string fingerprint, DateTimeOffset utcNow) =>
        utcNow < ExpiresUtc &&
        string.Equals(Fingerprint, fingerprint, StringComparison.Ordinal);
}

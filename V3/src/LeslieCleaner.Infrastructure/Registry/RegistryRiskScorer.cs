using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 注册表项风险评分。规则：指向用户目录缺失文件 = Safe（默认勾选）；
/// 指向系统路径（Program Files / Windows）= Moderate；无法判定 = Risky（默认不勾选）。
/// 纯函数，路径经环境变量解析比对，可独立单元测试。
/// </summary>
public static class RegistryRiskScorer
{
    private static readonly string[] UserProfilePrefixes = BuildUserProfilePrefixes();
    private static readonly string[] SystemPrefixes = BuildSystemPrefixes();

    /// <summary>
    /// 对失效目标路径评分。
    /// </summary>
    /// <param name="brokenTarget">缺失的目标路径；为 <see langword="null"/> 时评为 <see cref="RegistryRiskLevel.Risky"/>。</param>
    public static RegistryRiskLevel Score(string? brokenTarget)
    {
        if (string.IsNullOrWhiteSpace(brokenTarget))
        {
            return RegistryRiskLevel.Risky;
        }

        foreach (string prefix in UserProfilePrefixes)
        {
            if (brokenTarget.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return RegistryRiskLevel.Safe;
            }
        }

        foreach (string prefix in SystemPrefixes)
        {
            if (brokenTarget.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return RegistryRiskLevel.Moderate;
            }
        }

        return RegistryRiskLevel.Risky;
    }

    private static string[] BuildUserProfilePrefixes()
    {
        var prefixes = new List<string>(4);
        AddIfNotEmpty(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfNotEmpty(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddIfNotEmpty(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return [.. prefixes];
    }

    private static string[] BuildSystemPrefixes()
    {
        var prefixes = new List<string>(4);
        AddIfNotEmpty(prefixes, Environment.GetEnvironmentVariable("ProgramFiles"));
        AddIfNotEmpty(prefixes, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddIfNotEmpty(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddIfNotEmpty(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        return [.. prefixes];
    }

    private static void AddIfNotEmpty(List<string> prefixes, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            prefixes.Add(path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        }
    }
}

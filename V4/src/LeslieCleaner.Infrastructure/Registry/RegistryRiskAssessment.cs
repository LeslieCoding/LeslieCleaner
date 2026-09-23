using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>可解释的注册表风险评估结果。</summary>
/// <param name="Level">五级风险。</param>
/// <param name="Score">0–100 分，分数越高，修复影响越大。</param>
/// <param name="Explanation">结合位置、类型、依赖和用户影响的解释。</param>
public sealed record RegistryRiskAssessment(
    RegistryRiskLevel Level,
    int Score,
    string Explanation);

/// <summary>
/// 注册表五级风险引擎。所有分数都来自明确因子，
/// 不会因为“目标文件不存在”一个信号就降为可自动删除。
/// </summary>
public static class RegistryRiskEvaluator
{
    /// <summary>结合问题类型、位置、多源证据和保护状态评估风险。</summary>
    public static RegistryRiskAssessment Assess(
        RegistryIssueKind issue,
        string keyPath,
        string? brokenTarget,
        RegistryDetectionEvidence? evidence = null,
        bool isSystemProtected = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        evidence ??= new RegistryDetectionEvidence();

        if (isSystemProtected || RegistryProtectionPolicy.IsProtectedPath(keyPath))
        {
            return new RegistryRiskAssessment(
                RegistryRiskLevel.Critical,
                100,
                "命中系统启动、驱动、安全、硬件或 WRP/TrustedInstaller 保护区；禁止修复。");
        }

        int score = issue switch
        {
            RegistryIssueKind.InvalidUninstallEntry => 24,
            RegistryIssueKind.InvalidFileExtension => 48,
            RegistryIssueKind.InvalidComServer => 72,
            RegistryIssueKind.InvalidFontReference => 55,
            RegistryIssueKind.InvalidPathValue => IsStartupPath(keyPath) ? 78 : 38,
            _ => 65,
        };

        var factors = new List<string>(5) { $"问题类型 {issue}" };
        if (keyPath.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            score -= 6;
            factors.Add("位于当前用户范围");
        }
        else if (keyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
                 keyPath.StartsWith("HKCR", StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
            factors.Add("属于计算机或系统类关联范围");
        }

        RegistryRiskLevel pathRisk = RegistryRiskScorer.Score(brokenTarget);
        if (pathRisk == RegistryRiskLevel.Medium)
        {
            score += 8;
            factors.Add("目标位于 Windows/Program Files/ProgramData");
        }
        else if (pathRisk == RegistryRiskLevel.High)
        {
            score += 12;
            factors.Add("目标位置无法可靠归类");
        }
        else
        {
            factors.Add("目标位于用户资料范围");
        }

        if (evidence.RequiresRetention)
        {
            score = Math.Max(score, 95);
            factors.Add("存在安装、签名、发布者或近期使用保留证据");
        }

        score = Math.Clamp(score, 0, 100);
        RegistryRiskLevel level = score switch
        {
            <= 19 => RegistryRiskLevel.Safe,
            <= 39 => RegistryRiskLevel.Low,
            <= 59 => RegistryRiskLevel.Medium,
            <= 89 => RegistryRiskLevel.High,
            _ => RegistryRiskLevel.Critical,
        };
        return new RegistryRiskAssessment(level, score, string.Join("；", factors) + $"；综合 {score}/100。");
    }

    private static bool IsStartupPath(string keyPath) =>
        keyPath.Contains(@"\CurrentVersion\Run", StringComparison.OrdinalIgnoreCase) ||
        keyPath.Contains(@"\CurrentVersion\RunOnce", StringComparison.OrdinalIgnoreCase);
}

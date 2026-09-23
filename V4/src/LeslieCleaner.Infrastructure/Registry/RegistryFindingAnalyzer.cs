using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 对规则的初始命中做二次证据确认和五级风险分析。
/// 只要存在路径、安装关系、签名、发布者或近期使用证据，就不会产生可修复项。
/// </summary>
public static class RegistryFindingAnalyzer
{
    /// <summary>确认命中并生成可解释的五级风险。</summary>
    /// <returns>应保留时返回 <see langword="null"/>，否则返回已分析项。</returns>
    public static RegistryFinding? Analyze(
        RegistryFinding candidate,
        RegistryDetectionEvidence? evidence = null,
        bool isSystemProtected = false)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        evidence ??= new RegistryDetectionEvidence();
        if (evidence.RequiresRetention)
        {
            return null;
        }

        bool protectedTarget = isSystemProtected || RegistryProtectionPolicy.IsProtectedPath(candidate.KeyPath);
        RegistryRiskAssessment assessment = RegistryRiskEvaluator.Assess(
            candidate.Issue,
            candidate.KeyPath,
            candidate.BrokenTarget,
            evidence,
            protectedTarget);

        return candidate with
        {
            Risk = assessment.Level,
            RiskScore = assessment.Score,
            RiskExplanation = assessment.Explanation,
            Evidence = evidence,
            IsSystemProtected = protectedTarget,
            Recommendation = assessment.Level switch
            {
                RegistryRiskLevel.Safe => "可在完整备份后一键修复",
                RegistryRiskLevel.Low => "备份并标准确认后修复",
                RegistryRiskLevel.Medium => "阅读影响说明并详细确认",
                RegistryRiskLevel.High => "默认保留；仅高级确认后修复",
                _ => "系统保护项，禁止修复",
            },
        };
    }
}

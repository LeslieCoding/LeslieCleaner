namespace LeslieCleaner.Core.Models;

/// <summary>用户对一次注册表修复的明确授权。</summary>
/// <param name="StandardConfirmed">已确认低风险项。</param>
/// <param name="DetailedConfirmed">已阅读并确认中等风险详情。</param>
/// <param name="AdvancedConfirmed">已在高级模式单独确认高风险项。</param>
public sealed record RegistryRepairAuthorization(
    bool StandardConfirmed = false,
    bool DetailedConfirmed = false,
    bool AdvancedConfirmed = false);

/// <summary>注册表修复授权检查结果。</summary>
/// <param name="Allowed">是否允许进入备份/执行阶段。</param>
/// <param name="RequiredConfirmation">最低确认级别。</param>
/// <param name="Reason">可直接呈现给用户的原因。</param>
public sealed record RegistryRepairDecision(
    bool Allowed,
    RegistryConfirmationRequirement RequiredConfirmation,
    string Reason);

/// <summary>
/// 强制风险分级确认。该门禁必须在任何备份或删除之前运行，
/// 不允许调用方用“批量修复”绕过逐项检查。
/// </summary>
public static class RegistryRepairPolicy
{
    /// <summary>检查单项修复授权。</summary>
    public static RegistryRepairDecision Authorize(
        RegistryFinding finding,
        RegistryRepairAuthorization? authorization = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        authorization ??= new RegistryRepairAuthorization();

        if (finding.IsSystemProtected || finding.Risk == RegistryRiskLevel.Critical)
        {
            return new RegistryRepairDecision(
                false,
                RegistryConfirmationRequirement.Forbidden,
                "目标属于系统保护区或 Critical 风险，禁止修复且不会接管 TrustedInstaller/WRP 权限。");
        }

        return finding.ConfirmationRequirement switch
        {
            RegistryConfirmationRequirement.None => Allowed(finding, "Safe 项已通过一键修复门禁。"),
            RegistryConfirmationRequirement.Standard when authorization.StandardConfirmed =>
                Allowed(finding, "Low 项已获得标准确认。"),
            RegistryConfirmationRequirement.Detailed when authorization.DetailedConfirmed =>
                Allowed(finding, "Medium 项已获得详细确认。"),
            RegistryConfirmationRequirement.Advanced when authorization.AdvancedConfirmed =>
                Allowed(finding, "High 项已获得高级确认。"),
            _ => new RegistryRepairDecision(
                false,
                finding.ConfirmationRequirement,
                $"未获得 {finding.ConfirmationRequirement} 确认，拒绝修复。"),
        };
    }

    /// <summary>逐项检查批量修复，保留原输入顺序。</summary>
    public static IReadOnlyList<RegistryRepairDecision> AuthorizeBatch(
        IEnumerable<RegistryFinding> findings,
        RegistryRepairAuthorization? authorization = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return findings.Select(finding => Authorize(finding, authorization)).ToArray();
    }

    private static RegistryRepairDecision Allowed(RegistryFinding finding, string reason) =>
        new(true, finding.ConfirmationRequirement, reason);
}

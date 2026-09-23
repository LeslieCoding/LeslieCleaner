namespace LeslieCleaner.Modules.AI;

/// <summary>阻止 AI 提供危险操作指令，并为高风险建议强制附加备份与确认要求。</summary>
public sealed class AiSafetyFilter
{
    private static readonly string[] DangerousIntentTerms =
    [
        "直接删除", "强制删除", "delete files", "remove-item", "del /f", "rm -rf",
        "修改注册表", "registry edit", "reg delete", "reg add",
        "禁用安全软件", "关闭杀毒", "disable antivirus", "disable defender",
        "关闭系统保护", "禁用系统保护", "disable system protection",
        "禁用防火墙", "disable firewall",
    ];

    private static readonly string[] SensitiveActionTerms =
    [
        "注册表", "registry", "启动项", "startup", "强制清理", "forced cleanup",
        "删除", "delete", "禁用", "disable",
    ];

    /// <summary>过滤模型输出；发现执行指令时用安全拒绝文本替换。</summary>
    public (string Answer, AiSafetyDecision Decision) Apply(string question, string answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);
        string safeQuestion = PrivacyRedactor.Redact(question);
        string safeAnswer = PrivacyRedactor.Redact(answer);
        bool dangerousOutput = ContainsAny(safeAnswer, DangerousIntentTerms);
        bool sensitive = ContainsAny(safeQuestion, SensitiveActionTerms) ||
            ContainsAny(safeAnswer, SensitiveActionTerms) || dangerousOutput;

        if (dangerousOutput)
        {
            safeAnswer = "我不能提供直接删除文件、修改注册表、禁用安全软件或关闭系统保护的执行指令。" +
                "我可以帮助解释风险，并引导你在 LeslieCleaner 的扫描结果中查看可撤销的建议。";
        }

        var decision = sensitive
            ? new AiSafetyDecision(
                IsAllowed: !dangerousOutput,
                RequiresUserConfirmation: true,
                RiskExplanation: "该建议可能影响文件、启动配置、注册表或系统安全状态，误操作可能造成数据丢失或系统异常。",
                BackupSuggestion: "执行前请使用 LeslieCleaner 创建并验证备份或还原点。",
                ConfirmationRequirement: "AI 不会执行此操作；必须由用户查看明细并明确确认。")
            : new AiSafetyDecision(
                IsAllowed: true,
                RequiresUserConfirmation: false,
                RiskExplanation: "当前回答仅包含信息分析，不会更改系统。",
                BackupSuggestion: "无需备份；如后续选择系统操作，仍应先创建备份。",
                ConfirmationRequirement: "当前无需执行确认。 ");

        if (sensitive)
        {
            safeAnswer = $"{safeAnswer}\n\n风险：{decision.RiskExplanation}\n备份：{decision.BackupSuggestion}\n确认：{decision.ConfirmationRequirement}";
        }
        return (safeAnswer, decision);
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}

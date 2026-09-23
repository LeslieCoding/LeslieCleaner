using System.Globalization;
using System.Text;

namespace LeslieCleaner.Modules.AI;

/// <summary>把现有真实扫描指标转换为最小化聚合 AI 上下文。</summary>
public sealed class AiContextCollector
{
    /// <summary>从已有指标与非个人系统汇总创建上下文，不读取文件或浏览器数据库。</summary>
    public AiAggregateContext Collect(
        SystemMetrics metrics,
        double cpuUsagePercent,
        double memoryUsagePercent,
        TimeSpan uptime,
        int installedApplicationCount,
        int recentScanCount,
        int recentCleanupCount)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return new AiAggregateContext(
            "Windows",
            cpuUsagePercent,
            memoryUsagePercent,
            metrics.DiskFreePercent,
            uptime,
            installedApplicationCount,
            metrics.HighImpactStartupCount,
            recentScanCount,
            recentCleanupCount,
            metrics.JunkBytes,
            metrics.PrivacyItemCount).Normalize();
    }
}

/// <summary>构造稳定的 Role/Context/Task/Restrictions/Output 五段式 Prompt。</summary>
public sealed class AiPromptBuilder
{
    /// <summary>构造不含个人内容的结构化 Prompt。</summary>
    public string Build(string question, AiAggregateContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(context);
        AiAggregateContext safe = context.Normalize();
        string safeQuestion = PrivacyRedactor.Redact(question);
        var prompt = new StringBuilder(1024);
        prompt.AppendLine("[ROLE]");
        prompt.AppendLine("You are LeslieCleaner AI Assistant. Explain, recommend and assist only.");
        prompt.AppendLine("[CONTEXT]");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"OS family: {safe.OperatingSystemFamily}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"CPU usage: {safe.CpuUsagePercent:F1}%");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Memory usage: {safe.MemoryUsagePercent:F1}%");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Disk free: {safe.DiskFreePercent:F1}%");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Uptime hours: {safe.SystemUptime.TotalHours:F1}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Installed application count: {safe.InstalledApplicationCount}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"High-impact startup count: {safe.HighImpactStartupCount}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Recent scan/cleanup counts: {safe.RecentScanCount}/{safe.RecentCleanupCount}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Reclaimable bytes: {safe.ReclaimableBytes}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Privacy risk count: {safe.PrivacyRiskCount}");
        prompt.AppendLine("[TASK]");
        prompt.AppendLine(safeQuestion);
        prompt.AppendLine("[RESTRICTIONS]");
        prompt.AppendLine("Never execute or provide direct commands to delete files, modify registry, disable security software, or change system protection. Never request passwords, browser password databases, document content, private messages, file names, user names, or account identifiers. Recommendations that affect the system must explain risk, suggest a backup, and require explicit user confirmation.");
        prompt.AppendLine("[OUTPUT]");
        prompt.AppendLine("Use concise natural language sections: Problem, Reason, Suggestion, Risk. Suggestions only; no execution instructions.");
        return prompt.ToString();
    }
}

/// <summary>把现有健康指标转换为可解释的结构化建议。</summary>
public sealed class AiRecommendationEngine
{
    /// <summary>生成存储、启动、隐私和性能建议。</summary>
    public IReadOnlyList<AiRecommendation> Analyze(SystemMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var result = new List<AiRecommendation>();
        if (metrics.DiskFreePercent < 10)
        {
            result.Add(new("Storage", "系统盘空间接近耗尽", $"剩余空间仅 {metrics.DiskFreePercent:F1}%", "先扫描并确认安全垃圾，再通过带备份的清理流程处理。", AiRecommendationPriority.Critical, 2));
        }
        else if (metrics.JunkBytes >= 1024L * 1024 * 1024)
        {
            result.Add(new("Storage", "存在较多可回收空间", $"扫描发现 {metrics.JunkBytes / 1_073_741_824d:F1} GB 可清理项", "查看扫描明细并仅选择确认过的安全项。", AiRecommendationPriority.High, 1));
        }

        if (metrics.HighImpactStartupCount > 0)
        {
            result.Add(new("Startup", "高影响启动项会延长登录时间", $"共有 {metrics.HighImpactStartupCount} 个高影响项", "先创建备份，再由用户确认是否禁用非必要项目。", AiRecommendationPriority.Medium, 2));
        }

        if (metrics.PrivacyItemCount > 0)
        {
            result.Add(new("Privacy", "存在隐私痕迹", $"聚合扫描发现 {metrics.PrivacyItemCount} 个风险项", "检查类别与白名单后再清理；不要提交个人内容给 AI。", AiRecommendationPriority.Medium, 2));
        }

        if (result.Count == 0)
        {
            result.Add(new("Performance", "未发现明显健康风险", "当前聚合指标处于合理范围", "保持定期扫描即可。", AiRecommendationPriority.Low, 0));
        }
        return result;
    }
}

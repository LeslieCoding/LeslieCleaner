using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.AI;

/// <summary>
/// 系统健康评分引擎（加权真实指标，公式见 docs/05-ai.md 与 DEVLOG）：
/// 垃圾量（GB × 5，封顶 25）+ 高影响启动项（× 4，封顶 20）+ 注册表问题（每 10 项 1 分，封顶 15）
/// + 磁盘剩余（&lt;20% 时每 1% 扣 1，封顶 25）+ 隐私项（每 20 项扣 1，封顶 15）。
/// 纯函数，建议必须引用具体数据。
/// </summary>
public static class HealthScoreEngine
{
    /// <summary>
    /// 计算健康评分（0-100）。
    /// </summary>
    /// <param name="metrics">系统指标。</param>
    public static int ComputeScore(SystemMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        double junkGb = metrics.JunkBytes / 1_073_741_824.0;
        int score = 100
            - (int)Math.Min(25, junkGb * 5)
            - Math.Min(20, metrics.HighImpactStartupCount * 4)
            - Math.Min(15, metrics.RegistryIssueCount / 10)
            - (int)Math.Min(25, Math.Max(0, 20 - metrics.DiskFreePercent))
            - Math.Min(15, metrics.PrivacyItemCount / 20);
        return Math.Clamp(score, 0, 100);
    }

    /// <summary>
    /// 生成完整分析报告（评分 + 三类建议，全部引用具体数据）。
    /// </summary>
    /// <param name="metrics">系统指标。</param>
    public static AiAnalysisReport Analyze(SystemMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        int score = ComputeScore(metrics);
        return new AiAnalysisReport(
            score,
            BuildStartupSuggestions(metrics),
            BuildPrivacySuggestions(metrics),
            BuildStorageSuggestions(metrics));
    }

    /// <summary>启动优化建议。</summary>
    /// <param name="metrics">系统指标。</param>
    public static IReadOnlyList<string> BuildStartupSuggestions(SystemMetrics metrics)
    {
        if (metrics.HighImpactStartupCount == 0)
        {
            return ["启动项状态良好：未发现高影响启动项，登录速度不受第三方程序拖累。"];
        }

        return
        [
            $"检测到 {metrics.HighImpactStartupCount} 个高影响启动项，建议经 Startup Manager 备份式禁用" +
            $"（预计每项节省登录时间 1-3 秒）。",
        ];
    }

    /// <summary>隐私风险分析。</summary>
    /// <param name="metrics">系统指标。</param>
    public static IReadOnlyList<string> BuildPrivacySuggestions(SystemMetrics metrics)
    {
        if (metrics.PrivacyItemCount == 0)
        {
            return ["隐私面干净：最近文件/搜索/运行历史等八类探测均无残留。"];
        }

        return
        [
            $"发现 {metrics.PrivacyItemCount} 项隐私残留（最近文件/搜索历史/运行历史/Office MRU 等），" +
            "建议执行 Privacy Guard 清理（注册表项清理前自动备份）。",
        ];
    }

    /// <summary>存储优化建议。</summary>
    /// <param name="metrics">系统指标。</param>
    public static IReadOnlyList<string> BuildStorageSuggestions(SystemMetrics metrics)
    {
        var suggestions = new List<string>();
        double junkGb = metrics.JunkBytes / 1_073_741_824.0;
        if (junkGb >= 1)
        {
            suggestions.Add($"可清理垃圾 {junkGb:F1} GB（Smart Scan 实测），建议立即执行安全级清理。");
        }

        if (metrics.DiskFreePercent < 20)
        {
            suggestions.Add($"系统盘剩余仅 {metrics.DiskFreePercent:F1}%，低于 20% 警戒线，" +
                "建议结合 Large File Analyzer 处理 Top 大文件。");
        }

        if (metrics.TopLargeFileBytes >= 4L * 1024 * 1024 * 1024)
        {
            suggestions.Add($"最大单文件 {metrics.TopLargeFileBytes / 1_073_741_824.0:F1} GB，" +
                "建议确认其必要性（Large File Analyzer Top 100）。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add($"存储状态健康：垃圾 {junkGb:F2} GB，磁盘剩余 {metrics.DiskFreePercent:F1}%。");
        }

        return suggestions;
    }
}

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LeslieCleaner.Modules.AI;

/// <summary>无需网络和模型文件的真实本地知识 Provider，按聚合指标生成确定性自然语言建议。</summary>
public sealed class LocalRuleKnowledgeProvider : IAiProvider
{
    /// <inheritdoc />
    public string Id => "local-rules";

    /// <inheritdoc />
    public bool IsLocal => true;

    /// <inheritdoc />
    public Task<AiProviderResult> GenerateAsync(AiProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        string prompt = request.Prompt;
        string task = Section(prompt, "TASK");
        double cpu = Number(prompt, "CPU usage");
        double memory = Number(prompt, "Memory usage");
        double free = Number(prompt, "Disk free");
        int startup = (int)Number(prompt, "High-impact startup count");
        long reclaimable = (long)Number(prompt, "Reclaimable bytes");
        int privacy = (int)Number(prompt, "Privacy risk count");

        var reasons = new List<string>();
        var suggestions = new List<string>();
        if (cpu >= 80) { reasons.Add($"CPU 当前使用率为 {cpu:F1}%"); suggestions.Add("查看任务管理器中的持续高占用进程，并先确认其用途"); }
        if (memory >= 85) { reasons.Add($"内存当前使用率为 {memory:F1}%"); suggestions.Add("关闭不再使用的应用，并检查是否存在长期增长的进程"); }
        if (free < 20) { reasons.Add($"系统盘只剩 {free:F1}% 可用空间"); suggestions.Add("运行 Smart Scan 查看可清理明细，并优先处理确认过的大文件或缓存"); }
        if (startup > 0) { reasons.Add($"检测到 {startup} 个高影响启动项"); suggestions.Add("在 Startup Manager 中查看发布者和影响评分，再决定是否备份式禁用"); }
        if (reclaimable >= 1024L * 1024 * 1024) { reasons.Add($"扫描汇总显示约 {reclaimable / 1_073_741_824d:F1} GB 可回收空间"); suggestions.Add("按风险级别检查扫描结果，不要仅依据容量批量清理"); }
        if (privacy > 0) { reasons.Add($"隐私扫描汇总发现 {privacy} 个风险项"); suggestions.Add("检查白名单和登录状态后，再选择要清理的浏览数据类别"); }

        if (reasons.Count == 0)
        {
            reasons.Add("当前 CPU、内存、磁盘、启动项和隐私汇总没有明显异常");
            suggestions.Add("保持定期扫描，并在性能变慢时对比启动项数量和磁盘剩余空间趋势");
        }

        string problem = task.Contains("慢", StringComparison.OrdinalIgnoreCase) || task.Contains("slow", StringComparison.OrdinalIgnoreCase)
            ? "电脑变慢通常与资源持续占用、启动项增加或磁盘空间不足有关。"
            : "我已根据本机聚合健康数据完成分析。";
        string content = $"问题：{problem}\n原因：{string.Join("；", reasons)}。\n建议：{string.Join("；", suggestions)}。\n风险：以上仅为建议，不会自动更改系统；涉及清理或禁用时请先备份并由用户确认。";
        return Task.FromResult(new AiProviderResult(true, content, Id, request.Model, Stopwatch.GetElapsedTime(started)));
    }

    private static string Section(string prompt, string name)
    {
        Match match = Regex.Match(prompt, $@"\[{Regex.Escape(name)}\]\s*(.*?)(?=\r?\n\[[A-Z]+\]|\z)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static double Number(string prompt, string label)
    {
        Match match = Regex.Match(prompt, $@"(?m)^{Regex.Escape(label)}:\s*([0-9.]+)", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
    }
}

namespace LeslieCleaner.Modules.Startup.Classification;

/// <summary>
/// 启动影响评估器（纯函数启发式，输出为估计值，UI 必须标注）。
/// 因素：文件大小、来源类型、已知重型启动项名单、签名状态。
/// </summary>
public static class StartupImpactAssessor
{
    /// <summary>已知重型启动项（可执行文件名小写，高影响基线）。</summary>
    public static IReadOnlyList<string> KnownHeavyPrograms { get; } =
    [
        "steam.exe", "epicgameslauncher.exe", "spotify.exe", "discord.exe",
        "teams.exe", "onedrive.exe", "wechat.exe", "qq.exe", "dingtalk.exe",
        "feishu.exe", "obs64.exe", "docker desktop.exe", "com.docker.backend.exe",
        "adobe creative cloud.exe", "ccxprocess.exe", "baidunetdisk.exe",
    ];

    /// <summary>
    /// 评估启动影响（星级 1-5 + 等级 + CPU/磁盘/内存/耗时估计）。
    /// </summary>
    /// <param name="fileName">可执行文件名。</param>
    /// <param name="fileSizeBytes">文件大小（无文件为 0）。</param>
    /// <param name="sourceKind">来源分类。</param>
    /// <param name="signatureValid">签名是否有效。</param>
    public static StartupImpactEstimate Assess(
        string? fileName,
        long fileSizeBytes,
        StartupSourceKind sourceKind,
        bool signatureValid)
    {
        double score = 1.0;

        // 来源基线
        score += sourceKind switch
        {
            StartupSourceKind.MicrosoftWindows => 0.5,
            StartupSourceKind.HardwareDriver => 1.0,
            StartupSourceKind.StartupUnknown => 2.0,
            _ => 1.5,
        };

        // 文件大小分级
        double sizeMB = fileSizeBytes / 1048576.0;
        score += sizeMB switch
        {
            >= 100 => 1.5,
            >= 30 => 1.0,
            >= 10 => 0.5,
            _ => 0.2,
        };

        // 已知重型名单
        bool isHeavy = fileName is not null && KnownHeavyPrograms.Contains(fileName.ToLowerInvariant());
        if (isHeavy)
        {
            score += 1.5;
        }

        // 无签名惩罚
        if (!signatureValid)
        {
            score += 0.5;
        }

        int stars = (int)Math.Clamp(Math.Round(score), 1, 5);
        StartupImpactLevel level = stars switch
        {
            5 => StartupImpactLevel.Critical,
            4 => StartupImpactLevel.High,
            3 => StartupImpactLevel.Medium,
            2 => StartupImpactLevel.Low,
            _ => StartupImpactLevel.Minimal,
        };

        return new StartupImpactEstimate(
            stars,
            level,
            CpuEstimate: Math.Round(stars * 1.8, 1),
            DiskEstimateMB: Math.Round(Math.Max(sizeMB * 0.6, stars * 2.0), 1),
            MemoryEstimateMB: Math.Round(Math.Max(sizeMB * 0.4, stars * 8.0), 1),
            TimeEstimateSeconds: Math.Round(stars * 0.7, 1));
    }
}

/// <summary>
/// AI 启动建议引擎（纯函数规则引擎：每条建议必须引用真实依据）。
/// </summary>
public static class StartupAdvisor
{
    private static readonly string[] UpdaterKeywords =
        ["updater", "update", "autoupdate", "升级", "更新"];

    /// <summary>
    /// 生成启动建议。
    /// </summary>
    /// <param name="name">启动项名称。</param>
    /// <param name="classification">分类结果。</param>
    /// <param name="signatureValid">签名是否有效。</param>
    public static string Advise(string name, StartupClassification classification, bool signatureValid)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(classification);
        string lowered = name.ToLowerInvariant();

        switch (classification.Kind)
        {
            case StartupSourceKind.MicrosoftWindows:
                if (lowered.Contains("defender") || lowered.Contains("security"))
                {
                    return "禁止关闭：系统安全组件，关闭将使系统失去防护。";
                }

                if (lowered.Contains("audio") || lowered.Contains("shell") || lowered.Contains("explorer"))
                {
                    return "禁止关闭：系统关键组件，关闭将导致功能不可用。";
                }

                return "禁止关闭：Microsoft 系统组件（" + string.Join("；", classification.Reasons) + "）。";

            case StartupSourceKind.HardwareDriver:
                return $"建议保持：{string.Join("；", classification.Reasons)}，关闭可能影响对应硬件功能。";

            case StartupSourceKind.StartupUnknown:
                return $"谨慎处理：{string.Join("；", classification.Reasons)}，存在木马风险，建议核查文件来源后再决定。";

            default:
                if (UpdaterKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.Ordinal)))
                {
                    return "建议关闭：仅更新服务，主程序使用时可自行启动更新，无需常驻。";
                }

                return signatureValid
                    ? "建议关闭：非系统必须，关闭可缩短开机时间（可随时恢复启用）。"
                    : "建议关闭：非系统必须且签名无效，建议关闭并核查来源。";
        }
    }
}

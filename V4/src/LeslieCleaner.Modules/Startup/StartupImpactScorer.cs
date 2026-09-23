namespace LeslieCleaner.Modules.Startup;

/// <summary>
/// 启动影响评分与建议生成（纯函数规则引擎，禁止伪 AI：
/// 每条建议必须引用真实依据——位置、发布者、名单命中）。
/// </summary>
public static class StartupImpactScorer
{
    /// <summary>常见高影响程序名单（可执行文件名小写）。</summary>
    public static IReadOnlyList<string> KnownHighImpactPrograms { get; } =
    [
        "steam.exe", "epicgameslauncher.exe", "spotify.exe", "discord.exe",
        "teams.exe", "ms-teams.exe", "onedrive.exe", "wechat.exe", "qq.exe",
        "tim.exe", "dingtalk.exe", "feishu.exe", "adobe creative cloud.exe",
        "ccxprocess.exe", "core sync.exe", "nba_filemonitor.exe",
        "everything.exe", "baidunetdisk.exe", "xunlei.exe", "thunder.exe",
    ];

    /// <summary>
    /// 计算启动影响评级。
    /// </summary>
    /// <param name="name">启动项名称。</param>
    /// <param name="command">命令行/ImagePath。</param>
    /// <param name="publisher">发布者。</param>
    /// <param name="kind">类别。</param>
    public static StartupImpact Score(string name, string? command, string? publisher, StartupItemKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (IsMicrosoftSystemItem(command, publisher))
        {
            return StartupImpact.Low;
        }

        if (IsKnownHighImpact(command))
        {
            return StartupImpact.High;
        }

        if (kind == StartupItemKind.StartupFolder)
        {
            return StartupImpact.High; // 启动文件夹中的第三方项直接进入登录关键路径
        }

        if (string.IsNullOrWhiteSpace(publisher))
        {
            return StartupImpact.Medium; // 无发布者信息：谨慎评中
        }

        return StartupImpact.Medium;
    }

    /// <summary>
    /// 生成规则建议文本（引用真实依据）。
    /// </summary>
    /// <param name="item">已评分的启动项。</param>
    public static string BuildSuggestion(StartupItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string locationText = item.Kind switch
        {
            StartupItemKind.RegistryRun => "注册表 Run 键",
            StartupItemKind.StartupFolder => "启动文件夹",
            StartupItemKind.ScheduledTask => "计划任务",
            StartupItemKind.Service => "系统服务",
            StartupItemKind.Driver => "驱动自启动",
            _ => "未知位置",
        };

        if (item.Kind == StartupItemKind.Driver)
        {
            return $"\"{item.Name}\" 是驱动自启动（{locationText}），禁用可能导致硬件不可用，建议保留。";
        }

        if (item.Impact == StartupImpact.Low)
        {
            string basis = item.Publisher is not null ? $"发布者为 {item.Publisher}" : "位于系统目录";
            return $"\"{item.Name}\" 属于系统组件（{basis}），建议保留。";
        }

        if (item.Impact == StartupImpact.High)
        {
            string basis = IsKnownHighImpact(item.Command)
                ? $"命中常见高影响程序名单（{Path.GetFileName(item.Command!)}）"
                : $"位于{locationText}且非微软签名";
            return $"\"{item.Name}\" 启动影响高：{basis}，拖慢登录速度，建议禁用（可经备份恢复）。";
        }

        string publisherBasis = string.IsNullOrWhiteSpace(item.Publisher)
            ? "无发布者签名信息"
            : $"发布者为 {item.Publisher}";
        return $"\"{item.Name}\" 启动影响中：位于{locationText}，{publisherBasis}，若不常用建议禁用。";
    }

    /// <summary>判断是否命中常见高影响程序名单。</summary>
    /// <param name="command">命令行/ImagePath。</param>
    public static bool IsKnownHighImpact(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        string fileName = Path.GetFileName(command.Trim('"')).ToLowerInvariant();
        return KnownHighImpactPrograms.Contains(fileName);
    }

    private static bool IsMicrosoftSystemItem(string? command, string? publisher)
    {
        if (publisher is not null &&
            publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (command is null)
        {
            return false;
        }

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return !string.IsNullOrEmpty(windowsDirectory) &&
            command.Trim('"').StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase);
    }
}

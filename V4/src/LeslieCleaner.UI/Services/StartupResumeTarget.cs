namespace LeslieCleaner.UI.Services;

/// <summary>
/// 启动项页面的提权重启恢复目标。该类型只承载稳定项目标识，不解释或执行标识内容。
/// </summary>
public static class StartupResumeTarget
{
    /// <summary>用于命令行恢复启动项页面目标的参数名。</summary>
    public const string ArgumentName = "--startup-item";

    /// <summary>
    /// 规范化可信的启动项标识。拒绝控制字符和异常长度，避免将不可信文本重新注入命令行或日志。
    /// </summary>
    public static bool TryNormalize(string? value, out string? itemId)
    {
        itemId = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (normalized.Length > 2048 || normalized.Any(char.IsControl))
        {
            return false;
        }

        itemId = normalized;
        return true;
    }
}

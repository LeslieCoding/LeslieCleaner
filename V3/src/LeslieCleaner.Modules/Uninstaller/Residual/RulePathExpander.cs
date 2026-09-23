namespace LeslieCleaner.Modules.Uninstaller.Residual;

/// <summary>
/// 规则路径模板展开器（纯函数）。支持 %ProgramFiles% / %ProgramFiles(x86)% /
/// %AppData% / %LocalAppData% / %ProgramData% / %UserProfile% / %Documents% / %Public%
/// 占位，全部经环境变量与特殊文件夹解析。
/// </summary>
public static class RulePathExpander
{
    /// <summary>
    /// 展开模板为实际路径（不做存在性检查）。
    /// </summary>
    /// <param name="template">路径模板。</param>
    public static string Expand(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        string expanded = template
            .Replace("%ProgramFiles(x86)%",
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%ProgramFiles%",
                Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%AppData%",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)
            .Replace("%LocalAppData%",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase)
            .Replace("%ProgramData%",
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), StringComparison.OrdinalIgnoreCase)
            .Replace("%UserProfile%",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)
            .Replace("%Documents%",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
            .Replace("%Public%",
                Environment.GetEnvironmentVariable("PUBLIC") ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        return Environment.ExpandEnvironmentVariables(expanded);
    }

    /// <summary>
    /// 展开并过滤为实际存在的路径（文件或目录）。
    /// </summary>
    /// <param name="templates">模板集合。</param>
    public static IReadOnlyList<string> ExpandExisting(IEnumerable<string> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var existing = new List<string>();
        foreach (string template in templates)
        {
            string expanded = Expand(template);
            if (expanded.Contains('%'))
            {
                continue; // 未解析的占位残留：环境缺该变量，跳过
            }

            if (File.Exists(expanded) || Directory.Exists(expanded))
            {
                existing.Add(expanded);
            }
        }

        return existing;
    }
}

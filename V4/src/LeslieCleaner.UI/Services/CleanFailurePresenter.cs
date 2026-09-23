namespace LeslieCleaner.UI.Services;

/// <summary>面向非技术用户的清理失败说明。</summary>
public sealed record CleanFailurePresentation(string Reason, string Guidance, bool RequiresElevation);

/// <summary>把底层异常信息归类为权限、占用、保护、复验或其他可行动原因。</summary>
public static class CleanFailurePresenter
{
    /// <summary>返回不会伪装成功的失败说明与下一步。</summary>
    public static CleanFailurePresentation Describe(string? error)
    {
        string text = string.IsNullOrWhiteSpace(error) ? "未返回详细错误" : error.Trim();
        if (ContainsAny(text, "unauthorized", "access denied", "拒绝访问", "权限不足", "elevation", "administrator", "管理员"))
        {
            return new("需要管理员权限或当前账户无权访问", "保存当前结果，以管理员身份重新启动后再试。", true);
        }
        if (ContainsAny(text, "being used", "in use", "sharing violation", "占用", "正在使用", "被另一进程"))
        {
            return new("项目正被其他程序占用", "关闭相关应用后复扫，再只清理仍存在的项目。", false);
        }
        if (ContainsAny(text, "protected", "read-only", "readonly", "只读", "受保护", "security", "完整性"))
        {
            return new("项目受系统保护或安全只读策略阻止", "先在安全中心处理完整性/保护原因；不要强制删除。", false);
        }
        if (ContainsAny(text, "verify", "verification", "复验", "仍存在", "not empty"))
        {
            return new("清理后复验未通过", "查看剩余项并重新扫描；不要把已请求清理当作已释放。", false);
        }
        return new(text, "查看运行日志中的原始路径与错误；确认安全后重试。", false);
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
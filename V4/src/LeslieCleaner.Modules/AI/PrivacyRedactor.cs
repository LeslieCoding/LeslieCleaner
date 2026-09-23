using System.Text.RegularExpressions;

namespace LeslieCleaner.Modules.AI;

/// <summary>AI 边界统一隐私检测与脱敏器。</summary>
public static partial class PrivacyRedactor
{
    private const string Redacted = "[REDACTED]";

    /// <summary>判断文本是否疑似包含禁止收集的密码、Token、私信或个人文档内容。</summary>
    public static bool ContainsForbiddenContent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return false; }
        return SecretPattern().IsMatch(value) || PrivateContentPattern().IsMatch(value);
    }

    /// <summary>脱敏凭据、电子邮件、Windows 用户路径、私信/文档内容标记。</summary>
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) { return string.Empty; }
        string result = SecretPattern().Replace(value, "$1=" + Redacted);
        result = BearerPattern().Replace(result, "Bearer " + Redacted);
        result = EmailPattern().Replace(result, Redacted);
        result = UserPathPattern().Replace(result, @"C:\Users\[USER]\");
        result = PrivateContentPattern().Replace(result, Redacted);
        return result.Length <= 4000 ? result : result[..4000];
    }

    /// <summary>将分类字符串限制为不含路径和秘密的短值。</summary>
    public static string SanitizeCategory(string? value, string fallback)
    {
        string cleaned = Redact(value).Trim();
        if (cleaned.Length == 0 || cleaned.Contains(Redacted, StringComparison.Ordinal)) { return fallback; }
        return cleaned.Length <= 64 ? cleaned : cleaned[..64];
    }

    [GeneratedRegex(@"(?i)(password|passwd|pwd|token|api[_ -]?key|secret)\s*[:=]\s*([^\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?i)C:\\Users\\[^\\\s]+\\", RegexOptions.CultureInvariant)]
    private static partial Regex UserPathPattern();

    [GeneratedRegex(@"(?i)(private\s*message|私信|聊天记录|document\s*content|文档内容|browser\s*password|浏览器密码)\s*[:：]?[^\r\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateContentPattern();
}

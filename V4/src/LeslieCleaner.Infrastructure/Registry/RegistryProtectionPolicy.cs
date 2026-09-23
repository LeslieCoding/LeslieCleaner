namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 不可修复的注册表系统区域。判断只会扩大保护范围，
/// 绝不会取得所有权、改写 ACL 或绕过 TrustedInstaller/WRP。
/// </summary>
public static class RegistryProtectionPolicy
{
    private static readonly string[] ProtectedPrefixes =
    [
        @"HKLM\BCD00000000",
        @"HKLM\SYSTEM",
        @"HKLM\SAM",
        @"HKLM\SECURITY",
        @"HKLM\HARDWARE",
        @"HKLM\SOFTWARE\Microsoft\Windows Defender",
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender",
        @"HKLM\SOFTWARE\Microsoft\Security",
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies",
    ];

    /// <summary>判断路径是否命中系统启动、驱动、安全或硬件保护区。</summary>
    public static bool IsProtectedPath(string keyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        string normalized = Normalize(keyPath);
        return ProtectedPrefixes.Any(prefix => IsSameOrChild(normalized, prefix));
    }

    /// <summary>判断候选路径是否与保护前缀相同或位于其下。</summary>
    public static bool IsSameOrChild(string candidate, string prefix) =>
        candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
        (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
         candidate.Length > prefix.Length && candidate[prefix.Length] == '\\');

    private static string Normalize(string keyPath)
    {
        string normalized = keyPath.Trim().TrimEnd('\\').Replace('/', '\\');
        int slash = normalized.IndexOf('\\');
        string root = slash < 0 ? normalized : normalized[..slash];
        string suffix = slash < 0 ? string.Empty : normalized[slash..];
        root = root.ToUpperInvariant() switch
        {
            "HKLM32" or "HKLM64" or "HKEY_LOCAL_MACHINE" or "HKEY_LOCAL_MACHINE32" => "HKLM",
            "HKCU32" or "HKCU64" or "HKEY_CURRENT_USER" or "HKEY_CURRENT_USER32" => "HKCU",
            "HKCR32" or "HKCR64" or "HKEY_CLASSES_ROOT" or "HKEY_CLASSES_ROOT32" => "HKCR",
            _ => root,
        };
        return root + suffix;
    }
}

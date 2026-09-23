using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 注册表键存在性轻量探针（只读，64 位视图）。
/// 供规则库等需要高频存在性检查的场景使用，避免完整引擎往返。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class RegistryKeyProbe
{
    /// <summary>
    /// 判断键是否存在。解析失败或访问被拒返回 <see langword="false"/>。
    /// </summary>
    /// <param name="keyPath">完整注册表路径（HKLM\/HKCU\/HKCR\/HKU\/HKCC\ 前缀）。</param>
    public static bool Exists(string keyPath)
    {
        try
        {
            using RegistryKey hive = RegistryEngine.OpenHive(keyPath, out string subPath);
            if (subPath.Length == 0)
            {
                return true;
            }

            using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
            return key is not null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 探测失败按"键不存在"处理（轻量只读探针，静态设计无日志通道）。
            return false;
        }
    }
}

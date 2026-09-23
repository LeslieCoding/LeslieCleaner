using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Registry;

/// <summary>
/// WRP（Windows 资源保护）注册表项处理：TrustedInstaller 所有的键（连管理员也只读）
/// 必须被识别为"不可删除"并从扫描结果中跳过，杜绝"删了和没删一样"。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryWrpProtectionTests : IDisposable
{
    private readonly string _tempKeyRelative = $@"Software\LeslieCleaner.Tests\{Guid.NewGuid():N}";
    private readonly TempWorkspace _workspace = new();

    public RegistryWrpProtectionTests()
    {
        using RegistryKey _ = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative);
    }

    public void Dispose()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_tempKeyRelative, throwOnMissingSubKey: false);
        _workspace.Dispose();
    }

    [Fact]
    public void IsSystemProtectedKey_RealWrpKey_ReturnsTrue()
    {
        // 本机真实 WRP 键（所有者 TrustedInstaller，Administrators 仅 ReadKey）
        using RegistryKey classesRoot = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);
        using RegistryKey? wrpKey = classesRoot.OpenSubKey(
            @"CLSID\{A530D54A-DBA0-4b17-9F99-51A7A2CC17CA}\InprocServer32", writable: false);
        if (wrpKey is null)
        {
            return; // 键已被清理时跳过
        }

        Assert.True(RegistryScanner.IsSystemProtectedKey(wrpKey));
    }

    [Fact]
    public void IsSystemProtectedKey_NormalUserKey_ReturnsFalse()
    {
        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(_tempKeyRelative);
        Assert.False(RegistryScanner.IsSystemProtectedKey(key));
        Assert.False(RegistryScanner.IsSystemProtectedKey(null));
    }

    [Fact]
    public async Task Scan_SkipsWrpProtectedComEntries()
    {
        var scanner = new RegistryScanner(_workspace.Log);

        IReadOnlyList<Core.Models.RegistryFinding> findings = await scanner.ScanAsync();

        // 两个真实 WRP CLSID（所有者 TrustedInstaller）不再出现在可修复问题中
        Assert.DoesNotContain(findings,
            f => f.KeyPath.Contains("A530D54A-DBA0-4b17-9F99-51A7A2CC17CA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(findings,
            f => f.KeyPath.Contains("E9F83CF2-E0C0-4CA7-AF01-E90C70BEF496", StringComparison.OrdinalIgnoreCase));
    }
}

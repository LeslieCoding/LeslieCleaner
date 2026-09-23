using System.Security.Principal;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Registry;

/// <summary>
/// 管理员注册表验收。只操作每次随机创建的 LeslieCleaner 测试子树，
/// 不读取、备份或删除任何既有系统/第三方键。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[Trait("Category", "Privileged")]
public sealed class RegistryPrivilegedAcceptanceTests : IDisposable
{
    private readonly string _relativePath =
        $@"SOFTWARE\LeslieCleaner.PrivilegedAcceptance.Tests\{Guid.NewGuid():N}";
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task HklmPhysicalViews_BackupDeleteReadbackAndRestore()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(
                "LESLIECLEANER_RUN_PRIVILEGED_ACCEPTANCE"), "1", StringComparison.Ordinal))
        {
            return; // 仅专用脚本可开启；普通/管理员全量测试均不得意外写 HKLM。
        }

        Assert.True(IsElevated(),
            "Privileged registry acceptance was explicitly enabled but the process is not elevated.");
        RegistryView[] views = Environment.Is64BitOperatingSystem
            ? [RegistryView.Registry64, RegistryView.Registry32]
            : [RegistryView.Registry64];
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var engine = new RegistryEngine(backupService, _workspace.Log);

        foreach (RegistryView view in views)
        {
            string suffix = view == RegistryView.Registry32 ? "32" : string.Empty;
            string keyPath = $@"HKLM{suffix}\{_relativePath}";
            using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            try
            {
                using (RegistryKey key = hive.CreateSubKey(_relativePath, writable: true))
                {
                    key.SetValue("AcceptanceValue", "before", RegistryValueKind.String);
                    using RegistryKey child = key.CreateSubKey("Child", writable: true);
                    child.SetValue("Payload", 42, RegistryValueKind.DWord);
                    key.Flush();
                }

                var valueBackup = await backupService.BackupRegistryValueAsync(keyPath, "AcceptanceValue");
                await engine.DeleteValueAsync(keyPath, "AcceptanceValue", valueBackup.Id);
                Assert.DoesNotContain("AcceptanceValue", await engine.EnumerateValuesAsync(keyPath),
                    StringComparer.OrdinalIgnoreCase);
                await backupService.RestoreAsync(valueBackup.Id);
                Assert.Equal("before", await engine.ReadValueAsync(keyPath, "AcceptanceValue"));

                string childPath = keyPath + @"\Child";
                var keyBackup = await backupService.BackupRegistryKeyAsync(childPath);
                await engine.DeleteKeyAsync(childPath, keyBackup.Id);
                Assert.False(await engine.KeyExistsAsync(childPath));
                await backupService.RestoreAsync(keyBackup.Id);
                Assert.True(await engine.KeyExistsAsync(childPath));
                Assert.Equal(42, await engine.ReadValueAsync(childPath, "Payload"));
            }
            finally
            {
                hive.DeleteSubKeyTree(_relativePath, throwOnMissingSubKey: false);
            }
        }
    }

    public void Dispose() => _workspace.Dispose();

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

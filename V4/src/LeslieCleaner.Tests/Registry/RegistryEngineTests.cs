using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;
using System.Security.Principal;

namespace LeslieCleaner.Tests.Registry;

/// <summary>
/// 注册表引擎测试。全部操作在 HKCU\Software\LeslieCleaner.Tests 下的临时键进行，
/// 用例结束强制清理；不触碰任何真实软件键。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryEngineTests : IDisposable
{
    private readonly string _tempKeyRelative = $@"Software\LeslieCleaner.Tests\{Guid.NewGuid():N}";
    private readonly TempWorkspace _workspace = new();
    private readonly RegistryBackupService _backupService;
    private readonly RegistryEngine _engine;

    private string TempKeyPath => $@"HKCU\{_tempKeyRelative}";

    public RegistryEngineTests()
    {
        _backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        _engine = new RegistryEngine(_backupService, _workspace.Log);
        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative);
        key.CreateSubKey("ChildA")?.Dispose();
        key.CreateSubKey("ChildB")?.Dispose();
        key.SetValue("TestValue", "hello", RegistryValueKind.String);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_tempKeyRelative, throwOnMissingSubKey: false);
        }
        finally
        {
            _workspace.Dispose();
        }
    }

    [Fact]
    public async Task KeyExists_Works()
    {
        Assert.True(await _engine.KeyExistsAsync(TempKeyPath));
        Assert.False(await _engine.KeyExistsAsync(TempKeyPath + @"\NoSuchKey"));
    }

    [Fact]
    public async Task EnumerateSubKeys_ReturnsChildren()
    {
        IReadOnlyList<string> subKeys = await _engine.EnumerateSubKeysAsync(TempKeyPath);

        Assert.Equal(2, subKeys.Count);
        Assert.Contains("ChildA", subKeys);
        Assert.Contains("ChildB", subKeys);
    }

    [Fact]
    public async Task ReadValue_ReturnsStoredValue()
    {
        Assert.Equal("hello", await _engine.ReadValueAsync(TempKeyPath, "TestValue"));
        Assert.Null(await _engine.ReadValueAsync(TempKeyPath, "NoSuchValue"));
    }

    [Fact]
    public async Task DeleteKey_WithoutBackup_Throws()
    {
        await Assert.ThrowsAsync<BackupException>(
            () => _engine.DeleteKeyAsync(TempKeyPath + @"\ChildA", Guid.NewGuid()));

        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.Exception));
    }

    [Fact]
    public async Task DeleteKey_UnverifiedDeletion_ThrowsPermissionError()
    {
        if (IsElevated()) return; // 管理员验收只允许使用随机 HKLM 测试子树。
        // HKCR 合并视图在无管理员权限时 DeleteSubKeyTree 静默 no-op（不抛异常也不删除），
        // 引擎必须回读验证并报权限不足——本机为标准权限运行，该键真实存在且无写权限
        const string protectedKey = @"HKCR\CLSID\{A530D54A-DBA0-4b17-9F99-51A7A2CC17CA}\InprocServer32";
        if (!await _engine.KeyExistsAsync(protectedKey))
        {
            return; // 键已被管理员清理时跳过
        }

        // 备份真实键（reg.exe export 不需要写权限）
        BackupRecord backup = await _backupService.BackupRegistryKeyAsync(protectedKey);

        BackupException ex = await Assert.ThrowsAsync<BackupException>(
            () => _engine.DeleteKeyAsync(protectedKey, backup.Id));

        Assert.Contains("权限不足", ex.Message);
        // 删除未生效：键必须仍在（不允许假成功）；备份文件保留可用于恢复
        Assert.True(await _engine.KeyExistsAsync(protectedKey));
    }

    [Fact]
    public async Task DeleteValue_UnverifiedDeletion_ThrowsPermissionError()
    {
        if (IsElevated()) return; // 禁止管理员测试触碰既有系统 COM 键。
        const string protectedKey = @"HKCR\CLSID\{A530D54A-DBA0-4b17-9F99-51A7A2CC17CA}\InprocServer32";
        if (!await _engine.KeyExistsAsync(protectedKey))
        {
            return;
        }

        BackupRecord backup = await _backupService.BackupRegistryValueAsync(protectedKey, string.Empty);
        BackupException ex = await Assert.ThrowsAsync<BackupException>(
            () => _engine.DeleteValueAsync(protectedKey, string.Empty, backup.Id));

        Assert.Contains("权限不足", ex.Message);
    }

    [Fact]
    public async Task BackupDeleteRestore_FullLoop()
    {
        string target = TempKeyPath + @"\ChildA";
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative + @"\ChildA"))
        {
            key.SetValue("Payload", 42, RegistryValueKind.DWord);
        }

        BackupRecord backup = await _backupService.BackupRegistryKeyAsync(target);
        Assert.True(File.Exists(backup.FilePath));
        Assert.True(File.Exists(backup.MetadataPath));

        await _engine.DeleteKeyAsync(target, backup.Id);
        Assert.False(await _engine.KeyExistsAsync(target));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteFinish));

        await _backupService.RestoreAsync(backup.Id);
        Assert.True(await _engine.KeyExistsAsync(target));
        Assert.Equal(42, await _engine.ReadValueAsync(target, "Payload"));
    }

    [Fact]
    public async Task BackupDeleteRestore_ValuePreservesSiblingChanges()
    {
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative))
        {
            key.SetValue("Target", 42, RegistryValueKind.DWord);
            key.SetValue("Sibling", "before", RegistryValueKind.String);
        }

        BackupRecord backup = await _backupService.BackupRegistryValueAsync(TempKeyPath, "Target");
        await _engine.DeleteValueAsync(TempKeyPath, "Target", backup.Id);
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(_tempKeyRelative, writable: true)!)
        {
            key.SetValue("Sibling", "changed-after-backup", RegistryValueKind.String);
        }

        await _backupService.RestoreAsync(backup.Id);

        Assert.Equal(42, await _engine.ReadValueAsync(TempKeyPath, "Target"));
        Assert.Equal("changed-after-backup", await _engine.ReadValueAsync(TempKeyPath, "Sibling"));
    }

    [Fact]
    public async Task BackupDeleteRestore_32BitView_FullLoop()
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            return;
        }

        string relativePath = _tempKeyRelative + "-32";
        string target = $@"HKCU32\{relativePath}";
        using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
        try
        {
            using (RegistryKey key = hive.CreateSubKey(relativePath))
            {
                key.SetValue("Payload", "x86", RegistryValueKind.String);
            }

            BackupRecord backup = await _backupService.BackupRegistryKeyAsync(target);
            await _engine.DeleteKeyAsync(target, backup.Id);
            Assert.False(await _engine.KeyExistsAsync(target));

            await _backupService.RestoreAsync(backup.Id);
            Assert.Equal("x86", await _engine.ReadValueAsync(target, "Payload"));
        }
        finally
        {
            hive.DeleteSubKeyTree(relativePath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public async Task DeleteValue_WithBackup_RemovesOnlyValue()
    {
        BackupRecord backup = await _backupService.BackupRegistryValueAsync(TempKeyPath, "TestValue");

        await _engine.DeleteValueAsync(TempKeyPath, "TestValue", backup.Id);

        Assert.Null(await _engine.ReadValueAsync(TempKeyPath, "TestValue"));
        Assert.True(await _engine.KeyExistsAsync(TempKeyPath + @"\ChildA"));
    }

    [Fact]
    public async Task DeleteValue_KeyScopedBackupIsRejectedBeforeMutation()
    {
        BackupRecord backup = await _backupService.BackupRegistryKeyAsync(TempKeyPath);

        BackupException exception = await Assert.ThrowsAsync<BackupException>(
            () => _engine.DeleteValueAsync(TempKeyPath, "TestValue", backup.Id));

        Assert.Equal(OperationFailureKind.BackupFailed, exception.FailureKind);
        Assert.Equal("hello", await _engine.ReadValueAsync(TempKeyPath, "TestValue"));
    }

    [Fact]
    public async Task DeleteValue_CorruptBackupIsRejectedBeforeMutation()
    {
        BackupRecord backup = await _backupService.BackupRegistryValueAsync(TempKeyPath, "TestValue");
        await File.AppendAllTextAsync(backup.FilePath, "tampered");

        BackupException exception = await Assert.ThrowsAsync<BackupException>(
            () => _engine.DeleteValueAsync(TempKeyPath, "TestValue", backup.Id));

        Assert.Equal(OperationFailureKind.BackupFailed, exception.FailureKind);
        Assert.Contains("完整性校验失败", exception.Message, StringComparison.Ordinal);
        Assert.Equal("hello", await _engine.ReadValueAsync(TempKeyPath, "TestValue"));
        Assert.False(_workspace.Log.HasEvent(LogEventKind.DeleteFinish));
    }

    [Fact]
    public async Task DeleteValue_BackupAndReadbackMatchValueNameCaseInsensitively()
    {
        BackupRecord backup = await _backupService.BackupRegistryValueAsync(TempKeyPath, "TestValue");

        await _engine.DeleteValueAsync(TempKeyPath, "testvalue", backup.Id);

        Assert.Null(await _engine.ReadValueAsync(TempKeyPath, "TESTVALUE"));
    }

    [Fact]
    public async Task DeleteValue_BackupAndDeleteAcceptEquivalentHiveAliases()
    {
        const string backupPath = "HKCU64\\Software\\LeslieCleaner.Tests";
        const string deletePath = "HKEY_CURRENT_USER\\Software\\LeslieCleaner.Tests";
        BackupRecord backup = await _backupService.BackupRegistryValueAsync(backupPath, "TestValue");

        await _engine.DeleteValueAsync(deletePath, "TestValue", backup.Id);

        Assert.Null(await _engine.ReadValueAsync(backupPath, "TestValue"));
    }

    [Fact]
    public async Task BackupDeleteRestore_ValueNameCaseInsensitiveAndOriginalCasingPreserved()
    {
        BackupRecord backup = await _backupService.BackupRegistryValueAsync(TempKeyPath, "testvalue");

        Assert.True(backup.ValueSnapshot?.Exists);
        Assert.Equal("TestValue", backup.ValueName);
        Assert.Equal("TestValue", backup.ValueSnapshot?.OriginalValueName);

        await _engine.DeleteValueAsync(TempKeyPath, "TESTVALUE", backup.Id);
        Assert.Null(await _engine.ReadValueAsync(TempKeyPath, "testvalue"));

        await _backupService.RestoreAsync(backup.Id);

        Assert.Equal("hello", await _engine.ReadValueAsync(TempKeyPath, "testvalue"));
        IReadOnlyList<string> names = await _engine.EnumerateValuesAsync(TempKeyPath);
        Assert.Contains("TestValue", names);
    }

    [Fact]
    public async Task DeleteKey_TargetRemovedAfterBackup_IsIdempotentlyVerified()
    {
        string target = TempKeyPath + @"\ChildB";
        BackupRecord backup = await _backupService.BackupRegistryKeyAsync(target);
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
            _tempKeyRelative + @"\ChildB", throwOnMissingSubKey: false);

        await _engine.DeleteKeyAsync(target, backup.Id);

        Assert.False(await _engine.KeyExistsAsync(target));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteFinish));
    }

    [Fact]
    public async Task DeleteValue_SecurityGateBlocked_PreservesValue()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var guarded = new RegistryEngine(_backupService, _workspace.Log, gate);

        await Assert.ThrowsAsync<LeslieCleanerException>(
            () => guarded.DeleteValueAsync(TempKeyPath, "TestValue", Guid.NewGuid()));

        Assert.Equal("hello", await _engine.ReadValueAsync(TempKeyPath, "TestValue"));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.Exception));
    }

    [Fact]
    public void ParseKeyPath_SupportsAllRoots()
    {
        (RegistryKey hive, string subPath) = RegistryEngine.ParseKeyPath(@"HKLM\SOFTWARE\X");
        Assert.Equal("SOFTWARE\\X", subPath);
        hive.Dispose();

        Assert.Throws<ArgumentException>(() => RegistryEngine.ParseKeyPath(@"BADROOT\X"));
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

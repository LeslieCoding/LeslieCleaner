using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Privacy;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Privacy;

/// <summary>Privacy Guard 测试。注册表闭环在 HKCU\Software\LeslieCleaner.Tests 临时键进行，测完强制清理。</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PrivacyGuardTests : IDisposable
{
    private readonly string _tempKeyRelative = $@"Software\LeslieCleaner.Tests\{Guid.NewGuid():N}";
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly PrivacyGuardEngine _engine;
    private readonly RegistryEngine _registryEngine;

    private string TempKeyPath => $@"HKCU\{_tempKeyRelative}";

    public PrivacyGuardTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        var quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        _registryEngine = new RegistryEngine(backupService, _workspace.Log);
        var cleanEngine = new CleanEngine(quarantine, backupService, _registryEngine, _workspace.PathService, _workspace.Log);
        _engine = new PrivacyGuardEngine(_registryEngine, cleanEngine, browserPrivacyCleaner: null, _workspace.Log);

        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative);
        key.SetValue("MRUListEx", new byte[] { 1, 0 }, RegistryValueKind.Binary);
        key.SetValue("a", "notepad.exe", RegistryValueKind.String);
    }

    public void Dispose()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_tempKeyRelative, throwOnMissingSubKey: false);
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task CleanThenRollback_RegistryMruValues_FullLoop()
    {
        var items = new List<PrivacyItem>
        {
            new(PrivacyCategory.RunHistory, "运行历史（a）", $"{TempKeyPath}|a", IsRegistryTarget: true, 0),
            new(PrivacyCategory.RunHistory, "运行历史（MRUListEx）", $"{TempKeyPath}|MRUListEx", IsRegistryTarget: true, 0),
        };

        CleanResult result = await _engine.CleanAsync(items);

        Assert.True(result.Items.All(i => i.Success));
        Assert.Empty(await _registryEngine.EnumerateValuesAsync(TempKeyPath));

        bool rolledBack = await _engine.RollbackAsync(result.OperationId);

        Assert.True(rolledBack);
        Assert.Equal("notepad.exe", await _registryEngine.ReadValueAsync(TempKeyPath, "a"));
    }

    [Fact]
    public async Task Clean_FileItems_GoesToQuarantine()
    {
        string privacyFile = _workspace.CreateFile("privacy/recent-shortcut.lnk", "lnk");

        CleanResult result = await _engine.CleanAsync(
            [new PrivacyItem(PrivacyCategory.RecentFiles, "最近文件", privacyFile, IsRegistryTarget: false, 1)]);

        Assert.True(result.Items.All(i => i.Success));
        Assert.False(File.Exists(privacyFile));
    }

    [Fact]
    public async Task Clean_SecurityGateBlocked_PreservesRegistryValue()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var guarded = new PrivacyGuardEngine(_registryEngine,
            new CleanEngine(quarantine, backupService, _registryEngine, _workspace.PathService, _workspace.Log),
            browserPrivacyCleaner: null, _workspace.Log, gate);

        await Assert.ThrowsAsync<LeslieCleaner.Core.Diagnostics.LeslieCleanerException>(
            () => guarded.CleanAsync([new PrivacyItem(PrivacyCategory.RunHistory, "test", $"{TempKeyPath}|a", true, 0)]));

        Assert.Equal("notepad.exe", await _registryEngine.ReadValueAsync(TempKeyPath, "a"));
    }

    [Fact]
    public async Task Scan_RealSystem_CompletesWithoutError()
    {
        IReadOnlyList<PrivacyItem> items = await _engine.ScanAsync();

        // 只读探测：不修改任何内容；本机有/无命中均合法
        Assert.NotNull(items);
        Assert.DoesNotContain(items, i => i.Category == PrivacyCategory.BrowserPrivacy); // 未注入浏览器清理器
    }
}

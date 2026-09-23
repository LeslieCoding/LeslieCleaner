using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Registry;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Cleaner.Scanning;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Cleaner;

/// <summary>
/// Smart Scan 引擎与清理引擎闭环测试。全部文件目标指向临时目录；
/// 注册表闭环使用 HKCU\Software\LeslieCleaner.Tests 下的临时键，用例结束强制清理。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SmartScanEngineTests : IDisposable
{
    private readonly string _tempKeyRelative = $@"Software\LeslieCleaner.Tests\{Guid.NewGuid():N}";
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly RegistryBackupService _backupService;
    private readonly RegistryEngine _registryEngine;

    private string TempKeyPath => $@"HKCU\{_tempKeyRelative}";

    public SmartScanEngineTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        _backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        _registryEngine = new RegistryEngine(_backupService, _workspace.Log);
    }

    public void Dispose()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_tempKeyRelative, throwOnMissingSubKey: false);
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Scan_HitsFabricatedCachesAndSkipsMissing()
    {
        TestScanEnvironment environment = BuildFabricatedEnvironment(out _);
        var engine = new SmartScanEngine(environment, registryScanner: null, _workspace.Log);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["*"] });

        Assert.False(result.WasCancelled);
        Assert.Equal(2, result.Items.Count(static i => i.Category == "TempFiles"));
        Assert.Single(result.Items, static i => i.Category == "BrowserCache");
        Assert.Single(result.Items, static i => i.Category == "AiToolsCache");
        ScannedItem browser = Assert.Single(result.Items, static i => i.Category == "BrowserCache");
        ScannedItem aiTools = Assert.Single(result.Items, static i => i.Category == "AiToolsCache");
        Assert.Equal("LCN-CACHE-BROWSER-CACHE-001", browser.RuleId);
        Assert.Equal("Browser Providers", browser.Provider);
        Assert.Equal("1.0", browser.RuleVersion);
        Assert.Equal("Verified", browser.AccessState);
        Assert.Equal("LCN-CACHE-AI-TOOLS-CACHE-001", aiTools.RuleId);
        Assert.Equal("AI and Developer Tools", aiTools.Provider);
        Assert.DoesNotContain(result.Items, static i => i.Category == "Prefetch");
        Assert.DoesNotContain(result.Items, static i => i.Category == "ThumbnailCache");
        Assert.True(result.TotalSizeBytes > 0);
    }

    [Fact]
    public async Task Scan_RegistryTarget_EncodesValueLevelFindings()
    {
        var findings = new List<RegistryFinding>
        {
            new(@"HKCU\Software\Dead", ValueName: null, RegistryIssueKind.InvalidUninstallEntry,
                RegistryRiskLevel.Safe, "key-level", @"C:\Gone\a.exe"),
            new(@"HKCU\Software\Fonts", "Dead Font", RegistryIssueKind.InvalidFontReference,
                RegistryRiskLevel.Safe, "value-level", @"C:\Gone\f.ttf"),
        };
        var engine = new SmartScanEngine(
            new TestScanEnvironment(), new FakeRegistryScanner(findings), _workspace.Log);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["*"] });

        Assert.Equal(2, result.Items.Count(static i => i.Category == SmartScanTargets.RegistryCategoryId));
        Assert.Contains(result.Items, static i => i.Path == @"HKCU\Software\Dead");
        Assert.Contains(result.Items, static i => i.Path == @"HKCU\Software\Fonts|Dead Font");
    }

    [Fact]
    public async Task Scan_RegistryTargetFailure_IsCountedWithoutDiscardingOtherTargetResults()
    {
        TestScanEnvironment environment = BuildFabricatedEnvironment(out _);
        var engine = new SmartScanEngine(environment, new ThrowingRegistryScanner(), _workspace.Log);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["TempFiles", "Registry"] });

        Assert.False(result.WasCancelled);
        Assert.Equal(1, result.ErrorCount);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, static item => Assert.Equal("TempFiles", item.Category));
    }

    [Fact]
    public async Task Scan_SelectedTargets_OnlyReturnsRequestedCategories()
    {
        TestScanEnvironment environment = BuildFabricatedEnvironment(out _);
        var engine = new SmartScanEngine(environment, registryScanner: null, _workspace.Log);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["TempFiles"] });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, static item => Assert.Equal("TempFiles", item.Category));
    }

    [Fact]
    public async Task Scan_RecycleBin_UsesShellSnapshotForExactLogicalCountAndBytes()
    {
        var recycleBin = new FakeRecycleBinService(new RecycleBinSnapshot(1_237, 45_678_901));
        var engine = new SmartScanEngine(new TestScanEnvironment(), registryScanner: null, _workspace.Log, recycleBin);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["RecycleBin"] });

        ScannedItem item = Assert.Single(result.Items);
        Assert.Equal(SmartScanTargets.RecycleBinCategoryId, item.Category);
        Assert.Equal(1_237, item.ItemCount);
        Assert.Equal(45_678_901, item.SizeBytes);
    }

    [Fact]
    public async Task Clean_RecycleBin_RequiresConfirmationAndVerifiesShellEmpty()
    {
        var recycleBin = new FakeRecycleBinService(new RecycleBinSnapshot(3, 1234));
        var engine = new CleanEngine(_quarantine, _backupService, registryEngine: null,
            _workspace.PathService, _workspace.Log, recycleBin);
        ScannedItem item = new("shell://recycle-bin", 1234, SmartScanTargets.RecycleBinCategoryId,
            DateTimeOffset.UtcNow, 3);

        CleanResult denied = await engine.CleanAsync(new CleanRequest { Items = [item], Reason = "test" });
        Assert.False(Assert.Single(denied.Items).Success);
        Assert.Equal(3, (await recycleBin.GetSnapshotAsync()).ItemCount);

        CleanResult cleaned = await engine.CleanAsync(new CleanRequest
        {
            Items = [item],
            Reason = "test",
            AllowIrreversibleRecycleBin = true,
        });

        Assert.True(Assert.Single(cleaned.Items).Success);
        Assert.Equal(1234, cleaned.FreedBytes);
        Assert.Equal(0, (await recycleBin.GetSnapshotAsync()).ItemCount);
    }

    [Fact]
    public async Task CleanThenRollback_FullLoop()
    {
        TestScanEnvironment environment = BuildFabricatedEnvironment(out string tempRoot);
        var scanEngine = new SmartScanEngine(environment, registryScanner: null, _workspace.Log);
        ScanResult scan = await scanEngine.ScanAsync(new ScanOptions { Targets = ["*"] });

        // 注册表目标：HKCU 临时键（值级 + 键级）
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative))
        {
            key.SetValue("DeadValue", "x", RegistryValueKind.String);
            key.CreateSubKey("DeadSub")?.SetValue("Payload", 7, RegistryValueKind.DWord);
        }

        var items = new List<ScannedItem>(scan.Items)
        {
            new($"{TempKeyPath}|DeadValue", 0, SmartScanTargets.RegistryCategoryId, DateTimeOffset.UtcNow),
            new($@"{TempKeyPath}\DeadSub", 0, SmartScanTargets.RegistryCategoryId, DateTimeOffset.UtcNow),
        };

        var cleanEngine = new CleanEngine(_quarantine, _backupService, _registryEngine, _workspace.PathService, _workspace.Log);
        CleanResult clean = await cleanEngine.CleanAsync(
            new CleanRequest { Items = items, Reason = "闭环测试" });

        Assert.True(clean.Items.All(static i => i.Success));
        Assert.True(clean.FreedBytes > 0);
        // 文件已进隔离区
        Assert.Empty(Directory.EnumerateFiles(tempRoot, "*", SearchOption.AllDirectories));
        Assert.Equal(4, (await _quarantine.ListEntriesAsync()).Count);
        // 注册表已删除
        Assert.Null(await _registryEngine.ReadValueAsync(TempKeyPath, "DeadValue"));
        Assert.False(await _registryEngine.KeyExistsAsync($@"{TempKeyPath}\DeadSub"));

        bool rolledBack = await cleanEngine.RollbackAsync(clean.OperationId);

        Assert.True(rolledBack);
        // 文件已还原（tempRoot 仅含其自身的 2 个临时文件；浏览器/AI 缓存还原至各自原路径）
        Assert.Equal(2, Directory.EnumerateFiles(tempRoot, "*", SearchOption.AllDirectories).Count());
        Assert.True(File.Exists(Path.Combine(_workspace.RootDirectory, @"FakeLocalAppData\Google\Chrome\User Data\Default\Cache\chrome-cache.bin")));
        Assert.True(File.Exists(Path.Combine(_workspace.RootDirectory, @"FakeRoaming\Cursor\Cache\cursor-cache.bin")));
        Assert.Empty(await _quarantine.ListEntriesAsync());
        // 注册表已恢复
        Assert.Equal("x", await _registryEngine.ReadValueAsync(TempKeyPath, "DeadValue"));
        Assert.Equal(7, await _registryEngine.ReadValueAsync($@"{TempKeyPath}\DeadSub", "Payload"));
    }

    [Fact]
    public async Task Clean_RegistryValueReadback_IsCaseInsensitiveAndRejectsNoOpDeletion()
    {
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative))
        {
            key.SetValue("DeadValue", "still here", RegistryValueKind.String);
        }

        var registry = new NoOpRegistryEngine("DeadValue");
        var cleanEngine = new CleanEngine(
            _quarantine, _backupService, registry, _workspace.PathService, _workspace.Log);

        CleanResult result = await cleanEngine.CleanAsync(new CleanRequest
        {
            Items =
            [
                new ScannedItem($"{TempKeyPath}|deadvalue", 0,
                    SmartScanTargets.RegistryCategoryId, DateTimeOffset.UtcNow),
            ],
            Reason = "case-insensitive readback",
        });

        CleanedItem failed = Assert.Single(result.Items);
        Assert.False(failed.Success);
        Assert.Equal(OperationFailureKind.VerificationFailed, failed.FailureKind);
        Assert.Equal("still here", await _registryEngine.ReadValueAsync(TempKeyPath, "DeadValue"));
    }

    [Fact]
    public async Task Clean_TypedRegistryTargetsSupportPipeCharactersWithoutAmbiguousParsing()
    {
        string keyWithPipe = $@"{TempKeyPath}\Collision|Value";
        string valueKey = $@"{TempKeyPath}\Collision";
        const string valueWithPipe = "Value";
        using (RegistryKey root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_tempKeyRelative))
        {
            root.CreateSubKey("Collision|Value")?.SetValue("Payload", 7, RegistryValueKind.DWord);
            root.CreateSubKey("Collision")?.SetValue(valueWithPipe, "payload", RegistryValueKind.String);
        }

        var cleanEngine = new CleanEngine(
            _quarantine, _backupService, _registryEngine, _workspace.PathService, _workspace.Log);
        CleanResult result = await cleanEngine.CleanAsync(new CleanRequest
        {
            Items =
            [
                new ScannedItem(keyWithPipe, 0, SmartScanTargets.RegistryCategoryId, DateTimeOffset.UtcNow,
                    RegistryKeyPath: keyWithPipe),
                new ScannedItem($"{valueKey}|{valueWithPipe}", 0, SmartScanTargets.RegistryCategoryId,
                    DateTimeOffset.UtcNow, RegistryKeyPath: valueKey, RegistryValueName: valueWithPipe),
            ],
            Reason = "typed registry target",
        });

        Assert.Equal(result.Items[0].Item.Path, result.Items[1].Item.Path);
        Assert.All(result.Items, item => Assert.True(item.Success, item.Error));
        Assert.False(await _registryEngine.KeyExistsAsync(keyWithPipe));
        Assert.Null(await _registryEngine.ReadValueAsync(valueKey, valueWithPipe));
        Assert.True(await cleanEngine.RollbackAsync(result.OperationId));
        Assert.Equal(7, await _registryEngine.ReadValueAsync(keyWithPipe, "Payload"));
        Assert.Equal("payload", await _registryEngine.ReadValueAsync(valueKey, valueWithPipe));
    }

    [Fact]
    public async Task Rollback_UnknownOperation_Throws()
    {
        var cleanEngine = new CleanEngine(_quarantine, _backupService, _registryEngine, _workspace.PathService, _workspace.Log);

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(
            () => cleanEngine.RollbackAsync("no-such-operation"));
    }

    [Fact]
    public async Task Clean_InvalidOperationId_IsRejectedBeforeWritingManifest()
    {
        var cleanEngine = new CleanEngine(_quarantine, _backupService, _registryEngine, _workspace.PathService, _workspace.Log);

        await Assert.ThrowsAsync<ArgumentException>(() => cleanEngine.CleanAsync(
            new CleanRequest { Items = [], Reason = "test", OperationId = "../outside" }));

        string operationsDirectory = Path.Combine(_workspace.PathService.ProgramDataRoot, "Operations");
        Assert.False(Directory.Exists(operationsDirectory));
    }

    [Fact]
    public async Task Clean_SecurityGateBlocked_DoesNotWriteOperationManifest()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var cleanEngine = new CleanEngine(_quarantine, _backupService, _registryEngine,
            _workspace.PathService, _workspace.Log, securityOperationGate: gate);

        await Assert.ThrowsAsync<Core.Diagnostics.LeslieCleanerException>(
            () => cleanEngine.CleanAsync(new CleanRequest { Items = [], Reason = "test" }));

        Assert.False(Directory.Exists(Path.Combine(_workspace.PathService.ProgramDataRoot, "Operations")));
    }

    [Fact]
    public async Task CleanerModule_ReportsHealth()
    {
        TestScanEnvironment environment = BuildFabricatedEnvironment(out _);
        var module = new CleanerModule(environment);

        ModuleHealth health = await module.CheckHealthAsync();

        Assert.Equal("lesliecleaner.cleaner", health.ModuleId);
        Assert.NotEqual(ModuleStatus.Unhealthy, health.Status);
    }

    [Fact]
    public async Task Scan_RecycleBin_CountsAndSizesExactly_IncludingSubdirectories()
    {
        // 模拟回收站结构：$I 元数据 + $R 数据（含子目录内大文件）
        string driveRoot = Path.Combine(_workspace.RootDirectory, "FakeDrive");
        string sid = Path.Combine(driveRoot, "$Recycle.Bin", "S-1-5-21-test");
        _workspace.CreateFile(@"FakeDrive\$Recycle.Bin\S-1-5-21-test\$IAAA1.tmp", new string('m', 500));
        _workspace.CreateFile(@"FakeDrive\$Recycle.Bin\S-1-5-21-test\$IDIR1.tmp", new string('m', 600));
        byte[] bigFile = new byte[(1024 * 1024) + 123];
        File.WriteAllBytes(Path.Combine(sid, "$RAAA1.tmp"), bigFile);
        string subDirectory = Path.Combine(sid, "$RDIR1");
        Directory.CreateDirectory(subDirectory);
        File.WriteAllBytes(Path.Combine(subDirectory, "inner.bin"), new byte[2048]);

        var environment = new TestScanEnvironment().Map("FixedDrives", driveRoot);
        var engine = new SmartScanEngine(environment, registryScanner: null, _workspace.Log);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["RecycleBin"] });

        var recycleItems = result.Items.Where(static i => i.Category == "RecycleBin").ToList();
        long expectedBytes = 500 + 600 + bigFile.Length + 2048;
        Assert.Equal(4, recycleItems.Count);
        Assert.Equal(expectedBytes, recycleItems.Sum(static i => i.SizeBytes));
    }

    [Fact]
    public void SystemScanEnvironment_FixedDrives_ExpandsEveryDriveRecycleBin()
    {
        var environment = new SystemScanEnvironment();

        IReadOnlyList<string> expanded = environment.ExpandDirectories("FixedDrives", "$Recycle.Bin");

        // 与独立实测一致：每个就绪固定盘且存在 $Recycle.Bin 的目录都被覆盖
        var expected = DriveInfo.GetDrives()
            .Where(static d => d is { DriveType: DriveType.Fixed, IsReady: true })
            .Select(static d => Path.Combine(d.RootDirectory.FullName, "$Recycle.Bin"))
            .Where(Directory.Exists)
            .ToList();
        Assert.Equal(expected.Count, expanded.Count);
        Assert.All(expanded, dir => Assert.EndsWith("$Recycle.Bin", dir));
    }

    private TestScanEnvironment BuildFabricatedEnvironment(out string tempRoot)
    {
        tempRoot = Path.Combine(_workspace.RootDirectory, "FakeTemp");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "session1.tmp"), "junk");
        File.WriteAllText(Path.Combine(tempRoot, "session2.tmp"), "junk");

        string localAppData = Path.Combine(_workspace.RootDirectory, "FakeLocalAppData");
        _workspace.CreateFile(@"FakeLocalAppData\Google\Chrome\User Data\Default\Cache\chrome-cache.bin", "cache");

        string roamingAppData = Path.Combine(_workspace.RootDirectory, "FakeRoaming");
        _workspace.CreateFile(@"FakeRoaming\Cursor\Cache\cursor-cache.bin", "cache");

        return new TestScanEnvironment()
            .Map("Temp", tempRoot)
            .Map("LocalAppData", localAppData)
            .Map("RoamingAppData", roamingAppData)
            .Map("Windows", Path.Combine(_workspace.RootDirectory, "FakeWindows"))
            .Map("ProgramData", Path.Combine(_workspace.RootDirectory, "FakeProgramData"))
            .Map("SystemDrive", Path.Combine(_workspace.RootDirectory, "FakeDrive"));
    }

    private sealed class ThrowingRegistryScanner : IRegistryScanner
    {
        public Task<IReadOnlyList<RegistryFinding>> ScanAsync(
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException("test registry access denied");
    }

    private sealed class NoOpRegistryEngine(string valueName) : IRegistryEngine
    {
        public Task<IReadOnlyList<string>> EnumerateSubKeysAsync(
            string keyPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> EnumerateValuesAsync(
            string keyPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([valueName]);

        public Task<object?> ReadValueAsync(
            string keyPath, string valueName = "", CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(null);

        public Task<bool> KeyExistsAsync(
            string keyPath, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task DeleteKeyAsync(
            string keyPath, Guid backupId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteValueAsync(
            string keyPath, string valueName, Guid backupId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

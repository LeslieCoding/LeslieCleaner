using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Browser;
using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Residual;
using LeslieCleaner.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Integration;

/// <summary>
/// 集成测试 1：卸载流程端到端。
/// 模拟已装程序（HKCU Uninstall 键 + 假安装目录 + 假残留 + 假规则）
/// → 发现 → 残留管道（S1 富化 + S2 命中）→ 清理 → 回滚。全部在临时环境进行。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class UninstallFlowIntegrationTests : IAsyncLifetime
{
    private const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly CleanEngine _cleanEngine;
    private readonly InstalledAppDiscovery _discovery;
    private readonly ResidualScannerEngine _residualEngine;
    private readonly string _regKeyName = $"LeslieIntTest{Guid.NewGuid():N}";
    private string _installDir = string.Empty;
    private string _leftoverDir = string.Empty;

    public UninstallFlowIntegrationTests()
    {
        _database = new SqliteDatabase(
            Path.Combine(_workspace.PathService.DataDirectory, "int-uninstall.db"), _workspace.Log);
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var registryEngine = new Infrastructure.Registry.RegistryEngine(backupService, _workspace.Log);
        _cleanEngine = new CleanEngine(_quarantine, backupService, registryEngine, _workspace.PathService, _workspace.Log);
        _discovery = new InstalledAppDiscovery(_workspace.Log);
        _residualEngine = new ResidualScannerEngine(_database,
            new LeslieCleaner.Tests.Uninstaller.FakeFileIndexEngine([]), _cleanEngine, _discovery, _workspace.Log);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        // 假安装目录（模拟残留：卸载后仍存在的文件）
        _installDir = Path.Combine(_workspace.RootDirectory, "IntTestApp");
        _leftoverDir = Path.Combine(_workspace.RootDirectory, "IntTestAppLeftover");
        Directory.CreateDirectory(_installDir);
        Directory.CreateDirectory(_leftoverDir);
        File.WriteAllText(Path.Combine(_leftoverDir, "cache.dat"), "leftover-payload");
        File.WriteAllText(Path.Combine(_leftoverDir, "config.ini"), "leftover-config");

        // 注册 Uninstall 键（已装程序）— HKCU 临时项
        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"{UninstallSubKey}\{_regKeyName}");
        key.SetValue("DisplayName", "LeslieIntTest App", RegistryValueKind.String);
        key.SetValue("Publisher", "IntTestVendor", RegistryValueKind.String);
        key.SetValue("DisplayVersion", "1.0.0", RegistryValueKind.String);
        key.SetValue("InstallLocation", _installDir, RegistryValueKind.String);
        key.SetValue("UninstallString", Path.Combine(_installDir, "unins000.exe"), RegistryValueKind.String);

        // 规则库造规则（S2 数据源）：publisher 精确命中残留目录
        await _database.ExecuteBatchAsync(
            "INSERT OR REPLACE INTO residual_rules (publisher, product, paths, registry) VALUES ($p, '', $paths, '[]');",
            [
                (IReadOnlyList<SqliteParameter>)[
                    new SqliteParameter("$p", "IntTestVendor"),
                    new SqliteParameter("$paths", System.Text.Json.JsonSerializer.Serialize(new[] { _leftoverDir }))],
            ]);
    }

    public async Task DisposeAsync()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"{UninstallSubKey}\{_regKeyName}", throwOnMissingSubKey: false);
        _config.Dispose();
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public async Task FullUninstallFlow_DiscoverResidualCleanRollback()
    {
        // 1. 发现：注册表枚举能找到模拟程序
        IReadOnlyList<InstalledApp> apps = _discovery.EnumerateRegistryApps();
        InstalledApp? app = apps.FirstOrDefault(a => a.Name == "LeslieIntTest App");
        Assert.NotNull(app);
        Assert.Equal("IntTestVendor", app.Publisher);
        Assert.Equal(_installDir, app.InstallLocation);

        // 2. 残留管道：S1 富化 + S2 规则命中
        ResidualScanReport report = await _residualEngine.ScanWithReportAsync(
            new ResidualQuery(app.Publisher, app.Name, app.InstallLocation, AllowDeepScan: false));
        Assert.True(report.StageTimings[0].Duration < TimeSpan.FromMilliseconds(500), "S1 应 <500ms（本机实测）");
        Assert.True(report.StageTimings[1].Duration < TimeSpan.FromMilliseconds(300), "S2 应 <300ms");
        Assert.Contains(report.Items, i => i.Path.Equals(_leftoverDir, StringComparison.OrdinalIgnoreCase)
            && i.Source == ResidualSourceStage.KnownRules);

        // 3. 清理：残留进隔离区
        CleanResult clean = await _residualEngine.CleanResidualsAsync(report.Items);
        Assert.True(clean.Items.All(i => i.Success));
        Assert.False(Directory.EnumerateFiles(_leftoverDir).Any());
        Assert.True((await _quarantine.ListEntriesAsync()).Count >= 2);

        // 4. 回滚：文件还原
        bool rolledBack = await _cleanEngine.RollbackAsync(clean.OperationId);
        Assert.True(rolledBack);
        Assert.Equal(2, Directory.EnumerateFiles(_leftoverDir).Count());
    }
}

/// <summary>
/// 集成测试 2：注册表清理端到端。
/// HKCU 造无效卸载项（DisplayIcon 指向不存在路径）→ 扫描命中 → 备份 → 删除 → 恢复。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class RegistryCleanFlowIntegrationTests : IDisposable
{
    private const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly Infrastructure.Registry.RegistryScanner _scanner;
    private readonly CleanEngine _cleanEngine;
    private readonly QuarantineService _quarantine;
    private readonly string _regKeyName = $"BrokenIntTest{Guid.NewGuid():N}";

    private string RegKeyPath => $@"HKCU\{UninstallSubKey}\{_regKeyName}";

    public RegistryCleanFlowIntegrationTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var backupService = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var registryEngine = new Infrastructure.Registry.RegistryEngine(backupService, _workspace.Log);
        _cleanEngine = new CleanEngine(_quarantine, backupService, registryEngine, _workspace.PathService, _workspace.Log);
        _scanner = new Infrastructure.Registry.RegistryScanner(_workspace.Log);

        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"{UninstallSubKey}\{_regKeyName}");
        key.SetValue("DisplayName", "BrokenIntTest App", RegistryValueKind.String);
        key.SetValue("DisplayIcon", @"C:\DefinitelyMissing\app.exe,0", RegistryValueKind.String);
    }

    public void Dispose()
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"{UninstallSubKey}\{_regKeyName}", throwOnMissingSubKey: false);
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task RegistryCleanFlow_ScanBackupDeleteRestore()
    {
        // 1. 扫描命中（真实全量规则扫描，含 HKCU Uninstall）
        IReadOnlyList<RegistryFinding> findings = await _scanner.ScanAsync();
        RegistryFinding? finding = findings.FirstOrDefault(
            f => f.KeyPath.Equals(RegKeyPath, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(finding);
        Assert.Equal(RegistryIssueKind.InvalidUninstallEntry, finding.Issue);

        // 2. 备份 + 删除
        CleanResult clean = await _cleanEngine.CleanAsync(new CleanRequest
        {
            Items = [new ScannedItem(finding.KeyPath, 0, "Registry", DateTimeOffset.UtcNow)],
            Reason = "集成测试注册表清理",
        });
        Assert.True(clean.Items.All(i => i.Success));
        using (RegistryKey? gone = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{UninstallSubKey}\{_regKeyName}"))
        {
            Assert.Null(gone);
        }

        // 3. 恢复（reg import）
        bool rolledBack = await _cleanEngine.RollbackAsync(clean.OperationId);
        Assert.True(rolledBack);
        using (RegistryKey? restored = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"{UninstallSubKey}\{_regKeyName}"))
        {
            Assert.NotNull(restored);
            Assert.Equal("BrokenIntTest App", restored.GetValue("DisplayName"));
        }
    }
}

/// <summary>
/// 集成测试 3：浏览器清理端到端（伪造 Chromium Profile → 扫描 → 白名单保留 → 清理 → 回滚）。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class BrowserCleanFlowIntegrationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly BrowserCleanerEngine _engine;

    public BrowserCleanFlowIntegrationTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var environment = new Modules.Browser.BrowserEnvironment(
            Path.Combine(_workspace.RootDirectory, "LocalAppData"),
            Path.Combine(_workspace.RootDirectory, "RoamingAppData"));
        _engine = new BrowserCleanerEngine(_workspace.PathService, _quarantine, _config, _workspace.Log,
            environment, static _ => false);

        string profile = Path.Combine(environment.LocalAppData, @"Google\Chrome\User Data\Default");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(environment.LocalAppData, @"Google\Chrome\User Data\Local State"), "{}");
        _workspace.CreateFile(Path.Combine(profile, "Cache", "cache-entry-1"), "cache");
        string cookiesDb = Path.Combine(profile, "Network", "Cookies");
        Directory.CreateDirectory(Path.GetDirectoryName(cookiesDb)!);
        using (var connection = new SqliteConnection($"Data Source={cookiesDb};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE cookies (host_key TEXT);" +
                "INSERT INTO cookies VALUES ('.google.com');" +
                "INSERT INTO cookies VALUES ('.tracker.example');";
            command.ExecuteNonQuery();
        }

        CookiesDbPath = cookiesDb;
    }

    private string CookiesDbPath { get; }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task BrowserCleanFlow_WhitelistCleanRollback()
    {
        // 1. 扫描
        IReadOnlyList<Modules.Browser.BrowserScanItem> items = await _engine.ScanAsync();
        Assert.Contains(items, i => i.DataType == Modules.Browser.BrowserDataType.Cache);
        Assert.Contains(items, i => i.DataType == Modules.Browser.BrowserDataType.Cookies);

        // 2. 清理（白名单保留 + 缓存进隔离区）
        Modules.Browser.BrowserCleanResult result = await _engine.CleanAsync();
        Assert.True(result.CleanedItems > 0);
        Assert.Equal(1, CountCookies(".google.com"));
        Assert.Equal(0, CountCookies(".tracker.example"));
        Assert.Single(await _quarantine.ListEntriesAsync()); // 缓存文件在隔离区

        // 3. 回滚（sqlite 恢复 + 隔离区还原缓存）
        bool rolledBack = await _engine.RollbackAsync(result.OperationId);
        Assert.True(rolledBack);
        Assert.Equal(1, CountCookies(".tracker.example"));

        IReadOnlyList<QuarantineEntry> entries = await _quarantine.ListEntriesAsync();
        Assert.Single(entries);
        string restoredPath = await _quarantine.RestoreAsync(entries[0].Id);
        Assert.True(File.Exists(restoredPath));
    }

    private int CountCookies(string domainSuffix)
    {
        using var connection = new SqliteConnection($"Data Source={CookiesDbPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM cookies WHERE host_key LIKE $suffix;";
        command.Parameters.AddWithValue("$suffix", "%" + domainSuffix);
        return (int)(long)(command.ExecuteScalar() ?? 0L);
    }
}

/// <summary>
/// 集成测试 4：启动项管理端到端（HKCU Run 临时项 → 禁用 → 验证 → 启用 → 验证）。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class StartupFlowIntegrationTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly TempWorkspace _workspace = new();
    private readonly StartupManagerEngine _engine;
    private readonly string _valueName = $"LeslieIntStartup{Guid.NewGuid():N}";

    public StartupFlowIntegrationTests()
    {
        _engine = new StartupManagerEngine(_workspace.PathService, _workspace.Log);
        Cleanup();
        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(_valueName, @"C:\IntTest\app.exe", RegistryValueKind.String);
    }

    public void Dispose()
    {
        Cleanup();
        _workspace.Dispose();
    }

    [Fact]
    public async Task StartupFlow_DisableEnableVerify()
    {
        // 枚举命中
        IReadOnlyList<StartupItem> items = await _engine.EnumerateAsync();
        StartupItem? item = items.FirstOrDefault(i => i.Name == _valueName);
        Assert.NotNull(item);
        Assert.True(item.Enabled);

        // 禁用 → 验证值已移除且出现禁用记录
        StartupChangeResult disabled = await _engine.DisableAsync(item);
        Assert.True(disabled.Success, disabled.Error);
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!)
        {
            Assert.Null(key.GetValue(_valueName));
        }

        IReadOnlyList<StartupItem> afterDisable = await _engine.EnumerateAsync();
        Assert.Contains(afterDisable, i => i.Name == _valueName && !i.Enabled);

        // 启用 → 验证值恢复且禁用记录清除
        StartupChangeResult enabled = await _engine.EnableAsync(item.Id);
        Assert.True(enabled.Success, enabled.Error);
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!)
        {
            Assert.Equal(@"C:\IntTest\app.exe", key.GetValue(_valueName));
        }

        IReadOnlyList<StartupItem> afterEnable = await _engine.EnumerateAsync();
        Assert.Contains(afterEnable, i => i.Name == _valueName && i.Enabled);
    }

    private void Cleanup()
    {
        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}

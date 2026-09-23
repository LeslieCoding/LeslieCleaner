using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Browser;
using LeslieCleaner.Tests.Support;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Tests.Browser;

public sealed class BrowserCleanerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly BrowserEnvironment _environment;

    public BrowserCleanerTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        _environment = new BrowserEnvironment(
            Path.Combine(_workspace.RootDirectory, "LocalAppData"),
            Path.Combine(_workspace.RootDirectory, "RoamingAppData"));
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Locate_FindsChromiumAndFirefoxProfiles()
    {
        FabricateChromeProfile();
        FabricateFirefoxProfile();
        var engine = CreateEngine();

        IReadOnlyList<BrowserProfile> profiles = await engine.LocateProfilesAsync();

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.BrowserId == "chrome" && p.Engine == BrowserEngineKind.Chromium);
        Assert.Contains(profiles, p => p.BrowserId == "firefox" && p.Engine == BrowserEngineKind.Firefox);
    }

    [Fact]
    public async Task Locate_NotInstalled_Skipped()
    {
        var engine = CreateEngine();

        IReadOnlyList<BrowserProfile> profiles = await engine.LocateProfilesAsync();

        Assert.Empty(profiles);
    }

    [Fact]
    public async Task Clean_SecurityGateBlocked_PreservesBrowserData()
    {
        FabricateChromeProfile();
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var engine = new BrowserCleanerEngine(_workspace.PathService, _quarantine, _config, _workspace.Log,
            _environment, static _ => false, gate);

        await Assert.ThrowsAsync<LeslieCleaner.Core.Diagnostics.LeslieCleanerException>(
            () => engine.CleanAsync([BrowserDataType.Cache]));

        Assert.True(Directory.Exists(Path.Combine(ChromeProfileDir(), "Cache")));
    }

    [Fact]
    public async Task Scan_FindsCacheAndDatabases()
    {
        FabricateChromeProfile();
        var engine = CreateEngine();

        IReadOnlyList<BrowserScanItem> items = await engine.ScanAsync();

        Assert.Contains(items, i => i.DataType == BrowserDataType.Cache && i.SizeBytes > 0);
        Assert.Contains(items, i => i.DataType == BrowserDataType.Cookies);
        Assert.Contains(items, i => i.DataType == BrowserDataType.History);
        Assert.Contains(items, i => i.DataType == BrowserDataType.FormData);
    }

    [Fact]
    public async Task Clean_Cookies_KeepsWhitelistedDomains()
    {
        string cookiesDb = FabricateChromeProfile();
        var engine = CreateEngine();

        BrowserCleanResult result = await engine.CleanAsync([BrowserDataType.Cookies]);

        Assert.True(result.CleanedItems > 0);
        Assert.Equal(1, CountRows(cookiesDb, "cookies", "host_key LIKE '%google.com'"));
        Assert.Equal(0, CountRows(cookiesDb, "cookies", "host_key LIKE '%evil-tracker.com'"));
        Assert.Equal(1, CountRows(cookiesDb, "cookies", "host_key LIKE '%github.com'"));
    }

    [Fact]
    public async Task CleanThenRollback_RestoresDatabase()
    {
        string cookiesDb = FabricateChromeProfile();
        var engine = CreateEngine();
        BrowserCleanResult result = await engine.CleanAsync([BrowserDataType.Cookies]);
        Assert.Equal(0, CountRows(cookiesDb, "cookies", "host_key LIKE '%evil-tracker.com'"));

        bool rolledBack = await engine.RollbackAsync(result.OperationId);

        Assert.True(rolledBack);
        Assert.Equal(1, CountRows(cookiesDb, "cookies", "host_key LIKE '%evil-tracker.com'"));
    }

    [Fact]
    public async Task Clean_Cache_MovesFilesToQuarantine()
    {
        FabricateChromeProfile();
        var engine = CreateEngine();

        await engine.CleanAsync([BrowserDataType.Cache]);

        IReadOnlyList<Core.Models.QuarantineEntry> entries = await _quarantine.ListEntriesAsync();
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task Clean_HistoryAndFormData_ClearsRows()
    {
        FabricateChromeProfile();
        string profileDir = ChromeProfileDir();
        var engine = CreateEngine();

        await engine.CleanAsync([BrowserDataType.History, BrowserDataType.DownloadHistory, BrowserDataType.FormData]);

        Assert.Equal(0, CountRows(Path.Combine(profileDir, "History"), "urls", "1=1"));
        Assert.Equal(0, CountRows(Path.Combine(profileDir, "Web Data"), "autofill", "1=1"));
    }

    [Fact]
    public async Task RunningBrowser_IsSkippedAndReported()
    {
        FabricateChromeProfile();
        var engine = CreateEngine(processRunningProbe: static _ => true);

        IReadOnlyList<BrowserScanItem> items = await engine.ScanAsync();
        BrowserCleanResult result = await engine.CleanAsync();

        Assert.All(items, i => Assert.True(i.Skipped));
        Assert.Contains("chrome", result.SkippedBrowsers);
        Assert.Equal(0, result.CleanedItems);
    }

    [Fact]
    public async Task Firefox_Cookies_RespectsWhitelist()
    {
        string cookiesDb = FabricateFirefoxProfile();
        var engine = CreateEngine();

        await engine.CleanAsync([BrowserDataType.Cookies]);

        Assert.Equal(1, CountRows(cookiesDb, "moz_cookies", "host LIKE '%google.com'"));
        Assert.Equal(0, CountRows(cookiesDb, "moz_cookies", "host LIKE '%evil-tracker.com'"));
    }

    [Fact]
    public void AutoCleanRule_DueLogic()
    {
        var rule = new BrowserAutoCleanRule(null, BrowserDataType.Cache, 7, Enabled: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.True(BrowserCleanerEngine.IsRuleDue(rule, null, now));
        Assert.True(BrowserCleanerEngine.IsRuleDue(rule, now.AddDays(-8), now));
        Assert.False(BrowserCleanerEngine.IsRuleDue(rule, now.AddDays(-3), now));
        Assert.False(BrowserCleanerEngine.IsRuleDue(rule with { Enabled = false }, null, now));
    }

    private BrowserCleanerEngine CreateEngine(Func<string, bool>? processRunningProbe = null) =>
        new(_workspace.PathService, _quarantine, _config, _workspace.Log, _environment,
            processRunningProbe ?? (static _ => false));

    private string ChromeProfileDir() =>
        Path.Combine(_environment.LocalAppData, @"Google\Chrome\User Data\Default");

    private string FabricateChromeProfile()
    {
        string userData = Path.Combine(_environment.LocalAppData, @"Google\Chrome\User Data");
        string profile = ChromeProfileDir();
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(userData, "Local State"), "{}");
        _workspace.CreateFile(Path.Combine(profile, "Cache", "f_000001"), "cache-payload");
        _workspace.CreateFile(Path.Combine(profile, "Code Cache", "js-idx"), "cache-payload");

        string cookiesDb = Path.Combine(profile, "Network", "Cookies");
        CreateSqlite(cookiesDb, "CREATE TABLE cookies (host_key TEXT);",
            ["INSERT INTO cookies VALUES ('.google.com');",
             "INSERT INTO cookies VALUES ('.github.com');",
             "INSERT INTO cookies VALUES ('.evil-tracker.com');"]);

        string historyDb = Path.Combine(profile, "History");
        CreateSqlite(historyDb,
            "CREATE TABLE urls (id INTEGER); CREATE TABLE visits (id INTEGER); CREATE TABLE downloads (id INTEGER); CREATE TABLE downloads_url_chains (id INTEGER);",
            ["INSERT INTO urls VALUES (1);", "INSERT INTO downloads VALUES (1);"]);

        string webDataDb = Path.Combine(profile, "Web Data");
        CreateSqlite(webDataDb, "CREATE TABLE autofill (name TEXT);", ["INSERT INTO autofill VALUES ('addr');"]);
        return cookiesDb;
    }

    private string FabricateFirefoxProfile()
    {
        string firefoxRoot = Path.Combine(_environment.RoamingAppData, @"Mozilla\Firefox");
        string profile = Path.Combine(firefoxRoot, @"Profiles\abc123.default-release");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(firefoxRoot, "profiles.ini"),
            "[Profile0]\nName=default-release\nIsRelative=1\nPath=Profiles/abc123.default-release\n");

        string cache2 = Path.Combine(_environment.LocalAppData, @"Mozilla\Firefox\Profiles\abc123.default-release\cache2");
        Directory.CreateDirectory(cache2);
        File.WriteAllText(Path.Combine(cache2, "entry1"), "cache");

        string cookiesDb = Path.Combine(profile, "cookies.sqlite");
        CreateSqlite(cookiesDb, "CREATE TABLE moz_cookies (host TEXT);",
            ["INSERT INTO moz_cookies VALUES ('.google.com');",
             "INSERT INTO moz_cookies VALUES ('.evil-tracker.com');"]);
        return cookiesDb;
    }

    private static void CreateSqlite(string path, string schema, IReadOnlyList<string> seeds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = schema + string.Join(' ', seeds);
        command.ExecuteNonQuery();
    }

    private static int CountRows(string databasePath, string table, string where)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where};";
        return (int)(long)(command.ExecuteScalar() ?? 0L);
    }
}

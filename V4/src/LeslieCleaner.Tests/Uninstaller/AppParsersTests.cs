using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Execution;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Uninstaller;

public sealed class AppParsersTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void ParseUninstallValues_Win32Entry()
    {
        var values = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Test App",
            ["DisplayVersion"] = "1.2.3",
            ["Publisher"] = "Test Vendor",
            ["EstimatedSize"] = 2048,
            ["InstallDate"] = "20240115",
            ["InstallLocation"] = @"C:\Apps\Test",
            ["UninstallString"] = @"C:\Apps\Test\unins000.exe",
        };

        InstalledApp? app = AppParsers.ParseUninstallValues("TestApp", values, "hklm64");

        Assert.NotNull(app);
        Assert.Equal(AppSourceType.Win32, app.Source);
        Assert.Equal(2048L * 1024, app.SizeBytes);
        Assert.Equal(new DateOnly(2024, 1, 15), app.InstallDate);
        Assert.Null(app.ProductCode);
    }

    [Fact]
    public void ParseUninstallValues_GuidKey_IsMsi()
    {
        var values = new Dictionary<string, object?> { ["DisplayName"] = "MSI App" };

        InstalledApp? app = AppParsers.ParseUninstallValues("{12345678-1234-1234-1234-123456789012}", values, "hklm64");

        Assert.NotNull(app);
        Assert.Equal(AppSourceType.Msi, app.Source);
        Assert.Equal("{12345678-1234-1234-1234-123456789012}", app.ProductCode);
    }

    [Fact]
    public void ParseUninstallValues_SystemComponent_Skipped()
    {
        var values = new Dictionary<string, object?>
        {
            ["DisplayName"] = "System Component",
            ["SystemComponent"] = 1,
        };

        Assert.Null(AppParsers.ParseUninstallValues("sys", values, "hklm64"));
        Assert.Null(AppParsers.ParseUninstallValues("empty", new Dictionary<string, object?>(), "hklm64"));
    }

    [Theory]
    [InlineData("20240115", 2024, 1, 15)]
    [InlineData("2023/12/31", 2023, 12, 31)]
    [InlineData(null, null, null, null)]
    [InlineData("garbage", null, null, null)]
    public void ParseInstallDate_Formats(string? raw, int? year, int? month, int? day)
    {
        DateOnly? result = AppParsers.ParseInstallDate(raw);

        if (year is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(new DateOnly(year.Value, month!.Value, day!.Value), result);
        }
    }

    [Fact]
    public void ParseSteamAppManifest_Acf()
    {
        string acf = """
            "AppState"
            {
                "appid"		"730"
                "name"		"Counter-Strike 2"
                "installdir"		"Counter-Strike Global Offensive"
                "SizeOnDisk"		"35000000000"
            }
            """;

        InstalledApp? app = AppParsers.ParseSteamAppManifest(acf, @"D:\SteamLibrary\steamapps");

        Assert.NotNull(app);
        Assert.Equal("steam:730", app.Id);
        Assert.Equal(35000000000L, app.SizeBytes);
        Assert.EndsWith(@"common\Counter-Strike Global Offensive", app.InstallLocation);
        Assert.Equal("steam://uninstall/730", app.UninstallString);
    }

    [Fact]
    public void ParseLibraryFolders_FindsCustomLibraries()
    {
        string mainSteam = Path.Combine(_workspace.RootDirectory, "Steam");
        string customLib = Path.Combine(_workspace.RootDirectory, "CustomLibrary");
        Directory.CreateDirectory(Path.Combine(mainSteam, "steamapps"));
        Directory.CreateDirectory(Path.Combine(customLib, "steamapps"));
        string vdf = "\"libraryfolders\"\n{\n" +
            "    \"0\"\n    {\n        \"path\"\t\t\"" + mainSteam.Replace("\\", "\\\\") + "\"\n    }\n" +
            "    \"1\"\n    {\n        \"path\"\t\t\"" + customLib.Replace("\\", "\\\\") + "\"\n    }\n}\n";

        IReadOnlyList<string> libraries = AppParsers.ParseLibraryFolders(vdf, mainSteam);

        Assert.Equal(2, libraries.Count);
    }

    [Fact]
    public void ParseEpicManifest_ItemJson()
    {
        string json = """
            {
                "DisplayName": "Fortnite",
                "AppName": "Fortnite",
                "AppVersion": "30.00",
                "InstallLocation": "C:\\Games\\Fortnite",
                "InstallSize": 90000000000
            }
            """;

        InstalledApp? app = AppParsers.ParseEpicManifest(json, "Fortnite.item");

        Assert.NotNull(app);
        Assert.Equal(AppSourceType.Epic, app.Source);
        Assert.Equal(90000000000L, app.SizeBytes);
        Assert.Equal("epic:Fortnite", app.Id);
    }

    [Fact]
    public void ParseVisualStudioInstanceState_StateJson()
    {
        string json = """
            {
                "displayName": "Visual Studio Community 2022",
                "installationPath": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community",
                "catalogInfo": { "buildVersion": "17.9.0" }
            }
            """;

        InstalledApp? app = AppParsers.ParseVisualStudioInstanceState(json, "abc123");

        Assert.NotNull(app);
        Assert.Equal(AppSourceType.VisualStudio, app.Source);
        Assert.Equal("17.9.0", app.Version);
    }

    [Fact]
    public void ParseWingetList_Table()
    {
        string output = """
            名称                                     Id                                  版本
            -----------------------------------------------------------------------------------
            Google Chrome                            Google.Chrome                       120.0.0
            Visual Studio Code                       Microsoft.VisualStudioCode          1.85.0
            """;

        IReadOnlyList<InstalledApp> apps = AppParsers.ParseWingetList(output);

        Assert.Equal(2, apps.Count);
        Assert.Equal("winget:Google.Chrome", apps[0].Id);
        Assert.Equal(AppSourceType.Winget, apps[0].Source);
    }

    [Fact]
    public void ClassifyBySignature_Recategorizes()
    {
        var adobe = new InstalledApp("x", "Adobe Photoshop 2024", null, "Adobe Inc.", 0, null, null, null, AppSourceType.Win32, null, null);
        var docker = new InstalledApp("y", "Docker Desktop", null, "Docker Inc", 0, null, null, null, AppSourceType.Win32, null, null);
        var node = new InstalledApp("z", "Node.js", null, "Node.js Foundation", 0, null, null, null, AppSourceType.Win32, null, null);
        var plain = new InstalledApp("w", "Plain App", null, "Unknown", 0, null, null, null, AppSourceType.Win32, null, null);

        Assert.Equal(AppSourceType.Adobe, AppParsers.ClassifyBySignature(adobe).Source);
        Assert.Equal(AppSourceType.DockerWsl, AppParsers.ClassifyBySignature(docker).Source);
        Assert.Equal(AppSourceType.DevEnvironment, AppParsers.ClassifyBySignature(node).Source);
        Assert.Equal(AppSourceType.Win32, AppParsers.ClassifyBySignature(plain).Source);
    }

    [Theory]
    [InlineData("\"C:\\Apps\\unins000.exe\" /SILENT", "C:\\Apps\\unins000.exe", "/SILENT")]
    [InlineData("C:\\Apps\\uninstall.exe -q", "C:\\Apps\\uninstall.exe", "-q")]
    [InlineData("MsiExec.exe /X{GUID}", "MsiExec.exe", "/X{GUID}")]
    public void SplitCommandLine_Parses(string commandLine, string expectedFile, string expectedArgs)
    {
        (string fileName, string arguments) = UninstallExecutor.SplitCommandLine(commandLine);

        Assert.Equal(expectedFile, fileName);
        Assert.Equal(expectedArgs, arguments);
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class InstalledAppDiscoveryTests
{
    private readonly TempWorkspace _workspace = new();
    private readonly InstalledAppDiscovery _discovery;

    public InstalledAppDiscoveryTests() =>
        _discovery = new InstalledAppDiscovery(_workspace.Log);

    [Fact]
    public void EnumerateRegistryApps_ReturnsRealApps()
    {
        IReadOnlyList<InstalledApp> apps = _discovery.EnumerateRegistryApps();

        Assert.NotEmpty(apps);
        Assert.All(apps, app => Assert.False(string.IsNullOrWhiteSpace(app.Name)));
    }

    [Fact]
    public async Task EnumerateAll_CompletesWithMultipleSources()
    {
        IReadOnlyList<InstalledApp> apps = await _discovery.EnumerateAllAsync();

        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a.Source is AppSourceType.Win32 or AppSourceType.Msi);
    }
}

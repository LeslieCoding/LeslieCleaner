using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;
using Microsoft.Win32;

namespace LeslieCleaner.Tests.Startup;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class StartupManagerTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string TestValueName = "LeslieCleanerTest_Item";

    private readonly TempWorkspace _workspace = new();
    private readonly StartupManagerEngine _engine;

    public StartupManagerTests()
    {
        _engine = new StartupManagerEngine(_workspace.PathService, _workspace.Log);
        CleanupTestValue();
        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(TestValueName, @"C:\Temp\leslie-test.exe --flag", RegistryValueKind.String);
    }

    public void Dispose()
    {
        CleanupTestValue();
        _workspace.Dispose();
    }

    [Fact]
    public async Task DisableEnable_RegistryRun_FullLoop()
    {
        IReadOnlyList<StartupItem> items = await _engine.EnumerateAsync();
        StartupItem? target = items.FirstOrDefault(i => i.Name == TestValueName);
        Assert.NotNull(target);
        Assert.Equal(StartupItemKind.RegistryRun, target.Kind);
        Assert.True(target.Enabled);

        StartupChangeResult disabled = await _engine.DisableAsync(target);
        Assert.True(disabled.Success, disabled.Error);
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!)
        {
            Assert.Null(key.GetValue(TestValueName));
        }

        IReadOnlyList<StartupItem> afterDisable = await _engine.EnumerateAsync();
        Assert.Contains(afterDisable, i => i.Name == TestValueName && !i.Enabled);

        StartupChangeResult enabled = await _engine.EnableAsync(target.Id);
        Assert.True(enabled.Success, enabled.Error);
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!)
        {
            Assert.Equal(@"C:\Temp\leslie-test.exe --flag", key.GetValue(TestValueName));
        }
    }

    [Fact]
    public async Task Disable_SecurityGateBlocked_DoesNotModifyRegistryRunItem()
    {
        StartupItem target = (await _engine.EnumerateAsync()).Single(item => item.Name == TestValueName);
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var guarded = new StartupManagerEngine(_workspace.PathService, _workspace.Log, gate);

        StartupChangeResult result = await guarded.DisableAsync(target);

        Assert.False(result.Success);
        Assert.Contains("安全检查", result.Error, StringComparison.Ordinal);
        using RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!;
        Assert.NotNull(key.GetValue(TestValueName));
    }

    [Fact]
    public async Task DisableEnable_RegistryRun_PreservesExpandStringKind()
    {
        using (RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath))
        {
            key.SetValue(TestValueName, @"%LOCALAPPDATA%\LeslieCleaner\agent.exe", RegistryValueKind.ExpandString);
        }

        StartupItem target = (await _engine.EnumerateAsync()).Single(item => item.Name == TestValueName);
        Assert.True((await _engine.DisableAsync(target)).Success);
        Assert.True((await _engine.EnableAsync(target.Id)).Success);

        using RegistryKey restored = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath)!;
        Assert.Equal(RegistryValueKind.ExpandString, restored.GetValueKind(TestValueName));
        Assert.Equal(@"%LOCALAPPDATA%\LeslieCleaner\agent.exe",
            restored.GetValue(TestValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
    }

    [Fact]
    public async Task Enable_UnknownItem_Fails()
    {
        StartupChangeResult result = await _engine.EnableAsync("run:nowhere:nothing");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Enumerate_ScheduledTasks_ReadOnly()
    {
        IReadOnlyList<StartupItem> items = await _engine.EnumerateAsync();

        Assert.Contains(items, i => i.Kind == StartupItemKind.ScheduledTask);
    }

    [Fact]
    public async Task Enumerate_ServicesAndDrivers_Classified()
    {
        IReadOnlyList<StartupItem> items = await _engine.EnumerateAsync();

        Assert.Contains(items, i => i.Kind == StartupItemKind.Service);
        Assert.Contains(items, i => i.Kind == StartupItemKind.Driver);
    }

    [Fact]
    public void Scorer_MicrosoftItem_IsLow()
    {
        Assert.Equal(StartupImpact.Low,
            StartupImpactScorer.Score("OneDrive Stub", null, "Microsoft Corporation", StartupItemKind.RegistryRun));
        string windowsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "x.exe");
        Assert.Equal(StartupImpact.Low,
            StartupImpactScorer.Score("Sys", windowsPath, null, StartupItemKind.RegistryRun));
    }

    [Fact]
    public void Scorer_KnownHighImpact_IsHigh()
    {
        Assert.Equal(StartupImpact.High,
            StartupImpactScorer.Score("Steam", @"C:\Games\steam.exe", null, StartupItemKind.RegistryRun));
        Assert.True(StartupImpactScorer.IsKnownHighImpact(@"""D:\Apps\WeChat.exe"""));
    }

    [Fact]
    public void Scorer_StartupFolderThirdParty_IsHigh()
    {
        Assert.Equal(StartupImpact.High,
            StartupImpactScorer.Score("Tool", @"C:\Tools\tool.exe", "Small Vendor", StartupItemKind.StartupFolder));
    }

    [Fact]
    public void Suggestion_CitesRealEvidence()
    {
        var item = new StartupItem(
            "run:x:Steam", "Steam", StartupItemKind.RegistryRun, @"HKCU\...\Run",
            @"C:\Games\steam.exe", "Valve", true, StartupImpact.High, string.Empty);

        string suggestion = StartupImpactScorer.BuildSuggestion(item);

        Assert.Contains("steam.exe", suggestion);
        Assert.Contains("名单", suggestion);
    }

    private static void CleanupTestValue()
    {
        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(TestValueName, throwOnMissingValue: false);
    }
}

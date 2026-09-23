using System.Globalization;
using LeslieCleaner.Core.Models;
using LeslieCleaner.UI.Localization;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.Theming;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.Tests.UI;

/// <summary>V4 UI、可访问性、本地化与安装器代码级验收。</summary>
public sealed class UiInstallerAcceptanceTests
{
    private static readonly object CultureGate = new();

    [Fact]
    public async Task Navigation_CancelsPreviousActivation_AndKeepsNewestStatus()
    {
        var first = new BlockingPage();
        var vm = new MainViewModel();
        vm.RegisterPages([
            new NavigationItem("第一页", "one", () => first),
            new NavigationItem("第二页", "two", static () => new object()),
        ]);
        await first.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        vm.NavigateTo("第二页");
        await vm.LastActivationTask;
        await first.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("第二页已就绪", vm.NavigationStatus);
        Assert.Null(vm.NavigationError);
        Assert.False(vm.IsNavigationBusy);
    }

    [Fact]
    public async Task Navigation_ActivationFailure_IsPresentedWithoutThrowing()
    {
        var vm = new MainViewModel();
        vm.RegisterPages([new NavigationItem("故障页", "warning", static () => new FaultingPage())]);

        await vm.LastActivationTask;

        Assert.Contains("测试故障", vm.NavigationError, StringComparison.Ordinal);
        Assert.Contains("可切换页面后重试", vm.NavigationStatus, StringComparison.Ordinal);
        Assert.False(vm.IsNavigationBusy);
    }

    [Fact]
    public void ThemeResolution_HighContrastOverridesUserTheme()
    {
        Assert.Equal("Colors.HighContrast.xaml",
            ThemeService.ResolveThemeResourceName(ThemeMode.Light, systemLight: true, highContrast: true));
        Assert.Equal("Colors.HighContrast.xaml",
            ThemeService.ResolveThemeResourceName(ThemeMode.Dark, systemLight: false, highContrast: true));
        Assert.Equal("Colors.Dark.xaml",
            ThemeService.ResolveThemeResourceName(ThemeMode.Auto, systemLight: false, highContrast: false));
        Assert.Equal("Colors.Light.xaml",
            ThemeService.ResolveThemeResourceName(ThemeMode.Auto, systemLight: true, highContrast: false));
    }

    [Fact]
    public void Resx_LocalizesCorePlatformActions_AndFallsBackToNeutral()
    {
        lock (CultureGate)
        {
            CultureInfo? original = UiText.Culture;
            try
            {
                UiText.Culture = CultureInfo.GetCultureInfo("en-US");
                Assert.Equal("Platform Center", UiText.PlatformCenterTitle);
                Assert.Equal("Check for updates", UiText.CheckUpdates);

                UiText.Culture = CultureInfo.GetCultureInfo("zh-CN");
                Assert.Equal("平台中心", UiText.PlatformCenterTitle);
                Assert.Equal("重新评估", UiText.Reassess);
            }
            finally
            {
                UiText.Culture = original;
            }
        }
    }

    [Fact]
    public void PlatformXaml_HasResponsiveAndAccessibilityContracts()
    {
        string root = FindV4Root();
        string platform = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "Views", "PlatformCenterPage.xaml"));
        string main = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.App", "MainWindow.xaml"));

        Assert.Contains("AutomationProperties.Name", platform, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting", platform, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"980\"", platform, StringComparison.Ordinal);
        Assert.DoesNotContain("UniformGrid Columns=\"2\"", platform, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"800\"", main, StringComparison.Ordinal);
        Assert.Contains("NavigationStatus", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_DeclaresTwoModesUpgradeSafetyAndDataChoice()
    {
        string script = File.ReadAllText(Path.Combine(FindV4Root(), "installer", "LeslieCleaner.iss"));

        Assert.Contains("PrivilegesRequired=lowest", script, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequiredOverridesAllowed=dialog commandline", script, StringComparison.Ordinal);
        Assert.Contains("[Dirs]", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"{commonappdata}\\LeslieCleaner\"; Permissions: users-modify", script,
            StringComparison.Ordinal);
        Assert.Contains("IsInstallPathValid", script, StringComparison.Ordinal);
        Assert.Contains("HasEnoughSpace", script, StringComparison.Ordinal);
        Assert.Contains("GetSpaceOnDisk64(DriveRoot, FreeBytes, TotalBytes)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSpaceOnDisk64(DriveRoot, False", script, StringComparison.Ordinal);
        Assert.Contains("BackupUpgradeData", script, StringComparison.Ordinal);
        Assert.Contains("PrepareToInstall", script, StringComparison.Ordinal);
        Assert.Contains("ProvisionIntegrityBaseline", script, StringComparison.Ordinal);
        Assert.Contains("--provision-integrity-baseline", script, StringComparison.Ordinal);
        Assert.Contains("ewWaitUntilTerminated", script, StringComparison.Ordinal);
        Assert.Contains("ResultCode <> 0", script, StringComparison.Ordinal);
        Assert.Contains("InitializeUninstall", script, StringComparison.Ordinal);
        Assert.Contains("if UninstallSilent then", script, StringComparison.Ordinal);
        Assert.Contains("RemoveUserData := False", script, StringComparison.Ordinal);
        Assert.Contains("RemoveUserData", script, StringComparison.Ordinal);
        Assert.Contains("不会删除 LeslieCleaner 数据目录以外的个人文件", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmartScanXaml_DisclosesDualAccountingAndIncompleteCleanup()
    {
        string xaml = File.ReadAllText(Path.Combine(FindV4Root(), "src", "LeslieCleaner.UI", "Views", "SmartScanPage.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(FindV4Root(), "src", "LeslieCleaner.UI", "ViewModels", "SmartScanViewModel.cs"));

        Assert.Contains("MeasurementDetails", xaml, StringComparison.Ordinal);
        Assert.Contains("PostCleanVerificationText", xaml, StringComparison.Ordinal);
        Assert.Contains("CleanFailures", xaml, StringComparison.Ordinal);
        Assert.Contains("需要管理员权限", xaml, StringComparison.Ordinal);
        Assert.Contains("统计口径差异", viewModel, StringComparison.Ordinal);
        Assert.Contains("TotalAllocatedSizeBytes", viewModel, StringComparison.Ordinal);
        Assert.Contains("失败项目未计入已释放空间", viewModel, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Access denied", true, "管理员权限")]
    [InlineData("sharing violation", false, "占用")]
    [InlineData("protected by integrity policy", false, "受系统保护")]
    [InlineData("verification failed", false, "复验")]
    public void CleanFailurePresenter_ClassifiesActionableCauses(string error, bool elevation, string expected)
    {
        CleanFailurePresentation result = CleanFailurePresenter.Describe(error);

        Assert.Equal(elevation, result.RequiresElevation);
        Assert.Contains(expected, result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrityPlanner_RequiresAndCoversAllFiveArtifactKinds()
    {
        string root = Path.Combine(Path.GetTempPath(), "LeslieCleaner-UiAcceptance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string file in new[]
                     {
                         "LeslieCleaner.App.exe", "LeslieCleaner.Core.dll", "LeslieCleaner.App.runtimeconfig.json",
                         "LeslieCleaner.Data.dll", "LeslieCleaner.PluginSdk.dll",
                     })
            {
                File.WriteAllText(Path.Combine(root, file), "test");
            }

            IReadOnlyList<TrustedIntegrityTarget> targets = IntegrityTargetPlanner.Build(root);
            Assert.Equal(Enum.GetValues<IntegrityArtifactKind>().Order(), targets.Select(static item => item.Kind).Order());
            Assert.Equal(5, targets.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PlatformAndStartupXaml_ExposeExplicitRecoveryFlows()
    {
        string root = FindV4Root();
        string platform = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "Views", "PlatformCenterPage.xaml"));
        string startup = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "Views", "StartupPage.xaml"));
        string startupViewModel = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "ViewModels", "StartupViewModel.cs"));
        string registry = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "Views", "RegistryPage.xaml"));
        string registryViewModel = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.UI", "ViewModels", "RegistryViewModel.cs"));

        Assert.Contains("EstablishTrustedBaselineCommand", platform, StringComparison.Ordinal);
        Assert.Contains("RepairFromTrustedBackupCommand", platform, StringComparison.Ordinal);
        Assert.Contains("绝不会自动把当前文件标记为可信", platform, StringComparison.Ordinal);
        Assert.Contains("RestartAsAdministratorCommand", startup, StringComparison.Ordinal);
        Assert.Contains("result.RequiresElevation", startupViewModel, StringComparison.Ordinal);
        Assert.Contains("StartupResumeTarget.ArgumentName", startupViewModel, StringComparison.Ordinal);
        Assert.Contains("RestoreSelection", startupViewModel, StringComparison.Ordinal);
        Assert.Contains("RestartAsAdministratorCommand", registry, StringComparison.Ordinal);
        Assert.Contains("ResumeAfterElevationAsync", registryViewModel, StringComparison.Ordinal);
        Assert.Contains("if (IsBusy)", registryViewModel, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanFix)", registryViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void UsbDebugProbe_DistinguishesMissingToolsAndSupportsProjectLocalPlatformTools()
    {
        string root = FindV4Root();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "Test-UsbDebug.ps1"));

        Assert.Contains("tools\\android-platform-tools\\platform-tools", script, StringComparison.Ordinal);
        Assert.Contains("$debugUsbDevices.Count -gt 0 -and -not $adb", script, StringComparison.Ordinal);
        Assert.Contains("'DebugToolsMissing'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryPrivilegeProbe_RequiresExplicitGateAndUsesOnlyRandomTestSubtree()
    {
        string root = FindV4Root();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "Test-RegistryPrivileges.ps1"));
        string acceptance = File.ReadAllText(Path.Combine(root, "src", "LeslieCleaner.Tests", "Registry",
            "RegistryPrivilegedAcceptanceTests.cs"));

        Assert.Contains("NeedsElevation", script, StringComparison.Ordinal);
        Assert.Contains("LESLIECLEANER_RUN_PRIVILEGED_ACCEPTANCE", script, StringComparison.Ordinal);
        Assert.Contains("Category=Privileged", script, StringComparison.Ordinal);
        Assert.Contains("LeslieCleaner.PrivilegedAcceptance.Tests", acceptance, StringComparison.Ordinal);
        Assert.DoesNotContain("HKCR\\CLSID", acceptance, StringComparison.OrdinalIgnoreCase);
    }
    private static string FindV4Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LeslieCleaner.V4.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Unable to locate V4 solution root from test output.");
    }

    private sealed class BlockingPage : ICancelablePageActivatable
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task OnActivatedAsync() => OnActivatedAsync(CancellationToken.None);
        public async Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Canceled.TrySetResult(true);
                throw;
            }
        }
    }

    private sealed class FaultingPage : IPageActivatable
    {
        public Task OnActivatedAsync() => throw new InvalidOperationException("测试故障");
    }
}

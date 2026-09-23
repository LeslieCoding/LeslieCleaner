using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.Modules.Browser;
using LeslieCleaner.UI.Theming;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>
/// 设置视图模型：主题（即时生效+持久化）、常规（开机启动）、
/// 清理设置（隔离区保留天数、Cookie 白名单）、维护计划、关于。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly IConfigService _configService;
    private readonly ILazyModuleActivator _activator;
    private readonly ILogService _logService;

    private MaintenancePlanner? _maintenancePlanner;

    private MaintenancePlanner MaintenancePlanner => _maintenancePlanner ??=
        _activator.Create<MaintenancePlanner>(ModuleRefs.AiAssembly, ModuleRefs.MaintenancePlannerType);

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _launchAtStartup;

    [ObservableProperty]
    private int _retentionDays;

    [ObservableProperty]
    private string _cookieWhitelistText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>维护计划集合。</summary>
    public ObservableCollection<MaintenancePlan> Plans { get; } = [];

    /// <summary>应用版本（读取程序集信息版本）。</summary>
    public string AppVersion { get; } =
        $"LeslieCleaner v{(System.Reflection.AssemblyInformationalVersionAttribute.GetCustomAttribute(
            System.Reflection.Assembly.GetExecutingAssembly(),
            typeof(System.Reflection.AssemblyInformationalVersionAttribute))
            as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion ?? "1.0.0"}";

    /// <summary>以主题服务、配置服务与懒激活器创建视图模型。</summary>
    public SettingsViewModel(
        ThemeService themeService,
        IConfigService configService,
        ILazyModuleActivator activator,
        ILogService logService)
    {
        _themeService = themeService;
        _configService = configService;
        _activator = activator;
        _logService = logService;

        _selectedThemeIndex = themeService.Mode switch
        {
            ThemeMode.Light => 0,
            ThemeMode.Dark => 1,
            _ => 2,
        };
        _retentionDays = _configService.GetValue("quarantine:retentionDays", 30);
        _launchAtStartup = _configService.GetValue("general:launchAtStartup", false);
        IReadOnlyList<string> whitelist =
            _configService.GetValue(BrowserCleanerEngine.CookieWhitelistConfigKey, BrowserCatalog.DefaultCookieWhitelist);
        _cookieWhitelistText = string.Join(Environment.NewLine, whitelist);
        LoadPlans();
    }

    /// <summary>主题切换（即时生效+持久化）。</summary>
    partial void OnSelectedThemeIndexChanged(int value)
    {
        ThemeMode mode = value switch
        {
            0 => ThemeMode.Light,
            1 => ThemeMode.Dark,
            _ => ThemeMode.Auto,
        };
        _ = _themeService.SetModeAsync(mode);
    }

    /// <summary>开机启动切换（写注册表 Run 键）。</summary>
    partial void OnLaunchAtStartupChanged(bool value)
    {
        try
        {
            using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (value)
            {
                key.SetValue("LeslieCleaner", $"\"{Environment.ProcessPath}\"", Microsoft.Win32.RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue("LeslieCleaner", throwOnMissingValue: false);
            }

            _ = _configService.SetValueAsync("general:launchAtStartup", value, CancellationToken.None);
            StatusText = value ? "已设置开机启动" : "已取消开机启动";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "SettingsViewModel.LaunchAtStartup");
            StatusText = "设置失败（详见日志）";
        }
    }

    /// <summary>保存清理设置。</summary>
    [RelayCommand]
    private async Task SaveCleanupSettingsAsync()
    {
        try
        {
            await _configService.SetValueAsync("quarantine:retentionDays", RetentionDays, CancellationToken.None);
            IReadOnlyList<string> whitelist =
            [
                .. CookieWhitelistText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            await _configService.SetValueAsync(
                BrowserCleanerEngine.CookieWhitelistConfigKey, whitelist, CancellationToken.None);
            StatusText = "清理设置已保存（即时生效）";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "SettingsViewModel.SaveCleanupSettingsAsync");
            StatusText = "保存失败（详见日志）";
        }
    }

    /// <summary>新增每周维护计划。</summary>
    [RelayCommand]
    private async Task AddWeeklyPlanAsync()
    {
        var plans = new List<MaintenancePlan>(MaintenancePlanner.GetPlans());
        if (plans.Any(static p => p.Name == "每周自动维护"))
        {
            StatusText = "每周自动维护计划已存在";
            return;
        }

        plans.Add(new MaintenancePlan(
            "每周自动维护", 7,
            ["lesliecleaner.cleaner", "lesliecleaner.browser", "lesliecleaner.privacy"],
            Enabled: true, LastRunUtc: null));
        await MaintenancePlanner.SavePlansAsync(plans);
        LoadPlans();
        StatusText = "已创建每周自动维护计划";
    }

    private void LoadPlans()
    {
        Plans.Clear();
        foreach (MaintenancePlan plan in MaintenancePlanner.GetPlans())
        {
            Plans.Add(plan);
        }
    }
}

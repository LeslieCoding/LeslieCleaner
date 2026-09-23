using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>AI、插件和更新的统一平台中心视图模型。</summary>
public sealed partial class PlatformCenterViewModel : ObservableObject, IPageActivatable
{
    private readonly IPluginManager _pluginManager;
    private readonly IUpdateManager _updateManager;
    private readonly ISecurityManager _securityManager;
    private readonly ILazyModuleActivator _activator;

    [ObservableProperty]
    private string _pluginStatus = "尚未检查插件目录。";

    [ObservableProperty]
    private string _updateStatus = "尚未检查更新。";

    [ObservableProperty]
    private string _securityStatus = "插件默认禁用；启用前必须通过 SHA-256 校验。";

    /// <summary>经验证的插件清单。</summary>
    public ObservableCollection<PluginDescriptor> Plugins { get; } = [];

    /// <summary>AI 维护计划。</summary>
    public ObservableCollection<MaintenancePlan> AiPlans { get; } = [];

    /// <summary>创建平台中心。</summary>
    public PlatformCenterViewModel(
        IPluginManager pluginManager,
        IUpdateManager updateManager,
        ISecurityManager securityManager,
        ILazyModuleActivator activator)
    {
        _pluginManager = pluginManager;
        _updateManager = updateManager;
        _securityManager = securityManager;
        _activator = activator;
    }

    /// <inheritdoc />
    public async Task OnActivatedAsync()
    {
        await RefreshPluginsAsync();
        await RefreshSecurityAsync();
        LoadAiPlans();
    }

    /// <summary>重新发现并校验插件。</summary>
    [RelayCommand]
    private async Task RefreshPluginsAsync()
    {
        IReadOnlyList<PluginDescriptor> plugins = await _pluginManager.DiscoverAsync();
        Plugins.Clear();
        foreach (PluginDescriptor plugin in plugins)
        {
            Plugins.Add(plugin);
        }

        int ready = plugins.Count(static plugin => plugin.TrustStatus == PluginTrustStatus.Ready);
        int rejected = plugins.Count(static plugin => plugin.TrustStatus is not PluginTrustStatus.Ready and not PluginTrustStatus.Disabled);
        PluginStatus = $"已发现 {plugins.Count} 个插件：启用 {ready}，拒绝 {rejected}。";
    }

    /// <summary>切换一个已验证插件的启用状态。</summary>
    [RelayCommand]
    private async Task TogglePluginAsync(PluginDescriptor? plugin)
    {
        if (plugin is null || plugin.TrustStatus is not (PluginTrustStatus.Ready or PluginTrustStatus.Disabled))
        {
            return;
        }

        await _pluginManager.SetEnabledAsync(plugin.Id, plugin.TrustStatus != PluginTrustStatus.Ready);
        await RefreshPluginsAsync();
    }

    /// <summary>执行只读更新检查。</summary>
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        UpdateStatus status = await _updateManager.CheckAsync();
        UpdateStatus = $"当前 {status.CurrentVersion}。{status.Message}";
    }

    /// <summary>执行应用完整性、审计链和插件信任评估。</summary>
    [RelayCommand]
    private async Task RefreshSecurityAsync()
    {
        SecurityAssessment assessment = await _securityManager.AssessAsync();
        SecurityStatus = $"安全评分 {assessment.Score}/100。{assessment.Summary}";
    }

    /// <summary>加载已保存的、可解释的 AI 维护计划。</summary>
    [RelayCommand]
    private void LoadAiPlans()
    {
        var planner = _activator.Create<MaintenancePlanner>(ModuleRefs.AiAssembly, ModuleRefs.MaintenancePlannerType);
        AiPlans.Clear();
        foreach (MaintenancePlan plan in planner.GetPlans())
        {
            AiPlans.Add(plan);
        }
    }
}

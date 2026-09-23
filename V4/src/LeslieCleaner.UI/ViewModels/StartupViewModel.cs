using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Modules.Startup.Classification;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>分类节点（左侧树）。</summary>
public sealed partial class StartupCategoryNode : ObservableObject
{
    /// <summary>来源分类。</summary>
    public StartupSourceKind Kind { get; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; }

    /// <summary>图标字形。</summary>
    public string Icon { get; }

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>以分类、显示名与默认展开状态创建节点。</summary>
    public StartupCategoryNode(StartupSourceKind kind, string displayName, string icon, bool isExpanded)
    {
        Kind = kind;
        DisplayName = displayName;
        Icon = icon;
        _isExpanded = isExpanded;
    }
}

/// <summary>启动项行（含图标缓存）。</summary>
public sealed partial class StartupItemRow : ObservableObject, ISelectableRow
{
    /// <summary>详情数据。</summary>
    public StartupItemDetail Detail { get; }

    /// <summary>提取的文件图标；无图标为 <see langword="null"/>。</summary>
    public ImageSource? Icon { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <inheritdoc />
    bool ISelectableRow.IsChecked
    {
        get => IsSelected;
        set => IsSelected = value;
    }

    /// <summary>以详情创建行。</summary>
    public StartupItemRow(StartupItemDetail detail)
    {
        Detail = detail;
        Icon = StartupIconService.GetIcon(detail.FilePath);
    }

    /// <summary>星级文本（★★★★☆）。</summary>
    public string StarsText => new string('★', Detail.Impact.Stars) + new string('☆', 5 - Detail.Impact.Stars);

    /// <summary>影响等级中文。</summary>
    public string ImpactLevelText => Detail.Impact.Level switch
    {
        StartupImpactLevel.Critical => "极高",
        StartupImpactLevel.High => "高",
        StartupImpactLevel.Medium => "中",
        StartupImpactLevel.Low => "低",
        _ => "极低",
    };

    /// <summary>分类中文。</summary>
    public string KindText => Detail.Classification.Kind switch
    {
        StartupSourceKind.MicrosoftWindows => "Microsoft Windows",
        StartupSourceKind.HardwareDriver => "驱动/OEM",
        StartupSourceKind.UserApplication => "用户软件",
        _ => "未知",
    };

    /// <summary>分类徽章画刷资源键。</summary>
    public string KindBrushKey => Detail.Classification.Kind switch
    {
        StartupSourceKind.MicrosoftWindows => "PrimaryBrush",
        StartupSourceKind.HardwareDriver => "SuccessBrush",
        StartupSourceKind.UserApplication => "WarningBrush",
        _ => "DangerBrush",
    };

    /// <summary>签名状态中文。</summary>
    public string SignatureText => Detail.Signature.IsValid
        ? (StartupClassifier.IsMicrosoftSigner(Detail.Signature.SignerSubject)
            ? "Microsoft 已验证" : "已签名")
        : "未签名/无效";
}

/// <summary>
/// Startup Manager v2 视图模型：四级分类树 + 统计卡片 + 详情列表 +
/// 保护式启停（系统项三后果警告默认取消 / 驱动硬件提示）+ 批量启停 +
/// 搜索/排序/筛选 + 底部三张估算分析图。
/// </summary>
public sealed partial class StartupViewModel : ObservableObject, IPageActivatable
{
    private readonly ILazyModuleActivator _activator;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    private readonly List<StartupItemDetail> _allDetails = [];
    private StartupManagerEngine? _engine;

    private StartupManagerEngine Engine => _engine ??=
        _activator.Create<StartupManagerEngine>(ModuleRefs.StartupAssembly, ModuleRefs.StartupManagerEngineType);

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "加载启动项…";

    [ObservableProperty]
    private bool _hasElevationRequired;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedSortIndex;

    [ObservableProperty]
    private StartupCategoryNode? _selectedCategory;

    [ObservableProperty]
    private StartupItemRow? _selectedItem;

    [ObservableProperty]
    private int _systemCount;

    [ObservableProperty]
    private int _appCount;

    [ObservableProperty]
    private int _driverCount;

    [ObservableProperty]
    private int _unknownCount;

    [ObservableProperty]
    private string _optimizableText = "—";

    [ObservableProperty]
    private string _aiSummary = "—";

    [ObservableProperty]
    private IReadOnlyList<BarChartPoint>? _timePoints;

    [ObservableProperty]
    private IReadOnlyList<BarChartPoint>? _cpuPoints;

    [ObservableProperty]
    private IReadOnlyList<BarChartPoint>? _memoryPoints;

    /// <summary>分类节点集合（系统默认折叠、用户软件默认展开）。</summary>
    public ObservableCollection<StartupCategoryNode> Categories { get; } =
    [
        new(StartupSourceKind.MicrosoftWindows, "Microsoft Windows（系统）", "🛡", isExpanded: false),
        new(StartupSourceKind.HardwareDriver, "Hardware Drivers（驱动）", "🔧", isExpanded: false),
        new(StartupSourceKind.UserApplication, "User Applications（用户软件）", "📦", isExpanded: true),
        new(StartupSourceKind.StartupUnknown, "Unknown（未知）", "⚠", isExpanded: false),
    ];

    /// <summary>启动项行集合。</summary>
    public ObservableCollection<StartupItemRow> Items { get; } = [];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>启动项视图（搜索/分类过滤）。</summary>
    public ICollectionView ItemsView { get; }

    /// <summary>以懒激活器、对话框服务与日志服务创建视图模型。</summary>
    public StartupViewModel(ILazyModuleActivator activator, DialogService dialogService, ILogService logService)
    {
        _activator = activator;
        _dialogService = dialogService;
        _logService = logService;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FilterItem;
        SelectedCategory = Categories[2];
    }

    /// <inheritdoc />
    public async Task OnActivatedAsync() => await LoadCoreAsync(includeFileVerification: false);

    /// <summary>加载并聚合全部启动项详情。</summary>
    [RelayCommand]
    private Task LoadAsync() => LoadCoreAsync(includeFileVerification: false);

    [RelayCommand]
    private Task AnalyzeDetailsAsync() => LoadCoreAsync(includeFileVerification: true);

    private async Task LoadCoreAsync(bool includeFileVerification)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在枚举并分析启动项（验签/分类/影响评估）…";
        try
        {
            IReadOnlyList<StartupItemDetail> details = await Engine.EnumerateDetailedAsync(
                includeFileVerification: includeFileVerification);
            _allDetails.Clear();
            _allDetails.AddRange(details);
            ApplyPresentation();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "StartupViewModel.LoadAsync");
            StatusText = "加载失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>搜索文本变化刷新过滤。</summary>
    partial void OnSearchTextChanged(string value) => ItemsView.Refresh();

    /// <summary>分类选择变化刷新过滤。</summary>
    partial void OnSelectedCategoryChanged(StartupCategoryNode? value) => ItemsView.Refresh();

    /// <summary>排序变化。</summary>
    partial void OnSelectedSortIndexChanged(int value) => ApplyPresentation();

    /// <summary>切换单个启动项启停（含保护确认）。</summary>
    /// <param name="row">目标行。</param>
    [RelayCommand]
    private async Task ToggleAsync(StartupItemRow? row)
    {
        if (row is null || IsBusy)
        {
            return;
        }

        SelectedItem = row;

        if (row.Detail.Item.Enabled && !await ConfirmDisableAsync(row.Detail))
        {
            return;
        }

        HasElevationRequired = false;
        await ExecuteToggleAsync(row.Detail);
    }

    /// <summary>批量禁用勾选项（保护规则逐项生效）。</summary>
    [RelayCommand]
    private async Task DisableSelectedAsync()
    {
        var selected = Items.Where(static r => r.IsSelected && r.Detail.Item.Enabled).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选启用中的启动项";
            return;
        }

        int succeeded = 0;
        HasElevationRequired = false;
        foreach (StartupItemRow row in selected)
        {
            if (!await ConfirmDisableAsync(row.Detail))
            {
                continue;
            }

            if (await ExecuteToggleAsync(row.Detail, refresh: false))
            {
                succeeded++;
            }
        }

        StatusText = HasElevationRequired
            ? $"批量禁用完成：{succeeded}/{selected.Count}；部分项目需要管理员权限，请使用下方入口重启后重试。"
            : $"批量禁用完成：{succeeded}/{selected.Count}（备份式，可恢复）";
        await LoadAsync();
    }

    /// <summary>批量启用勾选项。</summary>
    [RelayCommand]
    private async Task EnableSelectedAsync()
    {
        var selected = Items.Where(static r => r.IsSelected && !r.Detail.Item.Enabled).ToList();
        int succeeded = 0;
        HasElevationRequired = false;
        foreach (StartupItemRow row in selected)
        {
            if (await ExecuteToggleAsync(row.Detail, refresh: false))
            {
                succeeded++;
            }
        }

        StatusText = HasElevationRequired
            ? $"批量启用完成：{succeeded}/{selected.Count}；部分项目需要管理员权限，请使用下方入口重启后重试。"
            : $"批量启用完成：{succeeded}/{selected.Count}";
        await LoadAsync();
    }

    /// <summary>恢复默认（重新加载系统真实状态）。</summary>
    [RelayCommand]
    private async Task RestoreDefaultAsync() => await LoadAsync();

    /// <summary>导出勾选启动项为 CSV（未勾选时导出当前分类全部）。</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var rows = Items.Where(static r => r.IsSelected).ToList();
        if (rows.Count == 0)
        {
            rows = [.. Items];
        }

        if (rows.Count == 0)
        {
            StatusText = "没有可导出的数据";
            return;
        }

        string? path = CsvExporter.PickSavePath("startup-items.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            CsvExporter.Write(path, ["名称", "发布商", "分类", "签名", "影响", "启用", "路径"],
                rows.Select(static r => (IReadOnlyList<string>)[
                    r.Detail.Item.Name,
                    r.Detail.Item.Publisher ?? string.Empty,
                    r.KindText,
                    r.SignatureText,
                    r.ImpactLevelText,
                    r.Detail.Item.Enabled ? "是" : "否",
                    r.Detail.FilePath ?? string.Empty]));
            StatusText = $"已导出 {rows.Count} 条到 {path}";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "StartupViewModel.ExportCsv");
            StatusText = "导出失败（详见日志）";
        }
    }

    private async Task<bool> ExecuteToggleAsync(StartupItemDetail detail, bool refresh = true)
    {
        IsBusy = true;
        StartupChangeResult result;
        try
        {
            result = detail.Item.Enabled
                ? await Engine.DisableAsync(detail.Item)
                : await Engine.EnableAsync(detail.Item.Id);
        }
        finally
        {
            IsBusy = false;
        }

        // 刷新必须在释放 IsBusy 之后：LoadAsync 以 IsBusy 为守卫，
        // 此前在持锁状态调用会直接早退 → 行状态停留在操作前（"关了没效果"的根因之一）
        if (refresh)
        {
            await LoadAsync();
        }

        // 操作结果提示在刷新之后设置，避免被 LoadAsync 的汇总文本覆盖
        HasElevationRequired |= result.RequiresElevation;
        StatusText = result.Success
            ? (detail.Item.Enabled ? $"已禁用：{detail.Item.Name}（备份可恢复）" : $"已恢复启用：{detail.Item.Name}")
            : result.RequiresElevation
                ? $"需要管理员权限：{detail.Item.Name} 尚未更改。请使用下方入口重启整个应用，再重试并复验。"
                : $"操作失败：{result.Error}";
        return result.Success;
    }

    /// <summary>经 UAC 整体提权重启，并恢复启动项页及最近操作目标。</summary>
    [RelayCommand]
    private async Task RestartAsAdministratorAsync()
    {
        string? itemId = SelectedItem?.Detail.Item.Id;
        bool hasResumeTarget = StartupResumeTarget.TryNormalize(itemId, out string? normalizedItemId);
        bool confirmed = await _dialogService.ConfirmAsync(
            "以管理员身份重新启动",
            hasResumeTarget
                ? "应用将整体关闭并触发 Windows UAC。重新打开后会回到启动项管理，并定位到刚才操作的项目；仍需重新执行操作并确认最终状态。"
                : "应用将整体关闭并触发 Windows UAC。重新打开后会回到启动项管理；仍需重新执行操作并确认最终状态。",
            "重新启动");
        if (!confirmed)
        {
            return;
        }

        try
        {
            bool started = hasResumeTarget
                ? ElevationHelper.RestartAsAdmin("--page", "启动项管理", StartupResumeTarget.ArgumentName, normalizedItemId!)
                : ElevationHelper.RestartAsAdmin("--page", "启动项管理");
            if (!started)
            {
                StatusText = "未启动管理员实例；当前启动项没有被伪报为已更改。";
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "StartupViewModel.RestartAsAdministrator");
            StatusText = "未能以管理员身份重新启动；本次启动项没有被伪报为已更改。";
        }
    }

    /// <summary>
    /// 在页面完成枚举后恢复提权前选中的稳定目标。找不到时保持当前真实列表，绝不创建虚假行。
    /// </summary>
    /// <param name="itemId">经命令行校验后的稳定启动项标识。</param>
    public void RestoreSelection(string itemId)
    {
        if (!StartupResumeTarget.TryNormalize(itemId, out string? normalizedItemId))
        {
            return;
        }

        StartupItemRow? row = Items.FirstOrDefault(item =>
            item.Detail.Item.Id.Equals(normalizedItemId, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            StatusText = "提权前的启动项已不在当前真实列表中；请复查列表后再操作。";
            _logService.LogEvent(LogEventKind.UserAction, "启动项提权恢复目标未找到", new Dictionary<string, object?>
            {
                ["StartupItemId"] = normalizedItemId,
            });
            return;
        }

        SelectedCategory = Categories.FirstOrDefault(category =>
            category.Kind == row.Detail.Classification.Kind);
        SearchText = string.Empty;
        SelectedItem = row;
        ItemsView.Refresh();
        StatusText = $"已恢复定位：{row.Detail.Item.Name}；请重新执行操作并确认最终状态。";
        _logService.LogEvent(LogEventKind.UserAction, "启动项提权恢复目标已定位", new Dictionary<string, object?>
        {
            ["StartupItemId"] = normalizedItemId,
            ["StartupItemName"] = row.Detail.Item.Name,
        });
    }

    private async Task<bool> ConfirmDisableAsync(StartupItemDetail detail)
    {
        switch (detail.Protection)
        {
            case DisableProtection.ConfirmSystemCritical:
                return await _dialogService.ConfirmAsync(
                    "【系统关键启动项】",
                    $"{detail.Item.Name}\n\n关闭后可能导致：\n① 系统无法正常启动\n② 系统服务异常\n③ 部分功能不可用\n\n确定继续？（默认：取消）",
                    "仍然关闭");
            case DisableProtection.ConfirmHardware:
                return await _dialogService.ConfirmAsync(
                    "驱动/OEM 启动项",
                    $"{detail.Item.Name}\n关闭可能影响对应硬件功能，确定继续？",
                    "关闭");
            default:
                return true;
        }
    }

    private bool FilterItem(object obj)
    {
        if (obj is not StartupItemRow row)
        {
            return false;
        }

        if (SelectedCategory is not null && row.Detail.Classification.Kind != SelectedCategory.Kind)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText) ||
            row.Detail.Item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            (row.Detail.Item.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void ApplyPresentation()
    {
        Items.Clear();
        IEnumerable<StartupItemDetail> ordered = SelectedSortIndex switch
        {
            1 => _allDetails.OrderByDescending(static d => d.Impact.Stars),
            2 => _allDetails.OrderByDescending(static d => d.FileSizeBytes),
            _ => _allDetails.OrderBy(static d => d.Classification.Kind).ThenBy(static d => d.Item.Name, StringComparer.OrdinalIgnoreCase),
        };
        foreach (StartupItemDetail detail in ordered)
        {
            Items.Add(new StartupItemRow(detail));
        }

        Selection.Attach(Items);

        SystemCount = _allDetails.Count(static d => d.Classification.Kind == StartupSourceKind.MicrosoftWindows);
        DriverCount = _allDetails.Count(static d => d.Classification.Kind == StartupSourceKind.HardwareDriver);
        AppCount = _allDetails.Count(static d => d.Classification.Kind == StartupSourceKind.UserApplication);
        UnknownCount = _allDetails.Count(static d => d.Classification.Kind == StartupSourceKind.StartupUnknown);
        foreach (StartupCategoryNode node in Categories)
        {
            node.Count = _allDetails.Count(d => d.Classification.Kind == node.Kind);
        }

        var closable = _allDetails
            .Where(static d => d.Classification.Kind == StartupSourceKind.UserApplication &&
                d.Item.Enabled && d.Advice.StartsWith("建议关闭", StringComparison.Ordinal))
            .ToList();
        double seconds = closable.Sum(static d => d.Impact.TimeEstimateSeconds);
        OptimizableText = $"{seconds:F1} 秒（估算）";
        AiSummary = $"建议关闭 {closable.Count} 个第三方启动项";

        TimePoints = BuildChart(static d => d.Impact.TimeEstimateSeconds);
        CpuPoints = BuildChart(static d => d.Impact.CpuEstimate);
        MemoryPoints = BuildChart(static d => d.Impact.MemoryEstimateMB);
        StatusText = $"共 {_allDetails.Count} 个启动项（影响数值为启发式估算）";
        ItemsView.Refresh();
    }

    private IReadOnlyList<BarChartPoint> BuildChart(Func<StartupItemDetail, double> value)
    {
        (StartupSourceKind Kind, string Label, string Color)[] groups =
        [
            (StartupSourceKind.MicrosoftWindows, "系统", "#2563EB"),
            (StartupSourceKind.HardwareDriver, "驱动", "#16A34A"),
            (StartupSourceKind.UserApplication, "用户软件", "#EA580C"),
            (StartupSourceKind.StartupUnknown, "未知", "#DC2626"),
        ];
        return [.. groups.Select(g => new BarChartPoint(
            g.Label,
            _allDetails.Where(d => d.Classification.Kind == g.Kind && d.Item.Enabled).Sum(d => value(d)),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(g.Color))))];
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Discovery;
using LeslieCleaner.Modules.Uninstaller.Execution;
using LeslieCleaner.Modules.Uninstaller.Residual;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.Converters;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>卸载向导页面状态。</summary>
public enum UninstallerPageState
{
    /// <summary>程序列表。</summary>
    List,

    /// <summary>卸载执行中。</summary>
    Uninstalling,

    /// <summary>残留扫描结果。</summary>
    Residuals,

    /// <summary>完成。</summary>
    Done,
}

/// <summary>程序行（多选+来源徽章）。</summary>
public sealed partial class InstalledAppRow : ObservableObject, ISelectableRow
{
    /// <summary>应用记录。</summary>
    public InstalledApp App { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <inheritdoc />
    bool ISelectableRow.IsChecked
    {
        get => IsSelected;
        set => IsSelected = value;
    }

    /// <summary>以应用记录创建行。</summary>
    public InstalledAppRow(InstalledApp app) => App = app;
}

/// <summary>残留项行（高置信度默认勾选）。</summary>
public sealed partial class ResidualItemRow : ObservableObject, ISelectableRow
{
    /// <summary>残留项。</summary>
    public ResidualItem Item { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以残留项创建行（置信度 ≥80% 默认勾选）。</summary>
    public ResidualItemRow(ResidualItem item)
    {
        Item = item;
        _isChecked = item.Confidence >= 0.8;
    }

    /// <summary>类型文本。</summary>
    public string KindText => Item.Kind switch
    {
        ResidualKind.Directory => "目录",
        ResidualKind.RegistryKey => "注册表",
        _ => "文件",
    };

    /// <summary>置信度文本。</summary>
    public string ConfidenceText => $"{Item.Confidence * 100:F0}%";
}

/// <summary>
/// 智能卸载视图模型：程序列表（搜索/多选/来源徽章）→ 卸载向导
/// （执行 → 残留扫描（分阶段耗时）→ 清理残留 → 完成）。全部接真实引擎。
/// </summary>
public sealed partial class UninstallerViewModel : ObservableObject, IPageActivatable
{
    private readonly ILazyModuleActivator _activator;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    // 应用发现结果的服务级缓存（枚举含 winget 等高成本源，避免每次导航重复枚举；
    // TTL 5 分钟，"刷新"按钮强制重枚举；ViewModel 本身仍可被 GC 回收）
    private static readonly object SharedCacheGate = new();
    private static IReadOnlyList<InstalledApp>? _sharedApps;
    private static DateTimeOffset _sharedAppsLoadedUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan SharedCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>已安装程序服务级缓存快照（全局搜索等只读消费；无缓存时返回空列表）。</summary>
    public static IReadOnlyList<InstalledApp> CachedAppsSnapshot
    {
        get
        {
            lock (SharedCacheGate)
            {
                return _sharedApps ?? [];
            }
        }
    }

    private InstalledAppDiscovery? _discovery;
    private UninstallExecutor? _executor;
    private ResidualScannerEngine? _residualEngine;

    private InstalledAppDiscovery Discovery => _discovery ??=
        _activator.Create<InstalledAppDiscovery>(ModuleRefs.UninstallerAssembly, ModuleRefs.InstalledAppDiscoveryType);

    private UninstallExecutor Executor => _executor ??=
        _activator.Create<UninstallExecutor>(ModuleRefs.UninstallerAssembly, ModuleRefs.UninstallExecutorType);

    private ResidualScannerEngine ResidualEngine => _residualEngine ??=
        _activator.Create<ResidualScannerEngine>(ModuleRefs.UninstallerAssembly, ModuleRefs.ResidualScannerEngineType);

    private readonly List<InstalledAppRow> _allApps = [];
    private ResidualScanReport? _residualReport;
    private string? _currentAppName;

    [ObservableProperty]
    private UninstallerPageState _state = UninstallerPageState.List;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "加载已安装程序…";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _wizardText = string.Empty;

    [ObservableProperty]
    private string _residualSummary = string.Empty;

    /// <summary>程序列表（ ICollectionView 支持搜索过滤）。</summary>
    public ICollectionView AppsView { get; }

    /// <summary>程序行集合。</summary>
    public ObservableCollection<InstalledAppRow> Apps { get; } = [];

    /// <summary>残留行集合。</summary>
    public ObservableCollection<ResidualItemRow> Residuals { get; } = [];

    /// <summary>程序列表批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>残留列表批量选择。</summary>
    public BatchSelectionHelper ResidualSelection { get; } = new();

    /// <summary>以懒激活器、对话框服务与日志服务创建视图模型。</summary>
    public UninstallerViewModel(
        ILazyModuleActivator activator,
        DialogService dialogService,
        ILogService logService)
    {
        _activator = activator;
        _dialogService = dialogService;
        _logService = logService;
        AppsView = CollectionViewSource.GetDefaultView(Apps);
        AppsView.Filter = obj => obj is InstalledAppRow row &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             row.App.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             (row.App.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    /// <summary>加载程序列表（强制重新枚举并更新服务级缓存）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<InstalledApp> apps = await Discovery.EnumerateAllAsync();
            lock (SharedCacheGate)
            {
                _sharedApps = apps;
                _sharedAppsLoadedUtc = DateTimeOffset.UtcNow;
            }

            ApplyApps(apps);
            SummaryText = $"已安装程序：{apps.Count} 个";
            StatusText = "勾选程序后点击卸载（支持多选批量）";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "UninstallerViewModel.LoadAsync");
            StatusText = "加载失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 页面激活：优先使用服务级缓存（TTL 5 分钟），过期/为空才重新枚举。
    /// </summary>
    public async Task OnActivatedAsync()
    {
        IReadOnlyList<InstalledApp>? cached;
        lock (SharedCacheGate)
        {
            cached = _sharedAppsLoadedUtc + SharedCacheTtl > DateTimeOffset.UtcNow ? _sharedApps : null;
        }

        if (cached is not null)
        {
            ApplyApps(cached);
            SummaryText = $"已安装程序：{cached.Count} 个（缓存，点击刷新更新）";
            StatusText = "勾选程序后点击卸载（支持多选批量）";
            return;
        }

        if (!IsBusy)
        {
            await LoadAsync();
        }
    }

    private void ApplyApps(IReadOnlyList<InstalledApp> apps)
    {
        Apps.Clear();
        _allApps.Clear();
        foreach (InstalledApp app in apps)
        {
            var row = new InstalledAppRow(app);
            Apps.Add(row);
            _allApps.Add(row);
        }

        Selection.Attach(Apps);
    }

    /// <summary>搜索文本变化时刷新过滤。</summary>
    partial void OnSearchTextChanged(string value) => AppsView.Refresh();

    /// <summary>卸载勾选程序并进入残留向导。</summary>
    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        var selected = _allApps.Where(static r => r.IsSelected).Select(static r => r.App).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先勾选要卸载的程序";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "卸载程序", $"将卸载 {selected.Count} 个程序：\n{string.Join("、", selected.Take(5).Select(static a => a.Name))}{(selected.Count > 5 ? " 等" : string.Empty)}\n卸载完成后自动扫描残留。", "开始卸载");
        if (!confirmed)
        {
            return;
        }

        State = UninstallerPageState.Uninstalling;
        try
        {
            IReadOnlyList<UninstallTaskResult> results = await Executor.RunBatchAsync(selected);
            int succeeded = results.Count(static r => r.Success);
            WizardText = $"卸载完成：{succeeded}/{results.Count} 成功，开始残留扫描…";

            // 逐个做残留管道扫描，合并结果（真实分阶段耗时）
            var allItems = new List<ResidualItem>();
            var stageSummary = new List<string>();
            foreach (InstalledApp app in selected)
            {
                _currentAppName = app.Name;
                ResidualScanReport report = await ResidualEngine.ScanWithReportAsync(
                    new ResidualQuery(app.Publisher, app.Name, app.InstallLocation, AllowDeepScan: false));
                allItems.AddRange(report.Items);
                stageSummary.Add($"{app.Name}: {report.TotalDuration.TotalMilliseconds:F0}ms");
            }

            _residualReport = new ResidualScanReport(
                [.. allItems.GroupBy(static i => i.Path, System.StringComparer.OrdinalIgnoreCase)
                    .Select(static g => g.OrderByDescending(static i => i.Confidence).First())],
                [], TimeSpan.Zero);

            Residuals.Clear();
            foreach (ResidualItem item in _residualReport.Items)
            {
                Residuals.Add(new ResidualItemRow(item));
            }

            ResidualSelection.Attach(Residuals);

            ResidualSummary = $"发现 {Residuals.Count} 项残留（扫描耗时 {string.Join("，", stageSummary)}）";
            State = UninstallerPageState.Residuals;
        }
        catch (OperationCanceledException)
        {
            StatusText = "卸载已取消";
            State = UninstallerPageState.List;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "UninstallerViewModel.UninstallSelectedAsync");
            StatusText = "卸载失败（详见日志）";
            State = UninstallerPageState.List;
        }
    }

    /// <summary>清理勾选残留。</summary>
    [RelayCommand]
    private async Task CleanResidualsAsync()
    {
        if (_residualReport is null)
        {
            return;
        }

        var selected = Residuals.Where(static r => r.IsChecked).Select(static r => r.Item).ToList();
        if (selected.Count == 0)
        {
            ResidualSummary = "未勾选任何残留项";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "清理残留", $"将清理 {selected.Count} 项勾选残留（文件进隔离区，注册表先备份）。", "清理残留");
        if (!confirmed)
        {
            return;
        }

        try
        {
            CleanResult result = await ResidualEngine.CleanResidualsAsync(selected);
            ResidualSummary = $"残留清理完成：{result.Items.Count(static i => i.Success)}/{result.Items.Count} 项成功" +
                $"（释放 {BytesToStringConverter.FormatBytes(result.FreedBytes)}）";
            State = UninstallerPageState.Done;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "UninstallerViewModel.CleanResidualsAsync");
            ResidualSummary = "残留清理失败（详见日志）";
        }
    }

    /// <summary>导出勾选程序为 CSV（未勾选时导出全部）。</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var rows = _allApps.Where(static r => r.IsSelected).ToList();
        if (rows.Count == 0)
        {
            rows = [.. _allApps];
        }

        if (rows.Count == 0)
        {
            StatusText = "没有可导出的数据";
            return;
        }

        string? path = CsvExporter.PickSavePath("installed-apps.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            CsvExporter.Write(path, ["名称", "发布商", "版本", "安装位置", "来源"],
                rows.Select(static r => (IReadOnlyList<string>)[
                    r.App.Name,
                    r.App.Publisher ?? string.Empty,
                    r.App.Version ?? string.Empty,
                    r.App.InstallLocation ?? string.Empty,
                    r.App.Source.ToString()]));
            StatusText = $"已导出 {rows.Count} 条到 {path}";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "UninstallerViewModel.ExportCsv");
            StatusText = "导出失败（详见日志）";
        }
    }

    /// <summary>返回程序列表。</summary>
    [RelayCommand]
    private async Task BackToListAsync()
    {
        State = UninstallerPageState.List;
        await LoadAsync();
    }

    /// <summary>取消当前卸载。</summary>
    [RelayCommand]
    private void CancelUninstall() => Executor.Cancel();
}

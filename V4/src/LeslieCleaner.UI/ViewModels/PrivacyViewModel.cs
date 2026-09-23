using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Privacy;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>隐私项行（按类别聚合展示）。</summary>
public sealed partial class PrivacyCategoryRow : ObservableObject, ISelectableRow
{
    /// <summary>类别。</summary>
    public PrivacyCategory Category { get; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; }

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private bool _isChecked = true;

    /// <summary>以类别创建行。</summary>
    public PrivacyCategoryRow(PrivacyCategory category, string displayName)
    {
        Category = category;
        DisplayName = displayName;
    }
}

/// <summary>
/// 隐私保护视图模型：八类项目勾选（图标+大小/项数）→ 一键清理
/// （注册表自动备份、文件进隔离区，可回滚）。
/// </summary>
public sealed partial class PrivacyViewModel : ObservableObject, IPageActivatable
{
    private static readonly (PrivacyCategory Category, string DisplayName)[] CategoryNames =
    [
        (PrivacyCategory.RecentFiles, "最近文件"),
        (PrivacyCategory.ClipboardHistory, "剪贴板历史"),
        (PrivacyCategory.SearchHistory, "搜索历史"),
        (PrivacyCategory.RunHistory, "运行历史"),
        (PrivacyCategory.OfficeHistory, "Office 历史"),
        (PrivacyCategory.BrowserPrivacy, "浏览器隐私数据"),
        (PrivacyCategory.LocationCache, "地理位置缓存"),
        (PrivacyCategory.SpeechCache, "语音输入缓存"),
    ];

    private readonly ILazyModuleActivator _activator;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    private PrivacyGuardEngine? _engine;

    private PrivacyGuardEngine Engine => _engine ??=
        _activator.Create<PrivacyGuardEngine>(ModuleRefs.PrivacyAssembly, ModuleRefs.PrivacyGuardEngineType);

    private IReadOnlyList<PrivacyItem> _items = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "清理使用痕迹，保护个人隐私安全";

    /// <summary>类别行集合。</summary>
    public ObservableCollection<PrivacyCategoryRow> Categories { get; } =
        [.. CategoryNames.Select(static c => new PrivacyCategoryRow(c.Category, c.DisplayName))];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>以懒激活器、对话框服务与日志服务创建视图模型。</summary>
    public PrivacyViewModel(ILazyModuleActivator activator, DialogService dialogService, ILogService logService)
    {
        _activator = activator;
        _dialogService = dialogService;
        _logService = logService;
        Selection.Attach(Categories);
    }

    /// <summary>扫描隐私项。</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _items = await Engine.ScanAsync();
            foreach (PrivacyCategoryRow row in Categories)
            {
                row.ItemCount = _items.Count(i => i.Category == row.Category);
                row.SizeBytes = _items.Where(i => i.Category == row.Category).Sum(static i => i.SizeBytes);
            }

            StatusText = $"发现 {_items.Count} 项隐私残留";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "PrivacyViewModel.ScanAsync");
            StatusText = "扫描失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>清理勾选类别。</summary>
    [RelayCommand]
    private async Task CleanAsync()
    {
        var selectedCategories = new HashSet<PrivacyCategory>(
            Categories.Where(static c => c.IsChecked).Select(static c => c.Category));
        var selected = _items.Where(i => selectedCategories.Contains(i.Category)).ToList();
        if (selected.Count == 0)
        {
            StatusText = "请先扫描或勾选类别";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "清理隐私数据", $"将清理 {selected.Count} 项隐私残留（注册表自动备份，文件进隔离区）。", "开始清理");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CleanResult result = await Engine.CleanAsync(selected);
            StatusText = $"清理完成：{result.Items.Count(static i => i.Success)}/{result.Items.Count} 项成功（可从工具箱回滚）";
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "PrivacyViewModel.CleanAsync");
            StatusText = "清理失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }
    /// <inheritdoc />
    public async Task OnActivatedAsync() => await ScanAsync();
}

using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner.Duplicates;
using LeslieCleaner.UI.Converters;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>重复组行（默认勾选，可整组排除）。</summary>
public sealed partial class DuplicateGroupRow : ObservableObject, ISelectableRow
{
    /// <summary>重复组。</summary>
    public DuplicateGroup Group { get; }

    /// <summary>哈希预览。</summary>
    public string HashPreview { get; }

    /// <summary>浪费空间文本。</summary>
    public string SizeText { get; }

    [ObservableProperty]
    private bool _isChecked = true;

    /// <summary>以组、哈希预览与大小文本创建行。</summary>
    public DuplicateGroupRow(DuplicateGroup group, string hashPreview, string sizeText)
    {
        Group = group;
        HashPreview = hashPreview;
        SizeText = sizeText;
    }
}

/// <summary>
/// 重复文件视图模型：三层扫描（大小→部分哈希→全量）→ 分组列表 →
/// 智能保留规则 → 批量进隔离区（可回滚）。
/// </summary>
public sealed partial class DuplicatesViewModel : ObservableObject
{
    private readonly ILazyModuleActivator _activator;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    private DuplicateFinderEngine? _engine;

    private DuplicateFinderEngine Engine => _engine ??=
        _activator.Create<DuplicateFinderEngine>(ModuleRefs.CleanerAssembly, ModuleRefs.DuplicateFinderEngineType);

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "扫描范围：下载 / 文档 / 桌面（三层哈希递进识别）";

    [ObservableProperty]
    private KeepPolicy _selectedPolicy = KeepPolicy.Newest;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    /// <summary>保留策略集合。</summary>
    public IReadOnlyList<KeepPolicy> Policies { get; } =
        [KeepPolicy.Newest, KeepPolicy.Oldest, KeepPolicy.ShortestPath, KeepPolicy.PreferredDirectory];

    /// <summary>重复组集合。</summary>
    public ObservableCollection<DuplicateGroupRow> Groups { get; } = [];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>以懒激活器、对话框服务与日志服务创建视图模型。</summary>
    public DuplicatesViewModel(ILazyModuleActivator activator, DialogService dialogService, ILogService logService)
    {
        _activator = activator;
        _dialogService = dialogService;
        _logService = logService;
    }

    /// <summary>执行重复扫描。</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在扫描（大小分组 → 部分哈希 → 全量哈希）…";
        ProgressText = string.Empty;
        // 实时进度：阶段 + 已处理文件数 + 当前路径（节流 100ms，不再"干等"）
        var progress = new ThrottledProgress<ScanProgress>(new Progress<ScanProgress>(p =>
        {
            ProgressText = p.TotalItems is { } total
                ? $"{p.Phase}：{p.ProcessedItems} / {total}"
                : $"{p.Phase}：已处理 {p.ProcessedItems} 项（{p.CurrentPath ?? string.Empty}）";
            if (p.PercentComplete is { } pc)
            {
                ProgressPercent = pc;
            }
        }));
        var roots = new List<string>();
        foreach (Environment.SpecialFolder folder in
            new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop })
        {
            string path = Environment.GetFolderPath(folder);
            if (Directory.Exists(path))
            {
                roots.Add(path);
            }
        }

        string downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            roots.Add(downloads);
        }

        try
        {
            IReadOnlyList<DuplicateGroup> groups = await Engine.FindDuplicatesAsync(roots, progress: progress);
            progress.Flush();
            Groups.Clear();
            foreach (DuplicateGroup group in groups)
            {
                Groups.Add(new DuplicateGroupRow(
                    group,
                    group.ContentHash[..Math.Min(12, group.ContentHash.Length)] + "…",
                    BytesToStringConverter.FormatBytes(group.WastedBytes)));
            }

            Selection.Attach(Groups);

            long wasted = groups.Sum(static g => g.WastedBytes);
            ProgressText = string.Empty;
            StatusText = groups.Count == 0
                ? "未发现重复文件"
                : $"发现 {groups.Count} 组重复，可释放 {BytesToStringConverter.FormatBytes(wasted)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "DuplicatesViewModel.ScanAsync");
            StatusText = "扫描失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>批量删除冗余（勾选组按保留策略进隔离区）。</summary>
    [RelayCommand]
    private async Task DeleteRedundantAsync()
    {
        var selectedGroups = Groups.Where(static g => g.IsChecked).ToList();
        if (selectedGroups.Count == 0)
        {
            StatusText = Groups.Count == 0 ? "请先扫描" : "未勾选任何重复组";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "清理重复文件", $"将按「{SelectedPolicy}」策略保留 1 份，其余 {selectedGroups.Sum(static g => g.Group.Files.Count - 1)} 个文件移入隔离区（30 天内可还原）。", "开始清理");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string operationId = await Engine.DeleteRedundantAsync(
                [.. selectedGroups.Select(static g => g.Group)], SelectedPolicy);
            StatusText = $"清理完成（操作 {operationId[..8]}，可从工具箱隔离区还原）";
            Groups.Clear();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "DuplicatesViewModel.DeleteRedundantAsync");
            StatusText = "清理失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

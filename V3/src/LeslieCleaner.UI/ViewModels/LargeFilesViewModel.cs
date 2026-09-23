using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner.Analytics;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>大文件行（勾选后可批量导出）。</summary>
public sealed partial class LargeFileRow : ObservableObject, ISelectableRow
{
    /// <summary>条目。</summary>
    public LargeFileEntry Entry { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以条目创建行。</summary>
    public LargeFileRow(LargeFileEntry entry) => Entry = entry;
}

/// <summary>
/// 大文件分析视图模型：Treemap 可视化 + Top 100 列表 + 类型统计 + 规则建议。
/// 默认范围为用户目录（文档/下载/桌面），支持暂停/取消。
/// </summary>
public sealed partial class LargeFilesViewModel : ObservableObject
{
    private readonly ILazyModuleActivator _activator;
    private readonly ILogService _logService;

    private LargeFileAnalyzerEngine? _engine;

    private LargeFileAnalyzerEngine Engine => _engine ??=
        _activator.Create<LargeFileAnalyzerEngine>(ModuleRefs.CleanerAssembly, ModuleRefs.LargeFileAnalyzerEngineType);

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "分析磁盘空间占用（Top 100 / 类型统计 / Treemap）";

    [ObservableProperty]
    private IReadOnlyList<TreemapNode>? _treemapRoots;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    /// <summary>Top 文件列表。</summary>
    public ObservableCollection<LargeFileRow> TopFiles { get; } = [];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>类型统计。</summary>
    public ObservableCollection<FileTypeStat> TypeStats { get; } = [];

    /// <summary>清理建议。</summary>
    public ObservableCollection<string> Suggestions { get; } = [];

    /// <summary>以懒激活器与日志服务创建视图模型。</summary>
    public LargeFilesViewModel(ILazyModuleActivator activator, ILogService logService)
    {
        _activator = activator;
        _logService = logService;
    }

    /// <summary>执行分析。</summary>
    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在分析…";
        var roots = new List<string>();
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string subDirectory in new[] { "Downloads", "Documents", "Desktop", "Videos", "Pictures", "Music" })
        {
            string path = Path.Combine(userProfile, subDirectory);
            if (Directory.Exists(path))
            {
                roots.Add(path);
            }
        }

        try
        {
            var progress = new Progress<ScanProgress>(p => CurrentPath = p.CurrentPath ?? string.Empty);
            LargeFileAnalysisResult result = await Engine.AnalyzeAsync(roots, progress: progress);
            TreemapRoots = result.Treemap;
            TopFiles.Clear();
            foreach (LargeFileEntry entry in result.TopFiles)
            {
                TopFiles.Add(new LargeFileRow(entry));
            }

            Selection.Attach(TopFiles);

            TypeStats.Clear();
            foreach (FileTypeStat stat in result.TypeStats.Take(10))
            {
                TypeStats.Add(stat);
            }

            Suggestions.Clear();
            foreach (string suggestion in result.Suggestions)
            {
                Suggestions.Add(suggestion);
            }

            StatusText = result.WasCancelled
                ? "分析已取消"
                : $"分析完成：{result.ScannedFiles:N0} 个文件，建议 {result.Suggestions.Count} 条";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "LargeFilesViewModel.AnalyzeAsync");
            StatusText = "分析失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>暂停/继续切换。</summary>
    [RelayCommand]
    private void TogglePause()
    {
        if (Engine.IsPaused)
        {
            Engine.Resume();
        }
        else
        {
            Engine.Pause();
        }
    }

    /// <summary>导出勾选大文件为 CSV（未勾选时导出全部）。</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var rows = TopFiles.Where(static r => r.IsChecked).ToList();
        if (rows.Count == 0)
        {
            rows = [.. TopFiles];
        }

        if (rows.Count == 0)
        {
            StatusText = "没有可导出的数据";
            return;
        }

        string? path = CsvExporter.PickSavePath("large-files.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            CsvExporter.Write(path, ["路径", "大小(字节)", "修改时间"],
                rows.Select(static r => (IReadOnlyList<string>)[
                    r.Entry.Path,
                    r.Entry.SizeBytes.ToString(),
                    r.Entry.LastWriteUtc.ToString("yyyy-MM-dd HH:mm:ss")]));
            StatusText = $"已导出 {rows.Count} 条到 {path}";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "LargeFilesViewModel.ExportCsv");
            StatusText = "导出失败（详见日志）";
        }
    }

    /// <summary>取消分析。</summary>
    [RelayCommand]
    private void Cancel() => Engine.Cancel();
}

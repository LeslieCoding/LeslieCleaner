using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Modules.Shredder;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>粉碎任务行（勾选后可批量移除出队列）。</summary>
public sealed partial class ShredTaskRow : ObservableObject, ISelectableRow
{
    /// <summary>任务。</summary>
    public ShredTask Task { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以任务创建行。</summary>
    public ShredTaskRow(ShredTask task) => Task = task;
}

/// <summary>
/// 文件粉碎视图模型：拖拽接收 + 任务队列 + 3/7/35 遍模式 + SSD 提示。
/// 引擎 API：AddFiles/AddFolder/RemovePendingTasks/Start/Pause/Resume/Cancel。
/// </summary>
public sealed partial class ShredderViewModel : ObservableObject
{
    private readonly ILazyModuleActivator _activator;
    private readonly ILogService _logService;

    private ShredderEngine? _engine;

    private ShredderEngine Engine => _engine ??=
        _activator.Create<ShredderEngine>(ModuleRefs.ShredderAssembly, ModuleRefs.ShredderEngineType);

    [ObservableProperty]
    private ShredMethod _selectedMethod = ShredMethod.DoD3Pass;

    [ObservableProperty]
    private string _statusText = "拖拽文件或文件夹到下方区域，或点击按钮添加";

    [ObservableProperty]
    private string _ssdHint = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    /// <summary>可选粉碎模式。</summary>
    public IReadOnlyList<ShredMethod> Methods { get; } =
        [ShredMethod.DoD3Pass, ShredMethod.DoD7Pass, ShredMethod.Gutmann35Pass];

    /// <summary>任务队列。</summary>
    public ObservableCollection<ShredTaskRow> Tasks { get; } = [];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>以懒激活器与日志服务创建视图模型。</summary>
    public ShredderViewModel(ILazyModuleActivator activator, ILogService logService)
    {
        _activator = activator;
        _logService = logService;
        RefreshSsdHint();
    }

    /// <summary>添加文件（拖拽或对话框）。</summary>
    /// <param name="paths">路径集合。</param>
    [RelayCommand]
    private void AddPaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return;
        }

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                Engine.AddFolder(path);
            }
            else
            {
                Engine.AddFiles([path]);
            }
        }

        RefreshTasks();
        int blocked = Tasks.Count(static t => t.Task.Status == ShredTaskStatus.Blocked);
        StatusText = blocked > 0
            ? $"已加入队列（{blocked} 项被关键路径防护拦截）"
            : "已加入队列，点击开始粉碎";
    }

    /// <summary>启动队列。</summary>
    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        StatusText = "正在粉碎…";
        var progress = new ThrottledProgress<ShredProgress>(new Progress<ShredProgress>(p =>
        {
            CurrentFile = p.CurrentFile;
            ProgressPercent = p.TotalFiles > 0 ? p.ProcessedFiles * 100.0 / p.TotalFiles : 0;
        }));

        try
        {
            await Engine.StartAsync(SelectedMethod, progress);
            progress.Flush();
            StatusText = "粉碎完成（文件不可恢复）";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ShredderViewModel.StartAsync");
            StatusText = "执行失败（详见日志）";
        }
        finally
        {
            IsRunning = false;
            RefreshTasks();
        }
    }

    /// <summary>取消队列。</summary>
    [RelayCommand]
    private void Cancel() => Engine.Cancel();

    /// <summary>批量移除勾选任务出队列（仅未执行项，不进粉碎流程）。</summary>
    [RelayCommand]
    private void RemoveSelected()
    {
        var selected = Tasks.Where(static t => t.IsChecked).Select(static t => t.Task.Id).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选任何任务";
            return;
        }

        int removed = Engine.RemovePendingTasks(selected);
        RefreshTasks();
        StatusText = $"已从队列移除 {removed} 项（执行中/已完成项保留）";
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

    private void RefreshTasks()
    {
        Tasks.Clear();
        foreach (ShredTask task in Engine.GetTasks())
        {
            Tasks.Add(new ShredTaskRow(task));
        }

        Selection.Attach(Tasks);
    }

    private void RefreshSsdHint()
    {
        bool? isSsd = StorageMediaDetector.IsSsd(
            Environment.GetFolderPath(Environment.SpecialFolder.System));
        SsdHint = isSsd == true
            ? "检测到 SSD：多遍覆写对 SSD 无意义且伤寿命，将自动使用 SSD 安全策略（随机单遍+截断）"
            : "检测到机械硬盘或介质未知：将按所选模式覆写";
    }
}

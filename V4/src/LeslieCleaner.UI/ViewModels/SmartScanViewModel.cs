using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Cleaner.Scanning;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Converters;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>Smart Scan 页面状态。</summary>
public enum SmartScanPageState
{
    /// <summary>就绪。</summary>
    Ready,

    /// <summary>扫描中。</summary>
    Scanning,

    /// <summary>结果页。</summary>
    Results,

    /// <summary>清理中。</summary>
    Cleaning,

    /// <summary>完成页。</summary>
    Done,
}

/// <summary>分类行（勾选/大小/文件数）。</summary>
public sealed partial class ScanCategoryRow : ObservableObject, ISelectableRow
{
    /// <summary>目标标识。</summary>
    public string TargetId { get; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; }

    /// <summary>安全级别。</summary>
    public ScanSafetyLevel Safety { get; }

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private long _logicalSizeBytes;

    [ObservableProperty]
    private long _allocatedSizeBytes;

    [ObservableProperty]
    private long _metadataSizeBytes;

    [ObservableProperty]
    private int _inaccessibleItemCount;

    [ObservableProperty]
    private long _measurementDiscrepancyBytes;

    [ObservableProperty]
    private string _measurementSource = string.Empty;

    [ObservableProperty]
    private string _measurementDetails = string.Empty;

    /// <summary>以目标定义与默认勾选创建行。</summary>
    public ScanCategoryRow(ScanTargetDefinition target)
    {
        TargetId = target.Id;
        DisplayName = target.DisplayName;
        Safety = target.Safety;
        _isChecked = target.Safety == ScanSafetyLevel.Default;
    }
}

/// <summary>逐项清理失败的用户可读说明。</summary>
/// <param name="Path">失败目标。</param>
/// <param name="Reason">归类后的原因。</param>
/// <param name="Guidance">建议的下一步。</param>
/// <param name="RequiresElevation">是否建议以管理员身份重试。</param>
public sealed record CleanFailureRow(string Path, string Reason, string Guidance, bool RequiresElevation);
/// <summary>
/// Smart Scan 视图模型：扫描中（当前路径+进度+取消/暂停）、完成页（环形图+分类表格）、
/// 清理（确认对话框→进度→结果页，释放空间与回滚入口）。全部接真实引擎。
/// </summary>
public sealed partial class SmartScanViewModel : ObservableObject
{
    private readonly ILazyModuleActivator _activator;
    private readonly ICleanEngine _cleanEngine;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    private SmartScanEngine? _scanEngine;

    private SmartScanEngine ScanEngine => _scanEngine ??=
        _activator.Create<SmartScanEngine>(ModuleRefs.CleanerAssembly, ModuleRefs.SmartScanEngineType,
            _activator.Create<IScanEnvironment>(ModuleRefs.CleanerAssembly, ModuleRefs.SystemScanEnvironmentType));

    private ScanResult? _lastScan;
    private string? _lastOperationId;

    [ObservableProperty]
    private SmartScanPageState _state = SmartScanPageState.Ready;

    [ObservableProperty]
    private string _statusText = "一键扫描 14 类系统垃圾";

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _totalFoundText = string.Empty;

    [ObservableProperty]
    private string _cleanResultText = string.Empty;

    [ObservableProperty]
    private string _cleanResultTitle = "清理结果";

    [ObservableProperty]
    private string _postCleanVerificationText = string.Empty;

    [ObservableProperty]
    private string _cleanFailureSummary = string.Empty;

    [ObservableProperty]
    private bool _hasCleanIssues;

    [ObservableProperty]
    private bool _canRollback;

    /// <summary>分类行集合。</summary>
    public ObservableCollection<ScanCategoryRow> Categories { get; } =
        [.. SmartScanTargets.All.Select(static t => new ScanCategoryRow(t))];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>失败项目（最多展示 100 项；总数在摘要中保留）。</summary>
    public ObservableCollection<CleanFailureRow> CleanFailures { get; } = [];

    /// <summary>环形图扇区。</summary>
    [ObservableProperty]
    private IReadOnlyList<DonutSlice>? _donutSlices;

    /// <summary>以懒激活器、清理引擎与对话框服务创建视图模型。</summary>
    public SmartScanViewModel(
        ILazyModuleActivator activator,
        ICleanEngine cleanEngine,
        DialogService dialogService,
        ILogService logService)
    {
        _activator = activator;
        _cleanEngine = cleanEngine;
        _dialogService = dialogService;
        _logService = logService;
        Selection.Attach(Categories);
    }

    /// <summary>开始扫描（真实引擎，后台执行）。</summary>
    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (State == SmartScanPageState.Scanning)
        {
            return;
        }

        State = SmartScanPageState.Scanning;
        StatusText = "正在扫描…";
        CleanFailures.Clear();
        HasCleanIssues = false;
        PostCleanVerificationText = string.Empty;
        ProgressPercent = 0;
        var progress = new ThrottledProgress<ScanProgress>(new Progress<ScanProgress>(p =>
        {
            CurrentPath = p.CurrentPath ?? p.Phase;
            if (p.PercentComplete is { } percent)
            {
                ProgressPercent = percent;
            }
        }));

        try
        {
            _lastScan = await ScanEngine.ScanAsync(new ScanOptions { Targets = ["*"] }, progress);
            progress.Flush();
            ApplyResults(_lastScan);
            State = SmartScanPageState.Results;
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
            State = SmartScanPageState.Ready;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "SmartScanViewModel.StartScanAsync");
            StatusText = "扫描失败（详见日志）";
            State = SmartScanPageState.Ready;
        }
    }

    /// <summary>取消扫描。</summary>
    [RelayCommand]
    private void CancelScan() => ScanEngine.Cancel();

    /// <summary>暂停/继续切换。</summary>
    [RelayCommand]
    private void TogglePause()
    {
        if (ScanEngine.IsPaused)
        {
            ScanEngine.Resume();
            IsPaused = false;
        }
        else
        {
            ScanEngine.Pause();
            IsPaused = true;
        }
    }

    /// <summary>一键清理（确认 → 进度 → 结果）。</summary>
    [RelayCommand]
    private async Task CleanAsync()
    {
        if (_lastScan is null || State != SmartScanPageState.Results)
        {
            return;
        }

        var selectedIds = new HashSet<string>(
            Categories.Where(static c => c.IsChecked).Select(static c => c.TargetId));
        var items = _lastScan.Items.Where(i => selectedIds.Contains(i.Category)).ToList();
        if (items.Count == 0)
        {
            StatusText = "未勾选任何分类";
            return;
        }

        long selectedLogicalBytes = items.Sum(static i => i.LogicalSizeBytes ?? i.SizeBytes);
        long selectedAllocatedBytes = items.Sum(static i => i.AllocatedSizeBytes ?? i.SizeBytes);
        long selectedItemCount = items.Sum(static item => item.ItemCount);
        bool includesRecycleBin = items.Any(static item => item.Category == SmartScanTargets.RecycleBinCategoryId);
        long selectedDiscrepancyBytes = items.Sum(static i => i.MeasurementDiscrepancyBytes);
        string measurementNotice = includesRecycleBin
            ? $"预计实际释放（磁盘占用）{BytesToStringConverter.FormatBytes(selectedAllocatedBytes)}；Shell 逻辑大小 {BytesToStringConverter.FormatBytes(selectedLogicalBytes)}。" +
              (selectedDiscrepancyBytes == 0 ? string.Empty : $" 检测到统计口径差异 {BytesToStringConverter.FormatBytes(Math.Abs(selectedDiscrepancyBytes))}，可能来自扫描期间变化。")
            : $"预计释放 {BytesToStringConverter.FormatBytes(selectedAllocatedBytes)}。";
        string rollbackNotice = includesRecycleBin
            ? "普通文件将进入隔离区（30 天内可还原），注册表项将自动备份。Windows 回收站会被系统级清空，无法还原。"
            : "文件将进入隔离区（30 天内可还原），注册表项将自动备份。";
        bool confirmed = await _dialogService.ConfirmAsync(
            "确认清理",
            $"将清理 {selectedItemCount:N0} 个项目。{measurementNotice}\n" + rollbackNotice,
            "开始清理");
        if (!confirmed)
        {
            return;
        }

        State = SmartScanPageState.Cleaning;
        StatusText = "正在清理…";
        try
        {
            CleanResult result = await _cleanEngine.CleanAsync(
            new CleanRequest
            {
                Items = items,
                Reason = "Smart Scan 一键清理",
                AllowIrreversibleRecycleBin = items.Any(static item => item.Category == SmartScanTargets.RecycleBinCategoryId),
            });
            _lastOperationId = result.OperationId;
            CanRollback = result.Items.Any(static i => i.Success && (i.QuarantineEntryId is not null || i.BackupId is not null));
            CleanFailures.Clear();
            CleanedItem[] failed = result.Items.Where(static item => !item.Success).ToArray();
            foreach (CleanedItem failure in failed.Take(100))
            {
                CleanFailurePresentation presentation = CleanFailurePresenter.Describe(failure.Error);
                CleanFailures.Add(new CleanFailureRow(
                    failure.Item.Path, presentation.Reason, presentation.Guidance, presentation.RequiresElevation));
            }

            int succeeded = result.Items.Count - failed.Length;
            CleanResultText = $"实际释放 {BytesToStringConverter.FormatBytes(result.FreedBytes)}；处理成功 {succeeded:N0}/{result.Items.Count:N0} 项。";
            CleanFailureSummary = failed.Length == 0
                ? "未收到逐项失败。"
                : $"{failed.Length:N0} 项失败（下方展示前 {Math.Min(100, failed.Length):N0} 项），失败项目未计入已释放空间。";

            try
            {
                ScanResult verification = await ScanEngine.ScanAsync(new ScanOptions { Targets = selectedIds.ToArray() });
                long remainingBytes = verification.Items.Sum(static item => item.SizeBytes);
                long remainingItems = verification.Items.Sum(static item => item.ItemCount);
                int inaccessible = verification.ErrorCount + verification.Items.Sum(static item => item.InaccessibleItemCount);
                PostCleanVerificationText = inaccessible > 0
                    ? $"复扫完成：仍发现 {remainingItems:N0} 项 / {BytesToStringConverter.FormatBytes(remainingBytes)}；另有 {inaccessible:N0} 个位置无法确认，结果不是全量验证。"
                    : $"复扫完成：仍发现 {remainingItems:N0} 项 / {BytesToStringConverter.FormatBytes(remainingBytes)}。";
                HasCleanIssues = failed.Length > 0 || remainingItems > 0 || inaccessible > 0;
            }
            catch (Exception verificationError) when (verificationError is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _logService.LogException(verificationError, "SmartScanViewModel.PostCleanVerification");
                PostCleanVerificationText = $"复扫失败，无法确认剩余：{CleanFailurePresenter.Describe(verificationError.Message).Reason}。";
                HasCleanIssues = true;
            }

            CleanResultTitle = HasCleanIssues ? "清理部分完成，需要处理剩余项" : "清理完成并通过复扫";
            State = SmartScanPageState.Done;
        }
        catch (OperationCanceledException)
        {
            StatusText = "清理已取消";
            State = SmartScanPageState.Results;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "SmartScanViewModel.CleanAsync");
            CleanFailurePresentation presentation = CleanFailurePresenter.Describe(ex.Message);
            CleanFailures.Clear();
            CleanFailures.Add(new CleanFailureRow("本次清理", presentation.Reason, presentation.Guidance, presentation.RequiresElevation));
            CleanResultTitle = "清理未完成";
            CleanResultText = "实际释放空间无法确认；失败不会计为成功。";
            CleanFailureSummary = presentation.RequiresElevation
                ? "需要管理员权限：可使用主窗口顶部“管理员模式”重新启动后复扫。"
                : presentation.Reason;
            PostCleanVerificationText = "清理流程未完成，必须重新扫描后才能确认剩余。";
            HasCleanIssues = true;
            State = SmartScanPageState.Done;
        }
    }

    /// <summary>回滚上次清理。</summary>
    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (_lastOperationId is null)
        {
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync("回滚清理", "将从隔离区/备份还原上次清理的全部项目。", "开始回滚");
        if (!confirmed)
        {
            return;
        }

        try
        {
            bool success = await _cleanEngine.RollbackAsync(_lastOperationId);
            CleanResultText = success ? "回滚完成：全部项目已还原。" : "回滚完成：部分项目还原失败（详见日志）。";
            CanRollback = !success;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "SmartScanViewModel.RollbackAsync");
            CleanResultText = "回滚失败（详见日志）";
        }
    }

    private void ApplyResults(ScanResult result)
    {
        long discrepancyBytes = result.Items.Sum(static item => item.MeasurementDiscrepancyBytes);
        TotalFoundText = $"预计可释放（磁盘占用）{BytesToStringConverter.FormatBytes(result.TotalAllocatedSizeBytes)}；逻辑大小 {BytesToStringConverter.FormatBytes(result.TotalSizeBytes)}，共 {result.Items.Sum(static item => item.ItemCount):N0} 项" +
            (discrepancyBytes == 0 ? string.Empty : $"；统计口径差异 {BytesToStringConverter.FormatBytes(Math.Abs(discrepancyBytes))}（可能为扫描期间变化）");
        StatusText = result.WasCancelled
            ? "扫描被取消（部分结果）"
            : result.ErrorCount > 0
                ? $"扫描完成，但有 {result.ErrorCount:N0} 个路径无法访问；结果不是全量统计"
                : "扫描完成";
        foreach (ScanCategoryRow row in Categories)
        {
            ScannedItem[] categoryItems = result.Items.Where(i => i.Category == row.TargetId).ToArray();
            row.SizeBytes = categoryItems.Sum(static item => item.SizeBytes);
            row.FileCount = checked((int)Math.Min(int.MaxValue, categoryItems.Sum(static item => item.ItemCount)));
            row.LogicalSizeBytes = categoryItems.Sum(static item => item.LogicalSizeBytes ?? item.SizeBytes);
            row.AllocatedSizeBytes = categoryItems.Sum(static item => item.AllocatedSizeBytes ?? item.SizeBytes);
            row.MetadataSizeBytes = categoryItems.Sum(static item => item.MetadataSizeBytes);
            row.InaccessibleItemCount = categoryItems.Sum(static item => item.InaccessibleItemCount);
            row.MeasurementDiscrepancyBytes = categoryItems.Sum(static item => item.MeasurementDiscrepancyBytes);
            row.MeasurementSource = string.Join(" + ", categoryItems.Select(static item => item.MeasurementSource)
                .Where(static source => !string.IsNullOrWhiteSpace(source)).Distinct(StringComparer.OrdinalIgnoreCase));
            row.MeasurementDetails = string.IsNullOrWhiteSpace(row.MeasurementSource) && row.InaccessibleItemCount == 0 && row.MetadataSizeBytes == 0
                ? string.Empty
                : $"磁盘占用 {BytesToStringConverter.FormatBytes(row.AllocatedSizeBytes)} · 逻辑大小 {BytesToStringConverter.FormatBytes(row.LogicalSizeBytes)} · 元数据 {BytesToStringConverter.FormatBytes(row.MetadataSizeBytes)} · 无法访问 {row.InaccessibleItemCount:N0} · 来源 {DisplayMeasurementSource(row.MeasurementSource)}" +
                  (row.MeasurementDiscrepancyBytes == 0 ? string.Empty : $" · 统计口径差异 {BytesToStringConverter.FormatBytes(Math.Abs(row.MeasurementDiscrepancyBytes))}（可能为扫描期间变化）");
        }

        DonutSlices = Categories
            .Where(static c => c.SizeBytes > 0)
            .OrderByDescending(static c => c.SizeBytes)
            .Take(8)
            .Select((c, i) => new DonutSlice(c.DisplayName, c.SizeBytes, SliceBrush(i)))
            .ToList();
    }

    private static string DisplayMeasurementSource(string source) => source
        .Replace("WindowsShellAndFileSystem", "Windows Shell + 文件系统枚举", StringComparison.Ordinal)
        .Replace("WindowsShell", "Windows Shell", StringComparison.Ordinal)
        .Replace("WindowsFileAllocation", "Windows 磁盘分配", StringComparison.Ordinal)
        .Replace("LogicalFileLength", "文件逻辑长度", StringComparison.Ordinal);

    private static System.Windows.Media.Brush SliceBrush(int index)
    {
        string[] colors = ["#2563EB", "#16A34A", "#EA580C", "#8B5CF6", "#0D94A8", "#DB2777", "#65A30D", "#D97706"];
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colors[index % colors.Length]));
    }
}

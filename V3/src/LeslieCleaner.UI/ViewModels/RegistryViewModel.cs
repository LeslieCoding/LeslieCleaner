using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.Core.Models;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>注册表问题行。</summary>
public sealed partial class RegistryFindingRow : ObservableObject, ISelectableRow
{
    /// <summary>命中项。</summary>
    public RegistryFinding Finding { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以命中项创建行（Safe/Moderate 默认勾，Risky 不勾）。</summary>
    public RegistryFindingRow(RegistryFinding finding)
    {
        Finding = finding;
        _isChecked = finding.Risk != RegistryRiskLevel.Risky;
    }
}

/// <summary>
/// 注册表清理视图模型：扫描（RegistryScanner）→ 分组列表（风险颜色）→
/// 修复（CleanEngine 备份后删）→ 一键恢复（备份列表 + reg import）。
/// </summary>
public sealed partial class RegistryViewModel : ObservableObject, IPageActivatable
{
    private readonly IRegistryScanner _registryScanner;
    private readonly ICleanEngine _cleanEngine;
    private readonly IBackupService _backupService;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;

    private string? _lastOperationId;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "扫描无效扩展名、COM/CLSID、字体引用、卸载项与失效路径";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>问题行集合。</summary>
    public ObservableCollection<RegistryFindingRow> Findings { get; } = [];

    /// <summary>批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper Selection { get; } = new();

    /// <summary>备份记录集合。</summary>
    public ObservableCollection<BackupRecord> Backups { get; } = [];

    /// <summary>以扫描器、清理引擎与备份服务创建视图模型。</summary>
    public RegistryViewModel(
        IRegistryScanner registryScanner,
        ICleanEngine cleanEngine,
        IBackupService backupService,
        DialogService dialogService,
        ILogService logService)
    {
        _registryScanner = registryScanner;
        _cleanEngine = cleanEngine;
        _backupService = backupService;
        _dialogService = dialogService;
        _logService = logService;
    }

    /// <summary>执行全量规则扫描。</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在扫描注册表…";
        try
        {
            IReadOnlyList<RegistryFinding> findings = await _registryScanner.ScanAsync();
            Findings.Clear();
            foreach (RegistryFinding finding in findings)
            {
                Findings.Add(new RegistryFindingRow(finding));
            }

            Selection.Attach(Findings);

            SummaryText = $"发现 {findings.Count} 个问题";
            StatusText = findings.Count == 0 ? "注册表很干净，未发现问题" : "勾选后点击修复（Safe/Moderate 已默认勾选）";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.ScanAsync");
            StatusText = "扫描失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>修复勾选项（自动备份，可一键恢复）。</summary>
    [RelayCommand]
    private async Task FixAsync()
    {
        var selected = Findings.Where(static f => f.IsChecked).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选任何问题";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "修复注册表", $"将修复 {selected.Count} 个勾选项。删除前自动备份（.reg + metadata），支持一键恢复。", "开始修复");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = selected.Select(f => new ScannedItem(
                f.Finding.ValueName is null ? f.Finding.KeyPath : $"{f.Finding.KeyPath}|{f.Finding.ValueName}",
                0, "Registry", DateTimeOffset.UtcNow)).ToList();
            CleanResult result = await _cleanEngine.CleanAsync(
                new CleanRequest { Items = items, Reason = "注册表修复" });
            _lastOperationId = result.OperationId;
            int succeeded = result.Items.Count(static i => i.Success);
            int failed = result.Items.Count - succeeded;
            StatusText = failed == 0
                ? $"修复完成：{succeeded}/{result.Items.Count} 项成功"
                : $"修复完成：{succeeded}/{result.Items.Count} 项成功，{failed} 项失败" +
                  "（失败项多为权限不足，可点右上角「管理员模式」重启后重试）";
            Findings.Clear();
            await LoadBackupsAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.FixAsync");
            StatusText = "修复失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>导出勾选问题为 CSV（未勾选时导出全部）。</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var rows = Findings.Where(static f => f.IsChecked).ToList();
        if (rows.Count == 0)
        {
            rows = [.. Findings];
        }

        if (rows.Count == 0)
        {
            StatusText = "没有可导出的数据";
            return;
        }

        string? path = CsvExporter.PickSavePath("registry-findings.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            CsvExporter.Write(path, ["键路径", "值名", "问题类型", "风险", "说明"],
                rows.Select(static f => (IReadOnlyList<string>)[
                    f.Finding.KeyPath,
                    f.Finding.ValueName ?? string.Empty,
                    f.Finding.Issue.ToString(),
                    f.Finding.Risk.ToString(),
                    f.Finding.Description]));
            StatusText = $"已导出 {rows.Count} 条到 {path}";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.ExportCsv");
            StatusText = "导出失败（详见日志）";
        }
    }

    /// <summary>加载备份列表。</summary>
    [RelayCommand]
    private async Task LoadBackupsAsync()
    {
        try
        {
            IReadOnlyList<BackupRecord> backups = await _backupService.ListBackupsAsync();
            Backups.Clear();
            foreach (BackupRecord backup in backups)
            {
                Backups.Add(backup);
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.LoadBackupsAsync");
        }
    }

    /// <summary>恢复指定备份。</summary>
    /// <param name="backup">备份记录。</param>
    [RelayCommand]
    private async Task RestoreBackupAsync(BackupRecord? backup)
    {
        if (backup is null)
        {
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync("恢复注册表", $"将恢复备份：{backup.RegistryKeyPath}", "恢复");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _backupService.RestoreAsync(backup.Id);
            StatusText = "备份已恢复";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.RestoreBackupAsync");
            StatusText = "恢复失败（详见日志）";
        }
    }
    /// <inheritdoc />
    public async Task OnActivatedAsync() => await LoadBackupsAsync();
}

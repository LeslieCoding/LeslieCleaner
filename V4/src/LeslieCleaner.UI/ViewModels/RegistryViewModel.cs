using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>注册表问题行。</summary>
public sealed partial class RegistryFindingRow : ObservableObject, ISelectableRow
{
    /// <summary>命中项。</summary>
    public RegistryFinding Finding { get; }

    private bool _isChecked;

    /// <summary>是否选中；系统保护项和 Critical 项不能被批量命令强制选中。</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, Finding.CanRepair && value);
    }

    [ObservableProperty]
    private string? _operationMessage;

    /// <summary>以命中项创建行（Safe/Moderate 默认勾，Risky 不勾）。</summary>
    public RegistryFindingRow(RegistryFinding finding)
    {
        Finding = finding;
        _isChecked = finding.CanRepairAutomatically;
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
    private readonly RegistryElevationResumeStore _resumeStore;
    private readonly List<RegistryResumeTarget> _elevationTargets = [];

    private string? _lastOperationId;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "扫描无效扩展名、COM/CLSID、字体引用、卸载项与失效路径";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private bool _hasElevationRequired;

    partial void OnIsBusyChanged(bool value)
    {
        FixCommand.NotifyCanExecuteChanged();
    }

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
        ILogService logService,
        RegistryElevationResumeStore resumeStore)
    {
        _registryScanner = registryScanner;
        _cleanEngine = cleanEngine;
        _backupService = backupService;
        _dialogService = dialogService;
        _logService = logService;
        _resumeStore = resumeStore;
    }

    /// <summary>执行全量规则扫描。</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        await ScanCoreAsync();
    }

    private async Task<bool> ScanCoreAsync()
    {
        if (IsBusy)
        {
            return false;
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
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.ScanAsync");
            StatusText = "扫描失败（详见日志）";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>修复勾选项（自动备份，可一键恢复）。</summary>
    [RelayCommand(CanExecute = nameof(CanFix))]
    private async Task FixAsync()
    {
        // Prevent a second click (or keyboard invocation) from starting a competing batch while
        // the first batch is still backing up and mutating the registry. The engine serializes
        // operations, but the UI selection is mutable; without this guard a stale second request
        // could create a different backup and report a misleading failure after the first pass.
        if (IsBusy)
        {
            return;
        }

        foreach (RegistryFindingRow forbidden in Findings.Where(static row => row.IsChecked && !row.Finding.CanRepair))
        {
            forbidden.IsChecked = false;
        }

        var selected = Findings.Where(static f => f.IsChecked && f.Finding.CanRepair).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选任何问题";
            return;
        }

        int mediumOrHigher = selected.Count(static row =>
            row.Finding.ConfirmationRequirement >= RegistryConfirmationRequirement.Detailed);
        int highRisk = selected.Count(static row =>
            row.Finding.ConfirmationRequirement == RegistryConfirmationRequirement.Advanced);

        bool confirmed = await _dialogService.ConfirmAsync(
            "修复注册表",
            $"将修复 {selected.Count} 个勾选项（其中需详细确认 {mediumOrHigher} 项、高风险 {highRisk} 项）。" +
            "删除前自动备份，删除后逐项回读验证；失败项会保留在列表中。",
            "开始修复");
        if (!confirmed)
        {
            return;
        }


        if (highRisk > 0 && !await _dialogService.ConfirmAsync(
                "高风险注册表项二次确认",
                $"有 {highRisk} 个高风险项目默认不会自动修复。请确认你已阅读路径和影响说明，并愿意仅在备份后继续。",
                "仍然修复"))
        {
            return;
        }

        IsBusy = true;
        HasElevationRequired = false;
        _elevationTargets.Clear();
        try
        {
            var items = selected.Select(f => new ScannedItem(
                f.Finding.ValueName is null ? f.Finding.KeyPath : $"{f.Finding.KeyPath}|{f.Finding.ValueName}",
                0, "Registry", DateTimeOffset.UtcNow,
                RegistryKeyPath: f.Finding.KeyPath,
                RegistryValueName: f.Finding.ValueName)).ToList();
            CleanResult result = await _cleanEngine.CleanAsync(
                new CleanRequest { Items = items, Reason = "注册表修复" });
            _lastOperationId = result.OperationId;
            int succeeded = result.Items.Count(static i => i.Success);
            int failed = result.Items.Count - succeeded;
            int requiresElevation = result.Items.Count(static item => item.RequiresElevation);
            var resultsByTarget = result.Items
                .GroupBy(static item => CleanEngine.BuildRegistryTargetIdentity(item.Item),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (RegistryFindingRow row in selected)
            {
                string targetIdentity = CleanEngine.BuildRegistryTargetIdentity(
                    row.Finding.KeyPath, row.Finding.ValueName);
                if (!resultsByTarget.TryGetValue(targetIdentity, out CleanedItem? itemResult))
                {
                    row.OperationMessage = "未收到该项目的执行结果，已保留";
                    continue;
                }

                if (itemResult.Success)
                {
                    Findings.Remove(row);
                    continue;
                }

                CleanFailurePresentation presentation = CleanFailurePresenter.Describe(itemResult.Error);
                row.OperationMessage = $"失败：{presentation.Reason}；{presentation.Guidance}";
                if (itemResult.RequiresElevation)
                {
                    _elevationTargets.Add(new RegistryResumeTarget(
                        row.Finding.KeyPath, row.Finding.ValueName));
                }
            }

            HasElevationRequired = _elevationTargets.Count > 0;

            Selection.Attach(Findings);
            SummaryText = $"本次成功 {succeeded} 项，失败 {failed} 项；列表保留 {Findings.Count} 项";
            StatusText = failed == 0
                ? $"修复完成：{succeeded}/{result.Items.Count} 项成功，且均已通过删除后回读"
                : requiresElevation > 0
                    ? $"修复完成：{succeeded} 项成功，{failed} 项失败，其中 {requiresElevation} 项需要管理员权限；失败项已保留"
                    : $"修复完成：{succeeded} 项成功，{failed} 项失败；请查看每项原因和运行日志";
            _logService.LogEvent(LogEventKind.UserAction, "Registry repair batch completed", new Dictionary<string, object?>
            {
                ["OperationId"] = result.OperationId,
                ["Requested"] = result.Items.Count,
                ["Succeeded"] = succeeded,
                ["Failed"] = failed,
                ["RequiresElevation"] = requiresElevation,
                ["RemainingFindings"] = Findings.Count,
            });
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

    private bool CanFix() => !IsBusy;

    /// <summary>保存权限失败目标，经 UAC 整体重启并返回注册表清理页。</summary>
    [RelayCommand]
    private async Task RestartAsAdministratorAsync()
    {
        if (_elevationTargets.Count == 0)
        {
            StatusText = "当前没有需要管理员权限重试的项目。";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "以管理员身份重新启动",
            $"将保存 {_elevationTargets.Count} 个权限失败目标并触发 Windows UAC。重新打开后会重新扫描并恢复勾选；不会自动删除，仍需再次确认。",
            "重新启动");
        if (!confirmed) return;

        try
        {
            string token = await _resumeStore.SaveAsync(_elevationTargets);
            bool started = ElevationHelper.RestartAsAdmin(
                "--page", "注册表清理", "--registry-resume", token);
            if (!started)
            {
                await _resumeStore.DiscardAsync();
                StatusText = "未启动管理员实例；恢复清单已丢弃，注册表没有被伪报为已更改。";
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.RestartAsAdministratorAsync");
            StatusText = "无法保存或启动管理员恢复流程；没有自动执行注册表操作，请查看日志。";
        }
    }

    /// <summary>提权启动后重新扫描真实状态，并仅恢复仍存在、仍可修复的目标选择。</summary>
    public async Task ResumeAfterElevationAsync(string token)
    {
        IReadOnlyList<RegistryResumeTarget> targets;
        try
        {
            targets = await _resumeStore.ConsumeAsync(token);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "RegistryViewModel.ResumeAfterElevationAsync");
            StatusText = "读取注册表提权恢复信息失败；未自动执行任何操作，请查看日志。";
            return;
        }
        if (targets.Count == 0)
        {
            StatusText = "注册表提权恢复信息无效或已过期；未自动执行任何操作。";
            return;
        }

        if (!await ScanCoreAsync())
        {
            foreach (RegistryFindingRow row in Findings) row.IsChecked = false;
            Selection.Attach(Findings);
            StatusText = "提权后的注册表重新扫描失败；旧结果未恢复勾选，也未执行任何操作，请查看日志。";
            return;
        }

        foreach (RegistryFindingRow row in Findings) row.IsChecked = false;
        int restored = 0;
        foreach (RegistryFindingRow row in Findings)
        {
            bool matches = targets.Any(target =>
                target.KeyPath.Equals(row.Finding.KeyPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(target.ValueName, row.Finding.ValueName, StringComparison.OrdinalIgnoreCase));
            if (matches && row.Finding.CanRepair)
            {
                row.IsChecked = true;
                restored++;
            }
        }

        Selection.Attach(Findings);
        StatusText = $"提权后已重新扫描，并恢复勾选 {restored}/{targets.Count} 个仍存在目标；请复核后再次点击修复。";
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

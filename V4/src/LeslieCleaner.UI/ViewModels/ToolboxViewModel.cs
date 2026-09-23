using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.UI.Services;
using LeslieCleaner.Modules.Startup;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>服务项行（勾选后可批量启停）。</summary>
public sealed partial class ServiceRow : ObservableObject, ISelectableRow
{
    /// <summary>服务项。</summary>
    public StartupItem Item { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以服务项创建行。</summary>
    public ServiceRow(StartupItem item) => Item = item;
}

/// <summary>隔离区条目行（勾选后可批量还原）。</summary>
public sealed partial class QuarantineEntryRow : ObservableObject, ISelectableRow
{
    /// <summary>条目。</summary>
    public QuarantineEntry Entry { get; }

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>以条目创建行。</summary>
    public QuarantineEntryRow(QuarantineEntry entry) => Entry = entry;
}

/// <summary>
/// 工具箱视图模型：隔离区管理（列表/还原/彻底删除）、注册表备份恢复、
/// 系统还原点、服务管理（只读枚举+启停，核心服务黑名单）、维护计划。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed partial class ToolboxViewModel : ObservableObject, IPageActivatable
{
    /// <summary>核心服务黑名单（禁止启停操作）。</summary>
    public static IReadOnlyList<string> CoreServiceBlacklist { get; } =
    [
        "WinDefend", "EventLog", "RpcSs", "DcomLaunch", "Winmgmt", "Schedule",
        "wuauserv", "lsm", "SamSs", "CryptSvc", "Dnscache", "W32Time",
    ];

    private readonly IQuarantineService _quarantineService;
    private readonly IBackupService _backupService;
    private readonly ISystemRestorePointService _restorePointService;
    private readonly ILazyModuleActivator _activator;
    private readonly DialogService _dialogService;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    private StartupManagerEngine? _startupEngine;
    private MaintenancePlanner? _maintenancePlanner;

    private StartupManagerEngine StartupEngine => _startupEngine ??=
        _activator.Create<StartupManagerEngine>(ModuleRefs.StartupAssembly, ModuleRefs.StartupManagerEngineType);

    private MaintenancePlanner MaintenancePlanner => _maintenancePlanner ??=
        _activator.Create<MaintenancePlanner>(ModuleRefs.AiAssembly, ModuleRefs.MaintenancePlannerType);

    [ObservableProperty]
    private string _statusText = "隔离区 / 备份 / 还原点 / 服务 / 计划任务";

    [ObservableProperty]
    private string _quarantineSummary = string.Empty;

    /// <summary>隔离区条目。</summary>
    public ObservableCollection<QuarantineEntryRow> QuarantineEntries { get; } = [];

    /// <summary>备份记录。</summary>
    public ObservableCollection<BackupRecord> Backups { get; } = [];

    /// <summary>服务项。</summary>
    public ObservableCollection<ServiceRow> Services { get; } = [];

    /// <summary>隔离区批量选择（三态全选/反选/勾选计数）。</summary>
    public BatchSelectionHelper QuarantineSelection { get; } = new();

    /// <summary>服务批量选择。</summary>
    public BatchSelectionHelper ServiceSelection { get; } = new();

    /// <summary>维护计划。</summary>
    public ObservableCollection<MaintenancePlan> Plans { get; } = [];

    /// <summary>以各基础设施服务与懒激活器创建视图模型。</summary>
    /// <param name="quarantineService">Provides quarantine inspection and recovery.</param>
    /// <param name="backupService">Provides registry backup recovery.</param>
    /// <param name="restorePointService">Provides Windows restore-point operations.</param>
    /// <param name="activator">Creates optional module services on demand.</param>
    /// <param name="dialogService">Collects user confirmation for destructive actions.</param>
    /// <param name="logService">Records operational failures and outcomes.</param>
    /// <param name="securityOperationGate">Blocks service control when integrity validation fails.</param>
    public ToolboxViewModel(
        IQuarantineService quarantineService,
        IBackupService backupService,
        ISystemRestorePointService restorePointService,
        ILazyModuleActivator activator,
        DialogService dialogService,
        ILogService logService,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _quarantineService = quarantineService;
        _backupService = backupService;
        _restorePointService = restorePointService;
        _activator = activator;
        _dialogService = dialogService;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>加载隔离区条目。</summary>
    [RelayCommand]
    private async Task LoadQuarantineAsync()
    {
        try
        {
            IReadOnlyList<QuarantineEntry> entries = await _quarantineService.ListEntriesAsync();
            QuarantineEntries.Clear();
            foreach (QuarantineEntry entry in entries)
            {
                QuarantineEntries.Add(new QuarantineEntryRow(entry));
            }

            QuarantineSelection.Attach(QuarantineEntries);

            QuarantineSummary = $"{entries.Count} 个项目，共 {entries.Sum(static e => e.SizeBytes) / 1048576.0:F1} MB";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.LoadQuarantineAsync");
            StatusText = "隔离区加载失败（详见日志）";
        }
    }

    /// <summary>还原隔离区条目。</summary>
    /// <param name="row">条目行。</param>
    [RelayCommand]
    private async Task RestoreQuarantineAsync(QuarantineEntryRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            await _quarantineService.RestoreAsync(row.Entry.Id, RestoreConflictPolicy.Rename);
            StatusText = $"已还原：{row.Entry.OriginalPath}";
            await LoadQuarantineAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.RestoreQuarantineAsync");
            StatusText = "还原失败（详见日志）";
        }
    }

    /// <summary>批量还原勾选隔离区条目（一次确认）。</summary>
    [RelayCommand]
    private async Task RestoreSelectedQuarantineAsync()
    {
        var selected = QuarantineEntries.Where(static r => r.IsChecked).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选任何隔离区条目";
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            "批量还原", $"将还原 {selected.Count} 个勾选条目到原位置（冲突自动重命名）。", "开始还原");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("Service control");
        }
        catch (Core.Diagnostics.LeslieCleanerException ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.BatchControlServicesAsync");
            StatusText = ex.Message;
            return;
        }

        int succeeded = 0;
        foreach (QuarantineEntryRow row in selected)
        {
            try
            {
                await _quarantineService.RestoreAsync(row.Entry.Id, RestoreConflictPolicy.Rename);
                succeeded++;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "ToolboxViewModel.RestoreSelectedQuarantineAsync",
                    new Dictionary<string, object?> { ["Entry"] = row.Entry.OriginalPath });
            }
        }

        StatusText = $"批量还原完成：{succeeded}/{selected.Count}";
        await LoadQuarantineAsync();
    }

    /// <summary>清理过期隔离区条目。</summary>
    [RelayCommand]
    private async Task PurgeExpiredAsync()
    {
        bool confirmed = await _dialogService.ConfirmAsync(
            "清理隔离区", "将永久删除所有超过保留期的条目（不可恢复）。", "永久删除");
        if (!confirmed)
        {
            return;
        }

        try
        {
            int purged = await _quarantineService.PurgeExpiredAsync();
            StatusText = $"已永久删除 {purged} 个过期条目";
            await LoadQuarantineAsync();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.PurgeExpiredAsync");
            StatusText = "清理失败（详见日志）";
        }
    }

    /// <summary>加载注册表备份。</summary>
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
            _logService.LogException(ex, "ToolboxViewModel.LoadBackupsAsync");
        }
    }

    /// <summary>恢复注册表备份。</summary>
    /// <param name="backup">备份记录。</param>
    [RelayCommand]
    private async Task RestoreBackupAsync(BackupRecord? backup)
    {
        if (backup is null)
        {
            return;
        }

        try
        {
            await _backupService.RestoreAsync(backup.Id);
            StatusText = $"已恢复：{backup.RegistryKeyPath}";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.RestoreBackupAsync");
            StatusText = "恢复失败（详见日志）";
        }
    }

    /// <summary>创建系统还原点。</summary>
    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        StatusText = "正在创建系统还原点（需要系统保护已启用）…";
        try
        {
            bool success = await _restorePointService.CreateRestorePointAsync("LeslieCleaner 手动还原点");
            StatusText = success ? "系统还原点已创建" : "创建失败：系统保护未启用或权限不足（详见日志）";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.CreateRestorePointAsync");
            StatusText = "创建失败（详见日志）";
        }
    }

    /// <summary>加载服务列表（自启动服务）。</summary>
    [RelayCommand]
    private async Task LoadServicesAsync()
    {
        try
        {
            IReadOnlyList<StartupItem> items = await StartupEngine.EnumerateAsync();
            Services.Clear();
            foreach (StartupItem item in items.Where(static i => i.Kind == StartupItemKind.Service))
            {
                Services.Add(new ServiceRow(item));
            }

            ServiceSelection.Attach(Services);

            StatusText = $"共 {Services.Count} 个自启动服务（核心服务已列入黑名单保护）";
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.LoadServicesAsync");
            StatusText = "服务枚举失败（详见日志）";
        }
    }

    /// <summary>启停服务（sc.exe，黑名单保护）。</summary>
    /// <param name="args">[服务项, "start"|"stop"]。</param>
    [RelayCommand]
    private async Task ControlServiceAsync(object?[]? args)
    {
        if (args is null || args.Length < 2 || args[0] is not StartupItem item ||
            args[1] is not string action || action is not ("start" or "stop"))
        {
            return;
        }

        if (CoreServiceBlacklist.Contains(item.Location, StringComparer.OrdinalIgnoreCase))
        {
            StatusText = $"核心系统服务 {item.Location} 受保护，禁止操作";
            return;
        }

        string actionText = action == "start" ? "启动" : "停止";
        bool confirmed = await _dialogService.ConfirmAsync(
            $"{actionText}服务", $"将{actionText}服务：{item.Name}。", $"{actionText}服务");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("Service control");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{action} \"{item.Location}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            if (process is not null)
            {
                await process.WaitForExitAsync();
                StatusText = process.ExitCode == 0
                    ? $"服务 {item.Name} {(action == "start" ? "已启动" : "已停止")}"
                    : $"操作失败（退出码 {process.ExitCode}，可能需要管理员权限）";
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "ToolboxViewModel.ControlServiceAsync");
            StatusText = "服务操作失败（详见日志）";
        }
    }

    /// <summary>批量启停勾选服务（一次确认，核心服务黑名单逐项生效）。</summary>
    /// <param name="action">"start" 或 "stop"。</param>
    [RelayCommand]
    private async Task BatchControlServicesAsync(string? action)
    {
        if (action is not ("start" or "stop"))
        {
            return;
        }

        var selected = Services.Where(static r => r.IsChecked).ToList();
        if (selected.Count == 0)
        {
            StatusText = "未勾选任何服务";
            return;
        }

        string actionText = action == "start" ? "启动" : "停止";
        bool confirmed = await _dialogService.ConfirmAsync(
            $"批量{actionText}服务", $"将对 {selected.Count} 个勾选服务执行{actionText}（核心系统服务自动跳过）。", $"开始{actionText}");
        if (!confirmed)
        {
            return;
        }

        int succeeded = 0;
        int skipped = 0;
        foreach (ServiceRow row in selected)
        {
            if (CoreServiceBlacklist.Contains(row.Item.Location, StringComparer.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"{action} \"{row.Item.Location}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                });
                if (process is not null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        succeeded++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "ToolboxViewModel.BatchControlServicesAsync",
                    new Dictionary<string, object?> { ["Service"] = row.Item.Location });
            }
        }

        StatusText = $"批量{actionText}完成：{succeeded}/{selected.Count} 成功" +
            (skipped > 0 ? $"，{skipped} 个核心服务受保护跳过" : string.Empty);
    }

    /// <summary>加载维护计划。</summary>
    [RelayCommand]
    private void LoadPlans()
    {
        Plans.Clear();
        foreach (MaintenancePlan plan in MaintenancePlanner.GetPlans())
        {
            Plans.Add(plan);
        }
    }
    /// <inheritdoc />
    public async Task OnActivatedAsync()
    {
        await LoadQuarantineAsync();
        await LoadBackupsAsync();
        LoadPlans();
    }
}

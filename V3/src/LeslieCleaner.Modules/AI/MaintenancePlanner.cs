using System.Diagnostics;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.AI;

/// <summary>
/// 自动维护计划器。配置驱动的计划（间隔天数 + 模块集合），到期判定为纯函数；
/// 可选注册为系统计划任务（schtasks，权限不足时降级为仅配置存储并记日志）。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class MaintenancePlanner
{
    /// <summary>维护计划配置键（MaintenancePlan 数组，LastRunUtc 由计划器回写）。</summary>
    public const string PlansConfigKey = "ai:maintenancePlans";

    private const string ScheduledTaskName = "LeslieCleaner Maintenance";

    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>以配置与日志服务创建计划器。</summary>
    /// <param name="configService">配置服务。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="securityOperationGate">Blocks creation of scheduled maintenance when integrity validation fails.</param>
    public MaintenancePlanner(
        IConfigService configService,
        ILogService logService,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _configService = configService;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>
    /// 计划到期判定（纯函数）：启用且（从未执行 或 距上次执行 ≥ 间隔天数）。
    /// </summary>
    /// <param name="plan">维护计划。</param>
    /// <param name="nowUtc">当前时间。</param>
    public static bool IsPlanDue(MaintenancePlan plan, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Enabled &&
            (plan.LastRunUtc is null || nowUtc - plan.LastRunUtc.Value >= TimeSpan.FromDays(plan.IntervalDays));
    }

    /// <summary>
    /// 保存计划列表。
    /// </summary>
    /// <param name="plans">计划集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task SavePlansAsync(IReadOnlyList<MaintenancePlan> plans, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        return _configService.SetValueAsync(PlansConfigKey, plans, cancellationToken);
    }

    /// <summary>
    /// 读取计划列表。
    /// </summary>
    public IReadOnlyList<MaintenancePlan> GetPlans() =>
        _configService.GetValue(PlansConfigKey, Array.Empty<MaintenancePlan>());

    /// <summary>
    /// 获取当前到期的计划。
    /// </summary>
    /// <param name="nowUtc">当前时间。</param>
    public IReadOnlyList<MaintenancePlan> GetDuePlans(DateTimeOffset nowUtc) =>
        [.. GetPlans().Where(plan => IsPlanDue(plan, nowUtc))];

    /// <summary>
    /// 标记计划已执行（回写 LastRunUtc）。
    /// </summary>
    /// <param name="planName">计划名。</param>
    /// <param name="runUtc">执行时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task MarkExecutedAsync(string planName, DateTimeOffset runUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planName);
        var plans = new List<MaintenancePlan>(GetPlans());
        int index = plans.FindIndex(p => p.Name == planName);
        if (index >= 0)
        {
            plans[index] = plans[index] with { LastRunUtc = runUtc };
            await SavePlansAsync(plans, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 将维护计划注册为系统计划任务（每日触发 LeslieCleaner.App --maintenance）。
    /// 权限不足/失败时记日志并降级返回 <see langword="false"/>（配置侧计划仍然有效）。
    /// </summary>
    /// <param name="executablePath">应用可执行文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<bool> RegisterScheduledTaskAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        string schtasks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        string arguments = $"/create /tn \"{ScheduledTaskName}\" /sc daily /st 09:00 /f /tr \"\\\"{executablePath}\\\" --maintenance\"";
        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("Maintenance task registration");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = schtasks,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new LeslieCleanerException("无法启动 schtasks.exe");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            bool success = process.ExitCode == 0;
            _logService.LogEvent(LogEventKind.UserAction, "注册维护计划任务", new Dictionary<string, object?>
            {
                ["Success"] = success,
                ["Executable"] = executablePath,
            });
            return success;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or LeslieCleanerException)
        {
            _logService.LogException(ex, "MaintenancePlanner.RegisterScheduledTaskAsync");
            return false;
        }
    }

    /// <summary>
    /// 删除系统维护计划任务（不存在/权限不足返回 <see langword="false"/>）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<bool> UnregisterScheduledTaskAsync(CancellationToken cancellationToken = default)
    {
        string schtasks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = schtasks,
                Arguments = $"/delete /tn \"{ScheduledTaskName}\" /f",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new LeslieCleanerException("无法启动 schtasks.exe");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or LeslieCleanerException)
        {
            _logService.LogException(ex, "MaintenancePlanner.UnregisterScheduledTaskAsync");
            return false;
        }
    }
}

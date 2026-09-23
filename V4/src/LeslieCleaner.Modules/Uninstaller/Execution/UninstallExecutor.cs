using System.Diagnostics;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller.Workflow;

namespace LeslieCleaner.Modules.Uninstaller.Execution;

/// <summary>
/// 批量卸载执行器。逐个调用 UninstallString / msiexec /x（MSI）/
/// winget uninstall / steam://uninstall 协议（提示用户交互），
/// 监控退出码，超时熔断（默认 30 分钟，可配置）并终止进程树。
/// 支持暂停（排队项暂停派发）与取消。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class UninstallExecutor : IUninstallRunner
{
    /// <summary>单应用卸载超时配置键（分钟，默认 30）。</summary>
    public const string TimeoutMinutesConfigKey = "uninstaller:timeoutMinutes";

    private readonly ILogService _logService;
    private readonly IConfigService? _configService;
    private readonly ISecurityOperationGate? _securityOperationGate;
    private readonly Core.Threading.PauseTokenSource _pauseTokenSource = new();

    private CancellationTokenSource? _activeRun;

    /// <summary>以日志服务创建执行器。</summary>
    /// <param name="logService">日志服务。</param>
    /// <param name="configService">配置服务（超时），可为 <see langword="null"/>。</param>
    /// <param name="securityOperationGate">Blocks uninstallation when startup integrity validation has failed.</param>
    public UninstallExecutor(
        ILogService logService,
        IConfigService? configService = null,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _logService = logService;
        _configService = configService;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>当前是否暂停。</summary>
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <summary>暂停派发（运行中的卸载进程不受影响）。</summary>
    public void Pause() => _pauseTokenSource.Pause();

    /// <summary>继续派发。</summary>
    public void Resume() => _pauseTokenSource.Resume();

    /// <summary>取消批量卸载（终止当前卸载进程树）。</summary>
    public void Cancel() => _activeRun?.Cancel();

    /// <summary>
    /// 顺序执行批量卸载，逐项报告结果。
    /// </summary>
    /// <param name="apps">待卸载应用。</param>
    /// <param name="progress">进度回调（已完成/总数/当前应用），可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<UninstallTaskResult>> RunBatchAsync(
        IReadOnlyList<InstalledApp> apps,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apps);
        _securityOperationGate?.EnsureSensitiveOperationAllowed("Software uninstallation");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeRun = linkedCts;
        try
        {
            int timeoutMinutes = _configService?.GetValue(TimeoutMinutesConfigKey, 30) ?? 30;
            var results = new List<UninstallTaskResult>(apps.Count);
            _logService.LogEvent(LogEventKind.DeleteStart, "批量卸载开始",
                new Dictionary<string, object?> { ["Apps"] = apps.Count });

            for (int i = 0; i < apps.Count; i++)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                await _pauseTokenSource.Token.WaitWhilePausedAsync(linkedCts.Token).ConfigureAwait(false);
                InstalledApp app = apps[i];
                results.Add(await UninstallOneAsync(app, TimeSpan.FromMinutes(timeoutMinutes), linkedCts.Token).ConfigureAwait(false));
                progress?.Report(new CleanProgress(i + 1, apps.Count, app.Name));
            }

            _logService.LogEvent(LogEventKind.DeleteFinish, "批量卸载完成", new Dictionary<string, object?>
            {
                ["Succeeded"] = results.Count(static r => r.Success),
                ["Failed"] = results.Count(static r => !r.Success),
            });
            return results;
        }
        finally
        {
            _activeRun = null;
        }
    }

    /// <summary>
    /// 卸载单个应用（公开供单卸载场景使用）。
    /// </summary>
    /// <param name="app">应用。</param>
    /// <param name="timeout">超时。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<UninstallTaskResult> UninstallOneAsync(
        InstalledApp app, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        _securityOperationGate?.EnsureSensitiveOperationAllowed("Software uninstallation");
        try
        {
            return app.Source switch
            {
                AppSourceType.Msi when app.ProductCode is not null =>
                    await RunProcessAsync(app, "msiexec.exe", $"/x {app.ProductCode} /passive /norestart",
                        "msiexec", timeout, cancellationToken).ConfigureAwait(false),
                AppSourceType.Appx or AppSourceType.Store =>
                    await RunAppxRemoveAsync(app, timeout, cancellationToken).ConfigureAwait(false),
                AppSourceType.Steam =>
                    await RunProtocolUninstallAsync(app, cancellationToken).ConfigureAwait(false),
                AppSourceType.Winget =>
                    await RunWingetUninstallAsync(app, timeout, cancellationToken).ConfigureAwait(false),
                _ when !string.IsNullOrWhiteSpace(app.UninstallString) =>
                    await RunUninstallStringAsync(app, timeout, cancellationToken).ConfigureAwait(false),
                _ => new UninstallTaskResult(app.Id, app.Name, false, null, "none",
                    TimedOut: false, "无可用卸载命令（需手动处理）"),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or LeslieCleanerException)
        {
            _logService.LogException(ex, "UninstallExecutor.UninstallOneAsync",
                new Dictionary<string, object?> { ["App"] = app.Name });
            return new UninstallTaskResult(app.Id, app.Name, false, null, "unknown", false, ex.Message);
        }
    }

    private async Task<UninstallTaskResult> RunUninstallStringAsync(
        InstalledApp app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        (string fileName, string arguments) = SplitCommandLine(app.UninstallString!);
        return await RunProcessAsync(app, fileName, arguments, "uninstall-string", timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UninstallTaskResult> RunWingetUninstallAsync(
        InstalledApp app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string wingetId = app.Id.StartsWith("winget:", StringComparison.Ordinal) ? app.Id["winget:".Length..] : app.Id;
        return await RunProcessAsync(app, "winget.exe",
            $"uninstall --id \"{wingetId}\" --disable-interactivity --accept-source-agreements",
            "winget", timeout, cancellationToken).ConfigureAwait(false);
    }

    private Task<UninstallTaskResult> RunProtocolUninstallAsync(InstalledApp app, CancellationToken cancellationToken)
    {
        // steam://uninstall/<appid> 协议：发起即视为成功（实际卸载由 Steam 客户端交互完成）
        Process.Start(new ProcessStartInfo
        {
            FileName = app.UninstallString!,
            UseShellExecute = true,
        });
        _logService.LogEvent(LogEventKind.UserAction, "Steam 卸载协议已发起（需用户在客户端确认）",
            new Dictionary<string, object?> { ["App"] = app.Name });
        return Task.FromResult(new UninstallTaskResult(app.Id, app.Name, true, null, "steam-protocol", false, null));
    }

    private async Task<UninstallTaskResult> RunAppxRemoveAsync(
        InstalledApp app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Remove-AppxPackage 管线：PackageManager.RemovePackageAsync（app.Id = "appx:{PackageFullName}"）
        string packageFullName = app.Id.StartsWith("appx:", StringComparison.Ordinal)
            ? app.Id["appx:".Length..]
            : app.Id;

        _logService.LogEvent(LogEventKind.DeleteStart, "Appx 卸载开始", new Dictionary<string, object?>
        {
            ["App"] = app.Name,
            ["PackageFullName"] = packageFullName,
        });

        try
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            Windows.Management.Deployment.DeploymentResult result;
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeout);
                result = await manager.RemovePackageAsync(packageFullName)
                    .AsTask(timeoutCts.Token).ConfigureAwait(false);
            }

            bool success = string.IsNullOrEmpty(result.ErrorText);
            _logService.LogEvent(LogEventKind.DeleteFinish, "Appx 卸载完成", new Dictionary<string, object?>
            {
                ["App"] = app.Name,
                ["Success"] = success,
                ["ErrorText"] = result.ErrorText,
            });
            return new UninstallTaskResult(app.Id, app.Name, success, success ? 0 : 1, "appx-remove",
                TimedOut: false, success ? null : result.ErrorText);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UninstallTaskResult(app.Id, app.Name, false, null, "appx-remove",
                TimedOut: true, $"卸载超时（{timeout.TotalMinutes} 分钟）");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "UninstallExecutor.RunAppxRemoveAsync",
                new Dictionary<string, object?> { ["App"] = app.Name });
            return new UninstallTaskResult(app.Id, app.Name, false, null, "appx-remove", false, ex.Message);
        }
    }

    private async Task<UninstallTaskResult> RunProcessAsync(
        InstalledApp app, string fileName, string arguments, string method,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _logService.LogEvent(LogEventKind.DeleteStart, "卸载进程启动", new Dictionary<string, object?>
        {
            ["App"] = app.Name,
            ["Command"] = $"{fileName} {arguments}",
        });

        process.Start();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            _logService.LogEvent(LogEventKind.Exception, "卸载超时熔断", new Dictionary<string, object?>
            {
                ["App"] = app.Name,
                ["TimeoutMinutes"] = timeout.TotalMinutes,
            });
            return new UninstallTaskResult(app.Id, app.Name, false, null, method, TimedOut: true,
                $"卸载超时（{timeout.TotalMinutes} 分钟）已终止");
        }

        bool success = process.ExitCode == 0 || process.ExitCode == 3010; // 3010 = 需重启的成功
        _logService.LogEvent(LogEventKind.DeleteFinish, "卸载进程退出", new Dictionary<string, object?>
        {
            ["App"] = app.Name,
            ["ExitCode"] = process.ExitCode,
        });
        return new UninstallTaskResult(app.Id, app.Name, success, process.ExitCode, method,
            TimedOut: false, success ? null : $"退出码 {process.ExitCode}");
    }

    /// <summary>
    /// 拆分卸载命令行为可执行文件与参数（处理引号）。
    /// </summary>
    /// <param name="commandLine">UninstallString 原文。</param>
    public static (string FileName, string Arguments) SplitCommandLine(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            int closing = trimmed.IndexOf('"', 1);
            if (closing > 1)
            {
                return (trimmed[1..closing], trimmed[(closing + 1)..].Trim());
            }
        }

        int exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            int end = exeIndex + 4;
            return (trimmed[..end], trimmed[end..].Trim());
        }

        int spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0 ? (trimmed, string.Empty) : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }
}

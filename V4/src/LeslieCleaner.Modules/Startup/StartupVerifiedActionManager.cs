using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Modules.Startup.Classification;
using Microsoft.Win32;

namespace LeslieCleaner.Modules.Startup;

/// <summary>用户请求的启动项变更类型。</summary>
public enum StartupActionKind
{
    /// <summary>禁用但保留恢复记录。</summary>
    Disable,

    /// <summary>从系统入口删除；V4 仅对可完整恢复的注册表/启动文件夹项开放。</summary>
    Delete,
}

/// <summary>受控提权请求；只能由启动操作管理器在用户确认后构造。</summary>
/// <param name="RequestId">单次随机请求标识。</param>
/// <param name="ExpiresUtc">短时有效期。</param>
/// <param name="Action">受限操作。</param>
/// <param name="Item">已扫描启动项。</param>
public sealed record StartupElevationRequest(
    Guid RequestId,
    DateTimeOffset ExpiresUtc,
    StartupActionKind Action,
    StartupItem Item);

/// <summary>宿主提供的安全提权通道。</summary>
public interface IStartupElevationBroker
{
    /// <summary>在隔离的提升权限进程中执行受限启动项操作并返回真实结果。</summary>
    Task<StartupChangeResult> ExecuteAsync(
        StartupElevationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>启动项当前系统状态探针。</summary>
public interface IStartupSystemStateProbe
{
    /// <summary>重新读取系统状态；返回当前是否启用。</summary>
    Task<bool> IsEnabledAsync(StartupItem item, CancellationToken cancellationToken = default);
}

/// <summary>
/// 受保护项判断、显式确认、权限状态、提权重试和变更后验证的统一入口。
/// 所有成功结果都必须由 <see cref="IStartupSystemStateProbe"/> 重新读取系统状态确认。
/// </summary>
public sealed class StartupVerifiedActionManager
{
    private readonly IStartupEntryOperations _operations;
    private readonly IStartupSystemStateProbe _stateProbe;
    private readonly IStartupElevationBroker? _elevationBroker;
    private readonly ILogService _logService;

    /// <summary>创建受验证启动操作管理器。</summary>
    public StartupVerifiedActionManager(
        IStartupEntryOperations operations,
        IStartupSystemStateProbe stateProbe,
        ILogService logService,
        IStartupElevationBroker? elevationBroker = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _stateProbe = stateProbe ?? throw new ArgumentNullException(nameof(stateProbe));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _elevationBroker = elevationBroker;
    }

    /// <summary>执行禁用或可恢复删除，并在返回成功前重读系统状态。</summary>
    public async Task<StartupChangeResult> ExecuteAsync(
        StartupItemDetail detail,
        StartupActionKind action,
        bool userConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);
        StartupChangeResult? blocked = ValidateRequest(detail, action, userConfirmed);
        if (blocked is not null)
        {
            return blocked;
        }

        StartupChangeResult raw = await _operations.DisableAsync(detail.Item, cancellationToken)
            .ConfigureAwait(false);
        if (!raw.Success)
        {
            bool requiresElevation = raw.RequiresElevation || IsPermissionFailure(raw.Error);
            return raw with
            {
                RequiresElevation = requiresElevation,
                Verified = false,
                Error = requiresElevation
                    ? $"权限不足，需要管理员权限；可在明确确认后安全提权重试。{raw.Error}"
                    : raw.Error,
            };
        }

        return await VerifyDisabledOrRollbackAsync(detail.Item, action, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 在上次结果为 RequiresElevation 后，由用户再次明确确认才调用宿主提权通道。
    /// 受保护项不会因提权而绕过，结果仍须重新读取系统状态验证。
    /// </summary>
    public async Task<StartupChangeResult> RetryWithElevationAsync(
        StartupItemDetail detail,
        StartupActionKind action,
        bool userConfirmedElevation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);
        StartupChangeResult? blocked = ValidateRequest(detail, action, userConfirmedElevation);
        if (blocked is not null)
        {
            return blocked;
        }

        if (_elevationBroker is null)
        {
            return new StartupChangeResult(detail.Item.Id, false,
                "当前宿主未配置受控提权通道，未执行任何系统变更。",
                RequiresElevation: true);
        }

        var request = new StartupElevationRequest(
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2), action, detail.Item);
        StartupChangeResult elevated = await _elevationBroker.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!elevated.Success)
        {
            return elevated with { Verified = false };
        }

        return await VerifyDisabledOrRollbackAsync(detail.Item, action, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<StartupChangeResult> VerifyDisabledOrRollbackAsync(
        StartupItem item,
        StartupActionKind action,
        CancellationToken cancellationToken)
    {
        bool remainsEnabled = await _stateProbe.IsEnabledAsync(item, cancellationToken)
            .ConfigureAwait(false);
        if (!remainsEnabled)
        {
            _logService.LogEvent(Core.Models.LogEventKind.UserAction,
                action == StartupActionKind.Delete ? "启动项已安全删除并验证" : "启动项已禁用并验证",
                new Dictionary<string, object?>
                {
                    ["ItemId"] = item.Id,
                    ["Verified"] = true,
                    ["Action"] = action.ToString(),
                });
            return new StartupChangeResult(item.Id, true, null, Verified: true);
        }

        StartupChangeResult rollback = await _operations.EnableAsync(item.Id, CancellationToken.None)
            .ConfigureAwait(false);
        _logService.LogEvent(Core.Models.LogEventKind.UserAction, "启动项变更验证失败",
            new Dictionary<string, object?>
            {
                ["ItemId"] = item.Id,
                ["RollbackSucceeded"] = rollback.Success,
            });
        return new StartupChangeResult(item.Id, false,
            $"系统状态复查显示启动项仍然启用；已尝试恢复原状态（成功={rollback.Success}）。",
            Verified: false);
    }

    private static StartupChangeResult? ValidateRequest(
        StartupItemDetail detail,
        StartupActionKind action,
        bool userConfirmed)
    {
        if (!userConfirmed)
        {
            return new StartupChangeResult(detail.Item.Id, false,
                "此操作必须由用户显式确认，默认已取消。", BlockedReason: "MissingExplicitConfirmation");
        }

        string? protectionReason = GetProtectionReason(detail);
        if (protectionReason is not null)
        {
            return new StartupChangeResult(detail.Item.Id, false,
                $"已阻止：{protectionReason} 提权也不会绕过此保护。",
                RequiresElevation: false, Verified: false, BlockedReason: protectionReason);
        }

        if (action == StartupActionKind.Delete &&
            detail.Item.Kind is not (StartupItemKind.RegistryRun or StartupItemKind.StartupFolder))
        {
            const string reason = "该来源缺少可完整重建的删除快照；仅允许禁用，不允许删除";
            return new StartupChangeResult(detail.Item.Id, false, $"已阻止：{reason}。",
                BlockedReason: reason);
        }

        return null;
    }

    private static string? GetProtectionReason(StartupItemDetail detail)
    {
        if (detail.Item.Kind == StartupItemKind.Driver)
        {
            return "驱动启动项受保护，删除或禁用可能导致硬件或系统无法启动";
        }

        if (detail.Classification.Kind == StartupSourceKind.MicrosoftWindows ||
            detail.Protection == DisableProtection.ConfirmSystemCritical)
        {
            return "Microsoft/Windows 系统启动项受保护（" +
                string.Join("；", detail.Classification.Reasons) + "）";
        }

        if (detail.Classification.Kind == StartupSourceKind.HardwareDriver ||
            detail.Protection == DisableProtection.ConfirmHardware)
        {
            return "硬件/OEM 启动项受保护（" +
                string.Join("；", detail.Classification.Reasons) + "）";
        }

        return null;
    }

    private static bool IsPermissionFailure(string? error) =>
        !string.IsNullOrWhiteSpace(error) &&
        (error.Contains("权限不足", StringComparison.OrdinalIgnoreCase) ||
         error.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase) ||
         error.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
         error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Windows 启动项状态重读实现。</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsStartupSystemStateProbe : IStartupSystemStateProbe
{
    private const string ServicesSubPath = @"SYSTEM\CurrentControlSet\Services";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Task.Run(() => IsEnabled(item, cancellationToken), cancellationToken);
    }

    private static bool IsEnabled(StartupItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return item.Kind switch
        {
            StartupItemKind.RegistryRun => IsRegistryValuePresent(item),
            StartupItemKind.StartupFolder => item.Command is not null && File.Exists(item.Command),
            StartupItemKind.ScheduledTask => IsScheduledTaskEnabled(item.Location, cancellationToken),
            StartupItemKind.Service or StartupItemKind.Driver => IsServiceEnabled(item.Location),
            _ => throw new LeslieCleanerException($"无法验证启动项来源: {item.Kind}"),
        };
    }

    private static bool IsRegistryValuePresent(StartupItem item)
    {
        (RegistryHive hive, RegistryView view, string subPath) = ParseRegistryLocation(item.Location);
        string valueName = item.Id[(item.Id.LastIndexOf(':') + 1)..];
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subPath, writable: false);
        return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not null;
    }

    private static bool IsServiceEnabled(string serviceName)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? service = baseKey.OpenSubKey($@"{ServicesSubPath}\{serviceName}", writable: false);
        return service?.GetValue("Start") is int start && start != 4;
    }

    private static bool IsScheduledTaskEnabled(string taskName, CancellationToken cancellationToken)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/query");
        startInfo.ArgumentList.Add("/tn");
        startInfo.ArgumentList.Add(taskName);
        startInfo.ArgumentList.Add("/xml");
        using Process process = Process.Start(startInfo)
            ?? throw new LeslieCleanerException("无法启动 schtasks.exe 进行状态验证。");
        string xml = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        while (!process.WaitForExit(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (process.ExitCode != 0)
        {
            throw new LeslieCleanerException($"计划任务状态验证失败: {error.Trim()}");
        }

        XDocument document = XDocument.Parse(xml);
        XElement? enabled = document.Descendants().FirstOrDefault(element =>
            element.Name.LocalName.Equals("Enabled", StringComparison.Ordinal));
        return enabled is null || !string.Equals(enabled.Value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static (RegistryHive Hive, RegistryView View, string SubPath) ParseRegistryLocation(string location)
    {
        if (location.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryHive.CurrentUser, RegistryView.Registry64, location[5..]);
        }

        if (location.StartsWith(@"HKLM\WOW6432Node\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryHive.LocalMachine, RegistryView.Registry32,
                location[@"HKLM\WOW6432Node\".Length..]);
        }

        if (location.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
        {
            return (RegistryHive.LocalMachine, RegistryView.Registry64, location[5..]);
        }

        throw new LeslieCleanerException($"无法解析注册表启动项位置: {location}");
    }
}
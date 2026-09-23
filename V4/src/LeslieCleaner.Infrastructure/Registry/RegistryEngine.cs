using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// Provides verified registry access in the requested 32-bit or 64-bit view.
/// Every destructive operation requires a matching pre-existing backup.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryEngine : IRegistryEngine
{
    private readonly IBackupService _backupService;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>Initializes the registry engine.</summary>
    /// <param name="backupService">Backup service used to validate destructive operations.</param>
    /// <param name="logService">Structured logging service.</param>
    /// <param name="securityOperationGate">Runtime gate that blocks destructive registry changes after a failed security check.</param>
    public RegistryEngine(IBackupService backupService, ILogService logService, ISecurityOperationGate? securityOperationGate = null)
    {
        _backupService = backupService;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateSubKeysAsync(string keyPath, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = OpenHive(keyPath, out string subPath);
            using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
            return key is null ? [] : key.GetSubKeyNames();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateValuesAsync(string keyPath, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = OpenHive(keyPath, out string subPath);
            using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
            return key is null ? [] : key.GetValueNames();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<object?> ReadValueAsync(string keyPath, string valueName = "", CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = OpenHive(keyPath, out string subPath);
            using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
            return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> KeyExistsAsync(string keyPath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = OpenHive(keyPath, out string subPath);
            if (subPath.Length == 0)
            {
                return true;
            }

            using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
            return key is not null;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteKeyAsync(string keyPath, Guid backupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _logService.LogEvent(LogEventKind.DeleteStart, "Registry key deletion started", RegistryAudit(
            keyPath, valueName: null, backupId));
        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("注册表键删除");
            await VerifyBackupAsync(keyPath, valueName: null, backupId, cancellationToken).ConfigureAwait(false);
            EnsureNotSystemProtected(keyPath);
        }
        catch (LeslieCleanerException exception)
        {
            _logService.LogException(exception, "RegistryEngine.DeleteKey.Precondition", RegistryAudit(
                keyPath, valueName: null, backupId, PreconditionFailureKind(exception)));
            throw;
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException(
                "注册表键删除前置检查", keyPath, exception);
            _logService.LogException(accessException, "RegistryEngine.DeleteKey.Precondition", RegistryAudit(
                keyPath, valueName: null, backupId, accessException.FailureKind));
            throw accessException;
        }

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey hive = OpenHive(keyPath, out string subPath);
                if (subPath.Length == 0)
                {
                    throw new BackupException("Refusing to delete a registry root hive.");
                }

                DeleteSubKeyTree(hive, subPath);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException("注册表键删除", keyPath, exception);
            _logService.LogException(accessException, "RegistryEngine.DeleteKey", RegistryAudit(
                keyPath, valueName: null, backupId, accessException.FailureKind));
            throw accessException;
        }

        bool keyStillExists;
        try
        {
            keyStillExists = await KeyStillExistsAfterRetriesAsync(keyPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException(
                "注册表键删除后回读", keyPath, exception);
            _logService.LogException(accessException, "RegistryEngine.VerifyKeyDeletion", RegistryAudit(
                keyPath, valueName: null, backupId, accessException.FailureKind));
            throw accessException;
        }

        if (keyStillExists)
        {
            bool restored = await TryRestoreAfterVerificationFailureAsync(
                keyPath, valueName: null, backupId, cancellationToken).ConfigureAwait(false);
            BackupException verificationException = CreateRegistryVerificationException(
                "注册表键删除", keyPath, restorationAttempted: true, restored);
            _logService.LogException(verificationException, "RegistryEngine.VerifyKeyDeletion", RegistryAudit(
                keyPath, valueName: null, backupId, verificationException.FailureKind));
            throw verificationException;
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "Registry key deleted after verified backup",
            RegistryAudit(keyPath, valueName: null, backupId));
    }

    /// <inheritdoc />
    public async Task DeleteValueAsync(string keyPath, string valueName, Guid backupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(valueName);
        _logService.LogEvent(LogEventKind.DeleteStart, "Registry value deletion started", RegistryAudit(
            keyPath, valueName, backupId));
        try
        {
            _securityOperationGate?.EnsureSensitiveOperationAllowed("注册表值删除");
            await VerifyBackupAsync(keyPath, valueName, backupId, cancellationToken).ConfigureAwait(false);
            EnsureNotSystemProtected(keyPath);
        }
        catch (LeslieCleanerException exception)
        {
            _logService.LogException(exception, "RegistryEngine.DeleteValue.Precondition", RegistryAudit(
                keyPath, valueName, backupId, PreconditionFailureKind(exception)));
            throw;
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException(
                "注册表值删除前置检查", $"{keyPath}|{valueName}", exception);
            _logService.LogException(accessException, "RegistryEngine.DeleteValue.Precondition", RegistryAudit(
                keyPath, valueName, backupId, accessException.FailureKind));
            throw accessException;
        }

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey hive = OpenHive(keyPath, out string subPath);
                using RegistryKey? key = hive.OpenSubKey(subPath, writable: true);
                if (key is null)
                {
                    return;
                }

                key.DeleteValue(valueName, throwOnMissingValue: false);
                key.Flush();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException(
                "注册表值删除", $"{keyPath}|{valueName}", exception);
            _logService.LogException(accessException, "RegistryEngine.DeleteValue", RegistryAudit(
                keyPath, valueName, backupId, accessException.FailureKind));
            throw accessException;
        }

        bool valueStillExists;
        try
        {
            valueStillExists = await ValueStillExistsAfterRetriesAsync(
                keyPath, valueName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRegistryAccessFailure(exception))
        {
            BackupException accessException = CreateRegistryAccessException(
                "注册表值删除后回读", $"{keyPath}|{valueName}", exception);
            _logService.LogException(accessException, "RegistryEngine.VerifyValueDeletion", RegistryAudit(
                keyPath, valueName, backupId, accessException.FailureKind));
            throw accessException;
        }

        if (valueStillExists)
        {
            bool restored = await TryRestoreAfterVerificationFailureAsync(
                keyPath, valueName, backupId, cancellationToken).ConfigureAwait(false);
            BackupException verificationException = CreateRegistryVerificationException(
                "注册表值删除", $"{keyPath}|{valueName}", restorationAttempted: true, restored);
            _logService.LogException(verificationException, "RegistryEngine.VerifyValueDeletion", RegistryAudit(
                keyPath, valueName, backupId, verificationException.FailureKind));
            throw verificationException;
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "Registry value deleted after verified backup",
            RegistryAudit(keyPath, valueName, backupId));
    }

    /// <summary>
    /// Opens the requested registry root and returns the path below that root.
    /// Plain roots select the 64-bit view; suffix <c>32</c> selects the 32-bit view.
    /// </summary>
    /// <param name="keyPath">Full registry path.</param>
    /// <returns>The root key and subkey path. The caller must dispose the root key.</returns>
    public static (RegistryKey Hive, string SubPath) ParseKeyPath(string keyPath)
    {
        RegistryKey hive = OpenHive(keyPath, out string subPath);
        return (hive, subPath);
    }

    internal static RegistryKey OpenHive(string keyPath, out string subPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        string trimmed = keyPath.Trim().TrimStart('\\');
        int separatorIndex = trimmed.IndexOf('\\');
        string root = separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
        subPath = separatorIndex < 0 ? string.Empty : trimmed[(separatorIndex + 1)..];

        (RegistryHive registryHive, RegistryView registryView) = root.ToUpperInvariant() switch
        {
            "HKLM" or "HKLM64" or "HKEY_LOCAL_MACHINE" => (RegistryHive.LocalMachine, RegistryView.Registry64),
            "HKLM32" or "HKEY_LOCAL_MACHINE32" => (RegistryHive.LocalMachine, RegistryView.Registry32),
            "HKCU" or "HKCU64" or "HKEY_CURRENT_USER" => (RegistryHive.CurrentUser, RegistryView.Registry64),
            "HKCU32" or "HKEY_CURRENT_USER32" => (RegistryHive.CurrentUser, RegistryView.Registry32),
            "HKCR" or "HKCR64" or "HKEY_CLASSES_ROOT" => (RegistryHive.ClassesRoot, RegistryView.Registry64),
            "HKCR32" or "HKEY_CLASSES_ROOT32" => (RegistryHive.ClassesRoot, RegistryView.Registry32),
            "HKU" or "HKU64" or "HKEY_USERS" => (RegistryHive.Users, RegistryView.Registry64),
            "HKU32" or "HKEY_USERS32" => (RegistryHive.Users, RegistryView.Registry32),
            "HKCC" or "HKCC64" or "HKEY_CURRENT_CONFIG" => (RegistryHive.CurrentConfig, RegistryView.Registry64),
            "HKCC32" or "HKEY_CURRENT_CONFIG32" => (RegistryHive.CurrentConfig, RegistryView.Registry32),
            _ => throw new ArgumentException($"Unsupported registry root: {root}", nameof(keyPath)),
        };

        if (registryView == RegistryView.Registry32 && !Environment.Is64BitOperatingSystem)
        {
            throw new PlatformNotSupportedException("The 32-bit registry view is not available on a 32-bit operating system.");
        }

        return RegistryKey.OpenBaseKey(registryHive, registryView);
    }

    private static void EnsureNotSystemProtected(string keyPath)
    {
        if (RegistryProtectionPolicy.IsProtectedPath(keyPath))
        {
            throw new BackupException(
                $"注册表目标位于系统保护区域，禁止删除: {keyPath}",
                OperationFailureKind.ProtectedResource);
        }

        using RegistryKey hive = OpenHive(keyPath, out string subPath);
        using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
        if (RegistryScanner.IsSystemProtectedKey(key))
        {
            throw new BackupException(
                $"注册表目标受 Windows 资源保护，权限不足且禁止强制接管所有权: {keyPath}",
                OperationFailureKind.ProtectedResource);
        }
    }

    private static BackupException CreateRegistryAccessException(string operation, string target, Exception exception)
    {
        bool elevated = IsCurrentProcessElevated();
        OperationFailureKind kind = elevated
            ? OperationFailureKind.ProtectedResource
            : OperationFailureKind.RequiresElevation;
        string guidance = elevated
            ? "当前已是管理员模式；目标可能受系统保护，禁止强制接管所有权"
            : "需要用户确认后以管理员模式重试";
        return new BackupException($"{operation}权限不足: {target}（{guidance}）", exception, kind);
    }

    private static BackupException CreateRegistryVerificationException(
        string operation, string target, bool restorationAttempted, bool restored)
    {
        bool elevated = IsCurrentProcessElevated();
        OperationFailureKind kind = elevated
            ? OperationFailureKind.VerificationFailed
            : OperationFailureKind.RequiresElevation;
        string guidance = elevated
            ? "操作返回后目标仍存在，已按失败处理"
            : "操作返回后目标仍存在，需要用户确认后以管理员模式重试";
        return new BackupException(
            $"{operation}未通过删除后回读: {target}（{guidance}；RestorationAttempted={restorationAttempted}；Restored={restored}）",
            kind);
    }

    private static bool IsRegistryAccessFailure(Exception exception)
    {
        if (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }

        if (exception is IOException ioException)
        {
            int nativeError = ioException.HResult & 0xFFFF;
            return nativeError is 5 or 1314; // ERROR_ACCESS_DENIED / ERROR_PRIVILEGE_NOT_HELD
        }

        return false;
    }

    private static OperationFailureKind PreconditionFailureKind(LeslieCleanerException exception) =>
        exception is BackupException backupException
            ? backupException.FailureKind
            : OperationFailureKind.SecurityGateBlocked;

    private async Task<bool> ValueExistsAsync(
        string keyPath, string valueName, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = await EnumerateValuesAsync(keyPath, cancellationToken).ConfigureAwait(false);
        // Registry value names are case-insensitive even though their original casing is preserved.
        return names.Contains(valueName, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> KeyStillExistsAfterRetriesAsync(
        string keyPath, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (!await KeyExistsAsync(keyPath, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(40 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return true;
    }

    private async Task<bool> ValueStillExistsAfterRetriesAsync(
        string keyPath, string valueName, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (!await ValueExistsAsync(keyPath, valueName, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(40 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return true;
    }

    private static void DeleteSubKeyTree(RegistryKey hive, string subPath)
    {
        int separatorIndex = subPath.LastIndexOf('\\');
        string parentPath = separatorIndex < 0 ? string.Empty : subPath[..separatorIndex];
        string childName = separatorIndex < 0 ? subPath : subPath[(separatorIndex + 1)..];
        if (childName.Length == 0)
        {
            throw new BackupException("Refusing to delete an empty registry key name.",
                OperationFailureKind.InvalidTarget);
        }

        if (parentPath.Length == 0)
        {
            hive.DeleteSubKeyTree(childName, throwOnMissingSubKey: false);
            hive.Flush();
            return;
        }

        using RegistryKey? parent = hive.OpenSubKey(parentPath, writable: true);
        if (parent is null)
        {
            // The target disappeared after backup but before deletion. The post-read remains authoritative.
            return;
        }

        parent.DeleteSubKeyTree(childName, throwOnMissingSubKey: false);
        parent.Flush();
    }

    private static IReadOnlyDictionary<string, object?> RegistryAudit(
        string keyPath,
        string? valueName,
        Guid backupId,
        OperationFailureKind failureKind = OperationFailureKind.None)
    {
        string root = keyPath.Trim().TrimStart('\\').Split('\\', 2)[0];
        return new Dictionary<string, object?>
        {
            ["KeyPath"] = keyPath,
            ["ValueName"] = valueName,
            ["BackupId"] = backupId,
            ["RegistryView"] = root.EndsWith("32", StringComparison.OrdinalIgnoreCase) ? "Registry32" : "Registry64",
            ["Elevated"] = IsCurrentProcessElevated(),
            ["FailureKind"] = failureKind.ToString(),
        };
    }

    private async Task<bool> TryRestoreAfterVerificationFailureAsync(
        string keyPath, string? valueName, Guid backupId, CancellationToken cancellationToken)
    {
        try
        {
            await _backupService.RestoreAsync(backupId, cancellationToken).ConfigureAwait(false);
            bool restored = valueName is null
                ? await KeyExistsAsync(keyPath, cancellationToken).ConfigureAwait(false)
                : await ValueExistsAsync(keyPath, valueName, cancellationToken).ConfigureAwait(false);
            _logService.LogEvent(LogEventKind.RestoreFinish,
                "Registry verification failed; backup restoration attempted",
                new Dictionary<string, object?>
                {
                    ["KeyPath"] = keyPath,
                    ["ValueName"] = valueName,
                    ["BackupId"] = backupId,
                    ["RestorationAttempted"] = true,
                    ["Restored"] = restored,
                });
            return restored;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BackupException)
        {
            _logService.LogException(exception, "RegistryEngine.RestoreAfterVerificationFailure",
                new Dictionary<string, object?>
                {
                    ["KeyPath"] = keyPath,
                    ["ValueName"] = valueName,
                    ["BackupId"] = backupId,
                    ["RestorationAttempted"] = true,
                    ["Restored"] = false,
                });
            return false;
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    private async Task VerifyBackupAsync(string keyPath, string? valueName, Guid backupId, CancellationToken cancellationToken)
    {
        Core.Models.BackupRecord? backup = await _backupService.GetBackupAsync(
            backupId, cancellationToken).ConfigureAwait(false);
        bool matches = backup is not null &&
            backup.Type == Core.Models.BackupType.Registry &&
            backup.Status is Core.Models.BackupStatus.Completed or Core.Models.BackupStatus.Restored &&
            RegistryPathsMatch(backup.RegistryKeyPath, keyPath) &&
            (valueName is null
                ? backup.Kind == Core.Models.RegistryBackupKind.Key
                : backup.Kind == Core.Models.RegistryBackupKind.Value && string.Equals(
                    backup.ValueName, valueName, StringComparison.OrdinalIgnoreCase));
        if (!matches)
        {
            throw new BackupException(
                $"删除前必须存在范围完全匹配的注册表备份: {keyPath}{(valueName is null ? string.Empty : "|" + valueName)}",
                OperationFailureKind.BackupFailed);
        }

        Core.Models.BackupVerificationResult verification = await _backupService.VerifyAsync(
            backupId, cancellationToken).ConfigureAwait(false);
        if (!verification.IsValid)
        {
            throw new BackupException(
                $"注册表备份完整性校验失败，已拒绝删除: {keyPath}{(valueName is null ? string.Empty : "|" + valueName)}；{verification.Error}",
                OperationFailureKind.BackupFailed);
        }
    }

    private static bool RegistryPathsMatch(string storedPath, string requestedPath) =>
        string.Equals(CanonicalizeRegistryPath(storedPath), CanonicalizeRegistryPath(requestedPath),
            StringComparison.OrdinalIgnoreCase);

    private static string CanonicalizeRegistryPath(string path)
    {
        string trimmed = path.Trim().TrimStart('\\');
        int separator = trimmed.IndexOf('\\');
        string root = separator < 0 ? trimmed : trimmed[..separator];
        string suffix = separator < 0 ? string.Empty : trimmed[separator..];
        string canonicalRoot = root.ToUpperInvariant() switch
        {
            "HKLM" or "HKLM64" or "HKEY_LOCAL_MACHINE" => "HKLM",
            "HKLM32" or "HKEY_LOCAL_MACHINE32" => "HKLM32",
            "HKCU" or "HKCU64" or "HKEY_CURRENT_USER" => "HKCU",
            "HKCU32" or "HKEY_CURRENT_USER32" => "HKCU32",
            "HKCR" or "HKCR64" or "HKEY_CLASSES_ROOT" => "HKCR",
            "HKCR32" or "HKEY_CLASSES_ROOT32" => "HKCR32",
            "HKU" or "HKU64" or "HKEY_USERS" => "HKU",
            "HKU32" or "HKEY_USERS32" => "HKU32",
            "HKCC" or "HKCC64" or "HKEY_CURRENT_CONFIG" => "HKCC",
            "HKCC32" or "HKEY_CURRENT_CONFIG32" => "HKCC32",
            _ => root.ToUpperInvariant(),
        };
        return canonicalRoot + suffix;
    }
}

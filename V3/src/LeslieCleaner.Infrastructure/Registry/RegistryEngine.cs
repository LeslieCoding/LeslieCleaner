using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
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
        _securityOperationGate?.EnsureSensitiveOperationAllowed("注册表键删除");
        await VerifyBackupAsync(keyPath, valueName: null, backupId, cancellationToken).ConfigureAwait(false);

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

                hive.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new BackupException($"注册表键删除权限不足，需要管理员权限: {keyPath} ({exception.Message})");
        }

        if (await KeyExistsAsync(keyPath, cancellationToken).ConfigureAwait(false))
        {
            throw new BackupException($"注册表键删除未生效，权限不足或需要管理员权限: {keyPath}");
        }

        _logService.LogEvent(Core.Models.LogEventKind.DeleteFinish, "Registry key deleted after verified backup", new Dictionary<string, object?>
        {
            ["KeyPath"] = keyPath,
            ["BackupId"] = backupId,
        });
    }

    /// <inheritdoc />
    public async Task DeleteValueAsync(string keyPath, string valueName, Guid backupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(valueName);
        _securityOperationGate?.EnsureSensitiveOperationAllowed("注册表值删除");
        await VerifyBackupAsync(keyPath, valueName, backupId, cancellationToken).ConfigureAwait(false);

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey hive = OpenHive(keyPath, out string subPath);
                using RegistryKey? key = hive.OpenSubKey(subPath, writable: true);
                if (key is null)
                {
                    throw new BackupException($"Registry key does not exist: {keyPath}");
                }

                key.DeleteValue(valueName, throwOnMissingValue: false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new BackupException($"注册表值删除权限不足，需要管理员权限: {keyPath}|{valueName} ({exception.Message})");
        }

        object? current = await ReadValueAsync(keyPath, valueName, cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            throw new BackupException($"注册表值删除未生效，权限不足或需要管理员权限: {keyPath}|{valueName}");
        }

        _logService.LogEvent(Core.Models.LogEventKind.DeleteFinish, "Registry value deleted after verified backup", new Dictionary<string, object?>
        {
            ["KeyPath"] = keyPath,
            ["ValueName"] = valueName,
            ["BackupId"] = backupId,
        });
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

    private async Task VerifyBackupAsync(string keyPath, string? valueName, Guid backupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Core.Models.BackupRecord> backups = await _backupService.ListBackupsAsync(cancellationToken).ConfigureAwait(false);
        bool matches = backups.Any(backup =>
            backup.Id == backupId &&
            string.Equals(backup.RegistryKeyPath, keyPath, StringComparison.OrdinalIgnoreCase) &&
            (valueName is null
                ? backup.Kind == Core.Models.RegistryBackupKind.Key
                : backup.Kind == Core.Models.RegistryBackupKind.Key ||
                  (backup.Kind == Core.Models.RegistryBackupKind.Value && string.Equals(backup.ValueName, valueName, StringComparison.Ordinal))));
        if (!matches)
        {
            throw new BackupException($"A matching registry backup is required before deletion: {keyPath}{(valueName is null ? string.Empty : "|" + valueName)}");
        }
    }
}

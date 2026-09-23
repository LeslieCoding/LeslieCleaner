using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Registry;
using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// Creates durable registry backups. Key backups use the registry tool matching the selected view;
/// value backups store a typed snapshot so restoration never overwrites sibling values.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryBackupService : IBackupService
{
    private const string RegistrySubdirectory = "Registry";
    private const string MetadataFileName = "metadata.json";
    private static readonly TimeSpan RegProcessTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly IPathService _pathService;
    private readonly ILogService _logService;

    /// <summary>Initializes the registry backup service.</summary>
    /// <param name="pathService">Application storage locations.</param>
    /// <param name="logService">Structured logging service.</param>
    public RegistryBackupService(IPathService pathService, ILogService logService)
    {
        _pathService = pathService;
        _logService = logService;
    }

    /// <summary>Gets the storage directory for a backup.</summary>
    /// <param name="backupId">Backup identifier.</param>
    /// <returns>Absolute backup directory.</returns>
    public string GetBackupDirectory(Guid backupId) =>
        Path.Combine(_pathService.BackupDirectory, RegistrySubdirectory, backupId.ToString("N"));

    /// <summary>Gets the .reg file path for a key backup.</summary>
    /// <param name="registryKeyPath">Registry key path.</param>
    /// <param name="backupId">Backup identifier.</param>
    /// <returns>Absolute .reg file path.</returns>
    public string GetBackupFilePath(string registryKeyPath, Guid backupId) =>
        Path.Combine(GetBackupDirectory(backupId), SanitizeFileName(registryKeyPath, backupId) + ".reg");

    /// <summary>Gets the metadata path for a backup.</summary>
    /// <param name="backupId">Backup identifier.</param>
    /// <returns>Absolute metadata path.</returns>
    public string GetMetadataFilePath(Guid backupId) =>
        Path.Combine(GetBackupDirectory(backupId), MetadataFileName);

    /// <inheritdoc />
    public async Task<BackupRecord> BackupRegistryKeyAsync(string registryKeyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryKeyPath);
        var backupId = Guid.NewGuid();
        string backupFilePath = GetBackupFilePath(registryKeyPath, backupId);
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);

        RegistryView registryView = GetRegistryView(registryKeyPath);
        await RunRegAsync(registryView, ["export", NormalizeRootName(registryKeyPath), backupFilePath, "/y"], cancellationToken)
            .ConfigureAwait(false);

        var record = new BackupRecord(
            backupId,
            registryKeyPath,
            backupFilePath,
            GetMetadataFilePath(backupId),
            DateTimeOffset.UtcNow,
            new FileInfo(backupFilePath).Length,
            RegistryBackupKind.Key);
        await WriteRecordAsync(record, cancellationToken).ConfigureAwait(false);
        LogBackup(record);
        return record;
    }

    /// <inheritdoc />
    public async Task<BackupRecord> BackupRegistryValueAsync(
        string registryKeyPath,
        string valueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryKeyPath);
        ArgumentNullException.ThrowIfNull(valueName);
        cancellationToken.ThrowIfCancellationRequested();

        RegistryValueSnapshot snapshot = await Task.Run(() => CaptureValue(registryKeyPath, valueName), cancellationToken)
            .ConfigureAwait(false);
        var backupId = Guid.NewGuid();
        string backupDirectory = GetBackupDirectory(backupId);
        Directory.CreateDirectory(backupDirectory);
        string snapshotPath = Path.Combine(backupDirectory, "value.json");
        await WriteJsonAtomicallyAsync(snapshotPath, snapshot, cancellationToken).ConfigureAwait(false);

        var record = new BackupRecord(
            backupId,
            registryKeyPath,
            snapshotPath,
            GetMetadataFilePath(backupId),
            DateTimeOffset.UtcNow,
            new FileInfo(snapshotPath).Length,
            RegistryBackupKind.Value,
            valueName,
            snapshot);
        await WriteRecordAsync(record, cancellationToken).ConfigureAwait(false);
        LogBackup(record);
        return record;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        BackupRecord record = await ReadRecordAsync(backupId, cancellationToken).ConfigureAwait(false);
        _logService.LogEvent(LogEventKind.RestoreStart, "Registry backup restoration started", new Dictionary<string, object?>
        {
            ["BackupId"] = backupId,
            ["RegistryKey"] = record.RegistryKeyPath,
            ["Kind"] = record.Kind.ToString(),
        });

        if (record.Kind == RegistryBackupKind.Key)
        {
            if (!File.Exists(record.FilePath))
            {
                throw new BackupException($"Registry backup file is missing: {record.FilePath}");
            }

            await RunRegAsync(GetRegistryView(record.RegistryKeyPath), ["import", record.FilePath], cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            RegistryValueSnapshot snapshot = record.ValueSnapshot
                ?? throw new BackupException($"Registry value backup metadata is incomplete: {record.MetadataPath}");
            await Task.Run(() => RestoreValue(record.RegistryKeyPath, record.ValueName ?? string.Empty, snapshot), cancellationToken)
                .ConfigureAwait(false);
        }

        _logService.LogEvent(LogEventKind.RestoreFinish, "Registry backup restoration completed", new Dictionary<string, object?>
        {
            ["BackupId"] = backupId,
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(_pathService.BackupDirectory, RegistrySubdirectory);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var records = new List<BackupRecord>();
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string metadataPath = Path.Combine(directory, MetadataFileName);
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            try
            {
                await using FileStream stream = new(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                BackupRecord? record = await JsonSerializer.DeserializeAsync<BackupRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                _logService.LogException(exception, "RegistryBackupService.ListBackupsAsync", new Dictionary<string, object?>
                {
                    ["MetadataPath"] = metadataPath,
                });
            }
        }

        records.Sort(static (left, right) => right.CreatedUtc.CompareTo(left.CreatedUtc));
        return records;
    }

    private static RegistryValueSnapshot CaptureValue(string registryKeyPath, string valueName)
    {
        using RegistryKey hive = RegistryEngine.OpenHive(registryKeyPath, out string subPath);
        using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
        if (key is null || !key.GetValueNames().Contains(valueName, StringComparer.Ordinal))
        {
            return new RegistryValueSnapshot(false, null, null, null, null, null, null);
        }

        RegistryValueKind kind = key.GetValueKind(valueName);
        object? value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => new RegistryValueSnapshot(true, kind.ToString(), (string?)value, null, null, null, null),
            RegistryValueKind.MultiString => new RegistryValueSnapshot(true, kind.ToString(), null, (string[]?)value, null, null, null),
            RegistryValueKind.Binary or RegistryValueKind.None => new RegistryValueSnapshot(true, kind.ToString(), null, null, Convert.ToBase64String((byte[]?)value ?? []), null, null),
            RegistryValueKind.DWord => new RegistryValueSnapshot(true, kind.ToString(), null, null, null, Convert.ToInt32(value), null),
            RegistryValueKind.QWord => new RegistryValueSnapshot(true, kind.ToString(), null, null, null, null, Convert.ToInt64(value)),
            _ => throw new BackupException($"Unsupported registry value type: {kind}"),
        };
    }

    private static void RestoreValue(string registryKeyPath, string valueName, RegistryValueSnapshot snapshot)
    {
        using RegistryKey hive = RegistryEngine.OpenHive(registryKeyPath, out string subPath);
        using RegistryKey key = hive.CreateSubKey(subPath, writable: true)
            ?? throw new BackupException($"Unable to open registry key for restoration: {registryKeyPath}");
        if (!snapshot.Exists)
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        if (!Enum.TryParse(snapshot.Kind, ignoreCase: false, out RegistryValueKind kind))
        {
            throw new BackupException($"Registry value backup has an unknown type: {snapshot.Kind}");
        }

        object value = kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => snapshot.StringValue ?? string.Empty,
            RegistryValueKind.MultiString => snapshot.StringValues?.ToArray() ?? [],
            RegistryValueKind.Binary or RegistryValueKind.None => string.IsNullOrEmpty(snapshot.BinaryValueBase64) ? [] : Convert.FromBase64String(snapshot.BinaryValueBase64),
            RegistryValueKind.DWord => snapshot.DwordValue ?? 0,
            RegistryValueKind.QWord => snapshot.QwordValue ?? 0L,
            _ => throw new BackupException($"Unsupported registry value type: {kind}"),
        };
        key.SetValue(valueName, value, kind);
    }

    private async Task WriteRecordAsync(BackupRecord record, CancellationToken cancellationToken) =>
        await WriteJsonAtomicallyAsync(record.MetadataPath, record, cancellationToken).ConfigureAwait(false);

    private async Task<BackupRecord> ReadRecordAsync(Guid backupId, CancellationToken cancellationToken)
    {
        string metadataPath = GetMetadataFilePath(backupId);
        if (!File.Exists(metadataPath))
        {
            throw new BackupException($"Registry backup record does not exist: {backupId}");
        }

        await using FileStream stream = new(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        BackupRecord? record = await JsonSerializer.DeserializeAsync<BackupRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return record ?? throw new BackupException($"Registry backup metadata is corrupt: {metadataPath}");
    }

    private static async Task WriteJsonAtomicallyAsync<T>(string path, T content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, content, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task RunRegAsync(RegistryView registryView, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetRegExePath(registryView),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new BackupException($"Unable to start reg.exe for {string.Join(' ', arguments)}");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RegProcessTimeout);
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

            throw new BackupException($"reg.exe timed out after {RegProcessTimeout.TotalSeconds:N0} seconds.");
        }

        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new BackupException($"reg.exe failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static RegistryView GetRegistryView(string registryKeyPath)
    {
        string root = registryKeyPath.Trim().TrimStart('\\').Split('\\', 2)[0];
        return root.EndsWith("32", StringComparison.OrdinalIgnoreCase) ? RegistryView.Registry32 : RegistryView.Registry64;
    }

    private static string NormalizeRootName(string registryKeyPath)
    {
        string trimmed = registryKeyPath.Trim().TrimStart('\\');
        int separatorIndex = trimmed.IndexOf('\\');
        string root = separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
        string suffix = separatorIndex < 0 ? string.Empty : trimmed[separatorIndex..];
        string normalizedRoot = root.ToUpperInvariant() switch
        {
            "HKLM32" or "HKLM64" => "HKLM",
            "HKCU32" or "HKCU64" => "HKCU",
            "HKCR32" or "HKCR64" => "HKCR",
            "HKU32" or "HKU64" => "HKU",
            "HKCC32" or "HKCC64" => "HKCC",
            _ => root,
        };
        return normalizedRoot + suffix;
    }

    private static string GetRegExePath(RegistryView registryView)
    {
        if (registryView == RegistryView.Registry32 && Environment.Is64BitOperatingSystem)
        {
            string x86SystemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
            string x86RegExe = Path.Combine(x86SystemDirectory, "reg.exe");
            if (File.Exists(x86RegExe))
            {
                return x86RegExe;
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "reg.exe");
    }

    private string SanitizeFileName(string registryKeyPath, Guid backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryKeyPath);
        var builder = new StringBuilder(registryKeyPath.Length);
        foreach (char character in registryKeyPath.Replace('\\', '.'))
        {
            builder.Append(Array.IndexOf(InvalidFileNameChars, character) >= 0 ? '_' : character);
        }

        string sanitized = builder.ToString();
        return sanitized.Length > 80 ? sanitized[..80] + "-" + backupId.ToString("N")[..8] : sanitized;
    }

    private void LogBackup(BackupRecord record) => _logService.LogEvent(LogEventKind.DeleteStart, "Registry backup created", new Dictionary<string, object?>
    {
        ["RegistryKey"] = record.RegistryKeyPath,
        ["BackupId"] = record.Id,
        ["Kind"] = record.Kind.ToString(),
        ["SizeBytes"] = record.SizeBytes,
    });
}

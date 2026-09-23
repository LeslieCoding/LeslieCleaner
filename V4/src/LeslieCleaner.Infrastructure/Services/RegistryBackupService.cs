using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Registry;
using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>Unified, durable backup/restore service with legacy registry compatibility.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryBackupService : IBackupService
{
    private const string RegistryDir = "Registry";
    private const string MetadataName = "metadata.json";
    private static readonly string[] ManagedDirs = [RegistryDir, "Files", "Config", "Database"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan RegTimeout = TimeSpan.FromSeconds(30);
    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();
    private readonly IPathService _paths;
    private readonly ILogService _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the unified backup service.</summary>
    public RegistryBackupService(IPathService pathService, ILogService logService) =>
        (_paths, _log) = (pathService, logService);

    /// <summary>Gets the legacy-compatible registry backup directory.</summary>
    public string GetBackupDirectory(Guid id) => Path.Combine(_paths.BackupDirectory, RegistryDir, id.ToString("N"));
    /// <summary>Gets the legacy-compatible registry payload path.</summary>
    public string GetBackupFilePath(string key, Guid id) => Path.Combine(GetBackupDirectory(id), Sanitize(key, id) + ".reg");
    /// <summary>Gets the legacy-compatible registry metadata path.</summary>
    public string GetMetadataFilePath(Guid id) => Path.Combine(GetBackupDirectory(id), MetadataName);
    /// <summary>Gets the unified backup index path.</summary>
    public string GetIndexFilePath() => Path.Combine(_paths.BackupDirectory, "index.json");

    /// <inheritdoc />
    public async Task<BackupRecord> BackupRegistryKeyAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var id = Guid.NewGuid();
        string payload = GetBackupFilePath(key, id);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        try
        {
            await RunRegAsync(GetRegistryView(key), ["export", NormalizeRoot(key), payload, "/y"], token).ConfigureAwait(false);
            var record = new BackupRecord(id, key, payload, GetMetadataFilePath(id), DateTimeOffset.UtcNow,
                new FileInfo(payload).Length, RegistryBackupKind.Key)
            {
                Name = BackupName(BackupType.Registry, id),
                Sha256 = await HashAsync(payload, token).ConfigureAwait(false),
            };
            await CommitAsync(record, token).ConfigureAwait(false);
            LogCreated(record);
            return record;
        }
        catch { DeleteDirectory(Path.GetDirectoryName(payload)!); throw; }
    }

    /// <inheritdoc />
    public async Task<BackupRecord> BackupRegistryValueAsync(string key, string valueName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueName);
        RegistryValueSnapshot snapshot = await Task.Run(() => CaptureValue(key, valueName), token).ConfigureAwait(false);
        string storedValueName = snapshot.OriginalValueName ?? valueName;
        var id = Guid.NewGuid();
        string directory = GetBackupDirectory(id);
        string payload = Path.Combine(directory, "value.json");
        Directory.CreateDirectory(directory);
        try
        {
            await WriteJsonAsync(payload, snapshot, token).ConfigureAwait(false);
            var record = new BackupRecord(id, key, payload, GetMetadataFilePath(id), DateTimeOffset.UtcNow,
                new FileInfo(payload).Length, RegistryBackupKind.Value, storedValueName, snapshot)
            {
                Name = BackupName(BackupType.Registry, id),
                Sha256 = await HashAsync(payload, token).ConfigureAwait(false),
            };
            await CommitAsync(record, token).ConfigureAwait(false);
            LogCreated(record);
            return record;
        }
        catch { DeleteDirectory(directory); throw; }
    }

    /// <inheritdoc />
    public async Task<BackupRecord> BackupFileAsync(string filePath, BackupType type = BackupType.File,
        string? description = null, BackupStorageMode storageMode = BackupStorageMode.FullCopy,
        CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (type == BackupType.Registry) throw new ArgumentException("Use a registry backup method.", nameof(type));
        string source = Path.GetFullPath(filePath);
        if (!File.Exists(source)) throw new BackupException($"Backup source file does not exist: {source}");
        var id = Guid.NewGuid();
        string directory = Path.Combine(_paths.BackupDirectory, TypeDirectory(type), id.ToString("N"));
        string payload = storageMode == BackupStorageMode.MetadataOnly ? string.Empty :
            Path.Combine(directory, BackupName(type, id) + Path.GetExtension(source));
        Directory.CreateDirectory(directory);
        try
        {
            long size = new FileInfo(source).Length;
            string hash = await HashAsync(source, token).ConfigureAwait(false);
            if (storageMode != BackupStorageMode.MetadataOnly)
            {
                await CopyAsync(source, payload, token).ConfigureAwait(false);
                if (new FileInfo(payload).Length != size ||
                    !hash.Equals(await HashAsync(payload, token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                    throw new BackupException("Backup copy failed integrity verification.");
            }
            var record = new BackupRecord(id, string.Empty, payload, Path.Combine(directory, MetadataName),
                DateTimeOffset.UtcNow, size)
            {
                Type = type, Name = BackupName(type, id), Description = description,
                OriginalPath = source, Sha256 = hash, StorageMode = storageMode,
            };
            await CommitAsync(record, token).ConfigureAwait(false);
            if (storageMode == BackupStorageMode.Move) File.Delete(source);
            LogCreated(record);
            return record;
        }
        catch { DeleteDirectory(directory); throw; }
    }

    /// <inheritdoc />
    public Task RestoreAsync(Guid id, CancellationToken token = default) =>
        RestoreAsync(id, RestoreConflictPolicy.Overwrite, token);

    /// <inheritdoc />
    public async Task RestoreAsync(Guid id, RestoreConflictPolicy conflictPolicy, CancellationToken token = default)
    {
        BackupRecord record = await ReadAsync(id, token).ConfigureAwait(false);
        BackupVerificationResult verified = await VerifyRecordAsync(record, token).ConfigureAwait(false);
        if (!verified.IsValid)
        {
            _log.LogEvent(LogEventKind.Exception, "Corrupt backup restoration rejected", Audit(record, verified.Error));
            throw new BackupException($"Backup {id} failed integrity verification: {verified.Error}");
        }
        _log.LogEvent(LogEventKind.RestoreStart, "Backup restoration started", Audit(record));
        try
        {
            if (record.Type == BackupType.Registry) await RestoreRegistryAsync(record, token).ConfigureAwait(false);
            else await RestoreFileAsync(record, conflictPolicy, token).ConfigureAwait(false);
            await CommitAsync(record with { Status = BackupStatus.Restored }, token).ConfigureAwait(false);
            _log.LogEvent(LogEventKind.RestoreFinish, "Backup restoration completed", Audit(record));
        }
        catch (Exception ex) { _log.LogException(ex, "RegistryBackupService.RestoreAsync", Audit(record)); throw; }
    }

    /// <inheritdoc />
    public async Task<BackupRecord?> GetBackupAsync(Guid id, CancellationToken token = default)
    {
        foreach (string dir in ManagedDirs)
        {
            string path = Path.Combine(_paths.BackupDirectory, dir, id.ToString("N"), MetadataName);
            if (File.Exists(path)) return await DeserializeAsync(path, token).ConfigureAwait(false);
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<BackupVerificationResult> VerifyAsync(Guid id, CancellationToken token = default)
    {
        BackupRecord record = await ReadAsync(id, token).ConfigureAwait(false);
        BackupVerificationResult result = await VerifyRecordAsync(record, token).ConfigureAwait(false);
        _log.LogEvent(result.IsValid ? LogEventKind.UserAction : LogEventKind.Exception,
            result.IsValid ? "Backup verified" : "Backup verification failed", Audit(record, result.Error));
        return result;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken token = default)
    {
        BackupRecord record = await ReadAsync(id, token).ConfigureAwait(false);
        if (record.IsLocked) throw new BackupException($"Locked backup cannot be deleted: {id}");
        await DeleteRecordAsync(record, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetLockedAsync(Guid id, bool isLocked, CancellationToken token = default)
    {
        BackupRecord record = await ReadAsync(id, token).ConfigureAwait(false);
        await CommitAsync(record with { IsLocked = isLocked }, token).ConfigureAwait(false);
        _log.LogEvent(LogEventKind.UserAction, isLocked ? "Backup locked" : "Backup unlocked", Audit(record));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            List<BackupRecord> records = await ScanAsync(token).ConfigureAwait(false);
            records.Sort(static (a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
            await WriteJsonAsync(GetIndexFilePath(), records, token).ConfigureAwait(false);
            return records;
        }
        finally { _gate.Release(); }
    }

    /// <inheritdoc />
    public async Task<BackupRetentionResult> ApplyRetentionAsync(BackupRetentionPolicy policy, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MaxAgeDays is < 0 || policy.MaxCount is < 1 || policy.MaxStorageBytes is < 0)
            throw new ArgumentOutOfRangeException(nameof(policy));
        List<BackupRecord> records = [.. await ListBackupsAsync(token).ConfigureAwait(false)];
        if (records.Count <= 1) return new BackupRetentionResult(0, 0, []);
        BackupRecord newest = records[0];
        var delete = new HashSet<Guid>();
        if (policy.MaxAgeDays.HasValue)
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-policy.MaxAgeDays.Value);
            foreach (BackupRecord item in records.Skip(1).Where(x => !x.IsLocked && x.CreatedUtc < cutoff)) delete.Add(item.Id);
        }
        if (policy.MaxCount.HasValue)
            foreach (BackupRecord item in records.Where(x => !x.IsLocked).Skip(policy.MaxCount.Value))
                if (item.Id != newest.Id) delete.Add(item.Id);
        if (policy.MaxStorageBytes.HasValue)
        {
            long retained = records.Sum(x => x.SizeBytes);
            foreach (BackupRecord item in records.AsEnumerable().Reverse())
            {
                if (retained <= policy.MaxStorageBytes.Value) break;
                if (delete.Contains(item.Id)) { retained -= item.SizeBytes; continue; }
                if (item.Id == newest.Id || item.IsLocked) continue;
                delete.Add(item.Id); retained -= item.SizeBytes;
            }
        }
        long freed = 0;
        var ids = new List<Guid>();
        foreach (BackupRecord item in records.AsEnumerable().Reverse().Where(x => delete.Contains(x.Id)))
        {
            await DeleteRecordAsync(item, token).ConfigureAwait(false);
            freed += item.SizeBytes; ids.Add(item.Id);
        }
        _log.LogEvent(LogEventKind.UserAction, "Backup retention applied", new Dictionary<string, object?>
            { ["DeletedCount"] = ids.Count, ["FreedBytes"] = freed });
        return new BackupRetentionResult(ids.Count, freed, ids);
    }

    private async Task RestoreRegistryAsync(BackupRecord record, CancellationToken token)
    {
        try
        {
            if (record.Kind == RegistryBackupKind.Value)
                await BackupRegistryValueAsync(record.RegistryKeyPath, record.ValueName ?? string.Empty, token).ConfigureAwait(false);
            else if (RegistryKeyExists(record.RegistryKeyPath))
                await BackupRegistryKeyAsync(record.RegistryKeyPath, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupException)
        { throw new BackupException("Unable to create current-state backup before registry restore.", ex); }

        if (record.Kind == RegistryBackupKind.Key)
        {
            await RunRegAsync(GetRegistryView(record.RegistryKeyPath), ["import", record.FilePath], token).ConfigureAwait(false);
            if (!RegistryKeyExists(record.RegistryKeyPath)) throw new BackupException("Registry restore validation failed.");
        }
        else
        {
            RegistryValueSnapshot snapshot = record.ValueSnapshot ?? await ReadSnapshotAsync(record.FilePath, token).ConfigureAwait(false);
            string valueName = snapshot.OriginalValueName ?? record.ValueName ?? string.Empty;
            await Task.Run(() => RestoreValue(record.RegistryKeyPath, valueName, snapshot), token).ConfigureAwait(false);
            if (!SnapshotsEqual(CaptureValue(record.RegistryKeyPath, valueName), snapshot))
                throw new BackupException("Registry value restore validation failed.");
        }
    }

    private async Task RestoreFileAsync(BackupRecord record, RestoreConflictPolicy policy, CancellationToken token)
    {
        if (record.StorageMode == BackupStorageMode.MetadataOnly || string.IsNullOrWhiteSpace(record.FilePath))
            throw new BackupException("Metadata-only backups cannot restore file content.");
        string target = record.OriginalPath ?? throw new BackupException("Backup metadata has no original path.");
        if (File.Exists(target))
        {
            if (policy == RestoreConflictPolicy.Fail) throw new BackupException($"Restore target already exists: {target}");
            if (policy == RestoreConflictPolicy.Rename) target = AvailablePath(target);
            else await BackupFileAsync(target, record.Type, $"Pre-restore snapshot for {record.Id}",
                BackupStorageMode.FullCopy, token).ConfigureAwait(false);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".restore.tmp";
        try
        {
            await CopyAsync(record.FilePath, temp, token).ConfigureAwait(false);
            if (!record.Sha256.Equals(await HashAsync(temp, token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                throw new BackupException("Restored file failed SHA-256 validation.");
            File.Move(temp, target, overwrite: policy == RestoreConflictPolicy.Overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private async Task<BackupVerificationResult> VerifyRecordAsync(BackupRecord record, CancellationToken token)
    {
        if (!File.Exists(record.MetadataPath)) return new(record.Id, false, "Metadata file is missing.", 0, null);
        if (record.StorageMode == BackupStorageMode.MetadataOnly && record.Type != BackupType.Registry)
        {
            bool validMetadata = record.SizeBytes >= 0 && ValidHash(record.Sha256);
            return new(record.Id, validMetadata, validMetadata ? null : "Metadata-only record is incomplete.",
                record.SizeBytes, record.Sha256);
        }
        if (string.IsNullOrWhiteSpace(record.FilePath) || !File.Exists(record.FilePath))
            return new(record.Id, false, "Payload file is missing.", 0, null);
        try
        {
            long size = new FileInfo(record.FilePath).Length;
            string hash = await HashAsync(record.FilePath, token).ConfigureAwait(false);
            bool hashOk = string.IsNullOrWhiteSpace(record.Sha256) || record.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase);
            bool valid = size == record.SizeBytes && hashOk;
            string? error = size != record.SizeBytes ? "Payload size differs from metadata." :
                !hashOk ? "Payload SHA-256 differs from metadata." : null;
            return new(record.Id, valid, error, size, hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(record.Id, false, $"Payload is unreadable: {ex.Message}", 0, null); }
    }

    private async Task CommitAsync(BackupRecord record, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await WriteJsonAsync(record.MetadataPath, record, token).ConfigureAwait(false);
            List<BackupRecord> records = await ScanAsync(token).ConfigureAwait(false);
            records.RemoveAll(x => x.Id == record.Id); records.Add(record);
            records.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
            await WriteJsonAsync(GetIndexFilePath(), records, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task DeleteRecordAsync(BackupRecord record, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        DeleteDirectory(Path.GetDirectoryName(record.MetadataPath)!);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { await WriteJsonAsync(GetIndexFilePath(), await ScanAsync(token).ConfigureAwait(false), token).ConfigureAwait(false); }
        finally { _gate.Release(); }
        _log.LogEvent(LogEventKind.UserAction, "Backup deleted", Audit(record));
    }

    private async Task<BackupRecord> ReadAsync(Guid id, CancellationToken token) =>
        await GetBackupAsync(id, token).ConfigureAwait(false) ?? throw new BackupException($"Backup record does not exist: {id}");

    private async Task<List<BackupRecord>> ScanAsync(CancellationToken token)
    {
        var records = new List<BackupRecord>();
        foreach (string sub in ManagedDirs)
        {
            string root = Path.Combine(_paths.BackupDirectory, sub);
            if (!Directory.Exists(root)) continue;
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                token.ThrowIfCancellationRequested();
                string path = Path.Combine(directory, MetadataName);
                if (!File.Exists(path)) continue;
                try
                {
                    BackupRecord? record = await DeserializeAsync(path, token).ConfigureAwait(false);
                    if (record is not null && Contained(record, path)) records.Add(record);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or BackupException)
                { _log.LogException(ex, "RegistryBackupService.ScanAsync", new Dictionary<string, object?> { ["MetadataPath"] = path }); }
            }
        }
        return records;
    }

    private async Task<BackupRecord?> DeserializeAsync(string path, CancellationToken token)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            BackupRecord? record = await JsonSerializer.DeserializeAsync<BackupRecord>(stream, JsonOptions, token).ConfigureAwait(false);
            if (record is null) return null;
            return record with { MetadataPath = path, Name = string.IsNullOrWhiteSpace(record.Name) ? BackupName(record.Type, record.Id) : record.Name };
        }
        catch (JsonException ex) { throw new BackupException($"Backup metadata is corrupt: {path}", ex); }
    }

    private bool Contained(BackupRecord record, string metadataPath)
    {
        string root = Path.GetFullPath(_paths.BackupDirectory) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(metadataPath).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(record.FilePath) &&
            !Path.GetFullPath(record.FilePath).StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogEvent(LogEventKind.Exception, "Rejected backup metadata with external payload path", Audit(record));
            return false;
        }
        return true;
    }

    private static async Task CopyAsync(string source, string destination, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, 131072, token).ConfigureAwait(false);
        await output.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static RegistryValueSnapshot CaptureValue(string keyPath, string valueName)
    {
        using RegistryKey hive = RegistryEngine.OpenHive(keyPath, out string subPath);
        using RegistryKey? key = hive.OpenSubKey(subPath, writable: false);
        string? originalValueName = key?.GetValueNames().FirstOrDefault(
            candidate => string.Equals(candidate, valueName, StringComparison.OrdinalIgnoreCase));
        if (key is null || originalValueName is null)
            return new(false, null, null, null, null, null, null);
        RegistryValueKind kind = key.GetValueKind(originalValueName);
        object? value = key.GetValue(originalValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        RegistryValueSnapshot snapshot = kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => new(true, kind.ToString(), (string?)value, null, null, null, null),
            RegistryValueKind.MultiString => new(true, kind.ToString(), null, (string[]?)value, null, null, null),
            RegistryValueKind.Binary or RegistryValueKind.None => new(true, kind.ToString(), null, null, Convert.ToBase64String((byte[]?)value ?? []), null, null),
            RegistryValueKind.DWord => new(true, kind.ToString(), null, null, null, Convert.ToInt32(value), null),
            RegistryValueKind.QWord => new(true, kind.ToString(), null, null, null, null, Convert.ToInt64(value)),
            _ => throw new BackupException($"Unsupported registry value type: {kind}"),
        };
        return snapshot with { OriginalValueName = originalValueName };
    }

    private static void RestoreValue(string keyPath, string valueName, RegistryValueSnapshot snapshot)
    {
        using RegistryKey hive = RegistryEngine.OpenHive(keyPath, out string subPath);
        using RegistryKey key = hive.CreateSubKey(subPath, writable: true) ?? throw new BackupException("Unable to open registry key.");
        if (!snapshot.Exists) { key.DeleteValue(valueName, false); return; }
        if (!Enum.TryParse(snapshot.Kind, false, out RegistryValueKind kind)) throw new BackupException("Unknown registry value type.");
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

    private static bool SnapshotsEqual(RegistryValueSnapshot a, RegistryValueSnapshot b) =>
        a.Exists == b.Exists && a.Kind == b.Kind && a.StringValue == b.StringValue &&
        (a.StringValues ?? []).SequenceEqual(b.StringValues ?? []) && a.BinaryValueBase64 == b.BinaryValueBase64 &&
        a.DwordValue == b.DwordValue && a.QwordValue == b.QwordValue;

    private static async Task<RegistryValueSnapshot> ReadSnapshotAsync(string path, CancellationToken token)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RegistryValueSnapshot>(stream, JsonOptions, token).ConfigureAwait(false)
            ?? throw new BackupException("Registry value snapshot is corrupt.");
    }

    private static bool RegistryKeyExists(string path)
    {
        using RegistryKey hive = RegistryEngine.OpenHive(path, out string subPath);
        using RegistryKey? key = hive.OpenSubKey(subPath, false);
        return key is not null;
    }

    private async Task RunRegAsync(RegistryView view, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var info = new ProcessStartInfo
        {
            FileName = RegExe(view), CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardError = true, RedirectStandardOutput = true,
        };
        foreach (string arg in arguments) info.ArgumentList.Add(arg);
        using Process process = Process.Start(info) ?? throw new BackupException("Unable to start reg.exe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RegTimeout);
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw new BackupException("reg.exe timed out.");
        }
        if (process.ExitCode != 0)
            throw new BackupException($"reg.exe failed with exit code {process.ExitCode}: {await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false)}");
    }

    private static RegistryView GetRegistryView(string path) =>
        path.Trim().TrimStart('\\').Split('\\', 2)[0].EndsWith("32", StringComparison.OrdinalIgnoreCase)
            ? RegistryView.Registry32 : RegistryView.Registry64;

    private static string NormalizeRoot(string path)
    {
        string text = path.Trim().TrimStart('\\');
        int separator = text.IndexOf('\\');
        string root = separator < 0 ? text : text[..separator];
        string suffix = separator < 0 ? string.Empty : text[separator..];
        root = root.ToUpperInvariant() switch
        { "HKLM32" or "HKLM64" => "HKLM", "HKCU32" or "HKCU64" => "HKCU", "HKCR32" or "HKCR64" => "HKCR",
          "HKU32" or "HKU64" => "HKU", "HKCC32" or "HKCC64" => "HKCC", _ => root };
        return root + suffix;
    }

    private static string RegExe(RegistryView view)
    {
        if (view == RegistryView.Registry32 && Environment.Is64BitOperatingSystem)
        {
            string x86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "reg.exe");
            if (File.Exists(x86)) return x86;
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "reg.exe");
    }

    private string Sanitize(string key, Guid id)
    {
        var builder = new StringBuilder(key.Length);
        foreach (char c in key.Replace('\\', '.')) builder.Append(Array.IndexOf(InvalidNameChars, c) >= 0 ? '_' : c);
        string name = builder.ToString();
        return name.Length > 80 ? name[..80] + "-" + id.ToString("N")[..8] : name;
    }

    private static string TypeDirectory(BackupType type) => type switch
    { BackupType.File => "Files", BackupType.Configuration => "Config", BackupType.Database => "Database", _ => RegistryDir };
    private static string BackupName(BackupType type, Guid id) => $"Backup_{type}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{id.ToString("N")[..8]}";
    private static bool ValidHash(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
    private static string AvailablePath(string target)
    {
        string directory = Path.GetDirectoryName(target)!; string stem = Path.GetFileNameWithoutExtension(target); string ext = Path.GetExtension(target);
        for (int i = 1; ; i++) { string candidate = Path.Combine(directory, $"{stem}.restored-{i}{ext}"); if (!File.Exists(candidate)) return candidate; }
    }
    private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }
    private static IReadOnlyDictionary<string, object?> Audit(BackupRecord record, string? error = null) =>
        new Dictionary<string, object?> { ["BackupId"] = record.Id, ["BackupType"] = record.Type.ToString(),
            ["Name"] = record.Name, ["SizeBytes"] = record.SizeBytes, ["Status"] = record.Status.ToString(), ["Error"] = error };
    private void LogCreated(BackupRecord record) => _log.LogEvent(LogEventKind.UserAction, "Backup created", Audit(record));
}


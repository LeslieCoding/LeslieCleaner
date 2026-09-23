using System.Text.Json;
using System.Text.RegularExpressions;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner.Scanning;

namespace LeslieCleaner.Modules.Cleaner;

/// <summary>
/// 清理引擎实现。文件项经 <see cref="IQuarantineService"/> 进隔离区；
/// 注册表项先 <see cref="IBackupService"/> 备份再经 <see cref="IRegistryEngine"/> 删除
/// （优先使用类型化键路径/值名，兼容旧版分隔符编码）。每次清理写入操作清单
/// （%ProgramData%\LeslieCleaner\Operations\{operationId}.json），
/// <see cref="RollbackAsync"/> 凭清单完整回滚。
/// </summary>
public sealed class CleanEngine : ICleanEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Regex OperationIdPattern = new("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant);

    private readonly IQuarantineService _quarantineService;
    private readonly IBackupService _backupService;
    private readonly IRegistryEngine? _registryEngine;
    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly ISystemRecycleBinService? _recycleBinService;
    private readonly ISecurityOperationGate? _securityOperationGate;
    private readonly ICleanupTargetPolicy? _cleanupTargetPolicy;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    /// <summary>以隔离区、备份、注册表引擎、路径与日志服务创建清理引擎。</summary>
    /// <param name="quarantineService">隔离区服务。</param>
    /// <param name="backupService">备份服务。</param>
    /// <param name="registryEngine">注册表引擎；为 <see langword="null"/> 时注册表项记为失败。</param>
    /// <param name="pathService">路径服务。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="recycleBinService">Windows Shell 回收站服务；为空时拒绝清空回收站。</param>
    /// <param name="securityOperationGate">启动安全检查失败时阻止破坏性清理的运行时闸门。</param>
    /// <param name="cleanupTargetPolicy">文件执行前的 WRP、重解析点与应用核心边界策略。</param>
    public CleanEngine(
        IQuarantineService quarantineService,
        IBackupService backupService,
        IRegistryEngine? registryEngine,
        IPathService pathService,
        ILogService logService,
        ISystemRecycleBinService? recycleBinService = null,
        ISecurityOperationGate? securityOperationGate = null,
        ICleanupTargetPolicy? cleanupTargetPolicy = null)
    {
        _quarantineService = quarantineService;
        _backupService = backupService;
        _registryEngine = registryEngine;
        _pathService = pathService;
        _logService = logService;
        _recycleBinService = recycleBinService;
        _securityOperationGate = securityOperationGate;
        _cleanupTargetPolicy = cleanupTargetPolicy;
    }

    /// <inheritdoc />
    public async Task<CleanResult> CleanAsync(
        CleanRequest request,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _securityOperationGate?.EnsureSensitiveOperationAllowed("系统清理");
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await CleanCoreAsync(request, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<CleanResult> CleanCoreAsync(
        CleanRequest request,
        IProgress<CleanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string operationId = request.OperationId ?? Guid.NewGuid().ToString("N");
        ValidateOperationId(operationId);

        if (request.CreateRestorePoint)
        {
            _logService.LogEvent(LogEventKind.UserAction, "清理请求系统还原点（由调用方经 ISystemRestorePointService 创建）",
                new Dictionary<string, object?> { ["OperationId"] = operationId });
        }

        var cleanedItems = new List<CleanedItem>();
        var quarantineEntryIds = new List<Guid>();
        var registryBackupIds = new List<Guid>();
        long freedBytes = 0;

        List<ScannedItem> recycleBinItems = [.. request.Items.Where(static i => i.Category == SmartScanTargets.RecycleBinCategoryId)];
        List<ScannedItem> fileItems = [.. request.Items.Where(static i =>
            i.Category is not SmartScanTargets.RegistryCategoryId and not SmartScanTargets.RecycleBinCategoryId)
            .GroupBy(static item => Path.GetFullPath(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())];
        List<ScannedItem> registryItems = [.. request.Items.Where(static i => i.Category == SmartScanTargets.RegistryCategoryId)
            .GroupBy(BuildRegistryTargetIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())];

        _logService.LogEvent(LogEventKind.DeleteStart, "清理开始", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["FileItems"] = fileItems.Count,
            ["RegistryItems"] = registryItems.Count,
            ["RecycleBinItems"] = recycleBinItems.Sum(static item => item.ItemCount),
        });

        await WriteManifestAsync(new OperationManifest(
            operationId, DateTimeOffset.UtcNow, quarantineEntryIds, registryBackupIds), cancellationToken).ConfigureAwait(false);

        // 文件项 → 隔离区。只有原路径消失、隔离载荷完整且清单持久化后才报告成功。
        for (int index = 0; index < fileItems.Count; index++)
        {
            ScannedItem item = fileItems[index];
            cancellationToken.ThrowIfCancellationRequested();
            QuarantineEntry? entry = null;
            if (_cleanupTargetPolicy is not null)
            {
                CleanupTargetDecision targetDecision = _cleanupTargetPolicy.EvaluateFile(item.Path);
                if (!targetDecision.Allowed)
                {
                    cleanedItems.Add(new CleanedItem(item, null, null, Success: false, Error: targetDecision.Reason)
                    {
                        FailureKind = targetDecision.FailureKind,
                        WasVerified = true,
                    });
                    _logService.LogEvent(LogEventKind.UserAction, "文件清理被最终安全策略阻止",
                        new Dictionary<string, object?>
                        {
                            ["Target"] = targetDecision.NormalizedPath ?? item.Path,
                            ["FailureKind"] = targetDecision.FailureKind.ToString(),
                            ["Reason"] = targetDecision.Reason,
                        });
                    progress?.Report(new CleanProgress(index + 1, request.Items.Count, item.Path));
                    continue;
                }
            }

            try
            {
                entry = await _quarantineService
                    .QuarantineFileAsync(item.Path, request.Reason, operationId, cancellationToken)
                    .ConfigureAwait(false);
                if (File.Exists(Path.GetFullPath(item.Path)))
                {
                    throw new QuarantineException(
                        $"清理复验失败，原文件仍存在: {item.Path}", OperationFailureKind.VerificationFailed);
                }

                quarantineEntryIds.Add(entry.Id);
                try
                {
                    await WriteManifestAsync(new OperationManifest(
                        operationId, DateTimeOffset.UtcNow, quarantineEntryIds, registryBackupIds), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    quarantineEntryIds.Remove(entry.Id);
                    await _quarantineService.RestoreAsync(
                        entry.Id, RestoreConflictPolicy.Overwrite, CancellationToken.None).ConfigureAwait(false);
                    entry = null;
                    throw;
                }

                freedBytes += item.AllocatedSizeBytes ?? item.SizeBytes;
                cleanedItems.Add(new CleanedItem(item, entry.Id, BackupId: null, Success: true, Error: null)
                {
                    WasVerified = true,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or QuarantineException)
            {
                OperationFailureKind failureKind = OperationFailureClassifier.Classify(ex);
                _logService.LogException(ex, "CleanEngine.QuarantineFile",
                    new Dictionary<string, object?>
                    {
                        ["Target"] = item.Path,
                        ["FailureKind"] = failureKind.ToString(),
                        ["RequiresElevation"] = failureKind == OperationFailureKind.RequiresElevation,
                    });
                cleanedItems.Add(new CleanedItem(item, entry?.Id, null, Success: false, Error: ex.Message)
                {
                    FailureKind = failureKind,
                    WasVerified = false,
                });
            }

            progress?.Report(new CleanProgress(index + 1, request.Items.Count, item.Path));
        }

        if (recycleBinItems.Count > 0)
        {
            if (!request.AllowIrreversibleRecycleBin)
            {
                const string denied = "回收站清空不可回滚，必须由用户明确确认。";
                cleanedItems.AddRange(recycleBinItems.Select(item => new CleanedItem(item, null, null, false, denied)));
            }
            else if (_recycleBinService is null)
            {
                const string unavailable = "Windows 回收站服务不可用。";
                cleanedItems.AddRange(recycleBinItems.Select(item => new CleanedItem(item, null, null, false, unavailable)));
            }
            else
            {
                try
                {
                    RecycleBinEmptyResult result = await _recycleBinService.EmptyAsync(cancellationToken).ConfigureAwait(false);
                    freedBytes += result.Before.ReclaimableSizeBytes;
                    cleanedItems.AddRange(recycleBinItems.Select(item => new CleanedItem(item, null, null, true, null)));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    _logService.LogException(ex, "CleanEngine.EmptyRecycleBin");
                    cleanedItems.AddRange(recycleBinItems.Select(item => new CleanedItem(item, null, null, false, ex.Message)));
                }
            }
        }

        // 注册表项 → 备份后删除；删除返回后由 CleanEngine 再做一次独立回读。
        for (int index = 0; index < registryItems.Count; index++)
        {
            ScannedItem item = registryItems[index];
            cancellationToken.ThrowIfCancellationRequested();
            BackupRecord? backup = null;
            if (_registryEngine is null)
            {
                cleanedItems.Add(new CleanedItem(item, null, null, Success: false, Error: "注册表引擎不可用")
                {
                    FailureKind = OperationFailureKind.Unknown,
                });
                continue;
            }

            try
            {
                (string keyPath, string? valueName) = ResolveRegistryTarget(item);
                backup = valueName is null
                    ? await _backupService.BackupRegistryKeyAsync(keyPath, cancellationToken).ConfigureAwait(false)
                    : await _backupService.BackupRegistryValueAsync(keyPath, valueName, cancellationToken).ConfigureAwait(false);
                registryBackupIds.Add(backup.Id);
                await WriteManifestAsync(new OperationManifest(
                    operationId, DateTimeOffset.UtcNow, quarantineEntryIds, registryBackupIds), cancellationToken).ConfigureAwait(false);
                if (valueName is null)
                {
                    await _registryEngine.DeleteKeyAsync(keyPath, backup.Id, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _registryEngine.DeleteValueAsync(keyPath, valueName, backup.Id, cancellationToken).ConfigureAwait(false);
                }

                bool stillExists = valueName is null
                    ? await _registryEngine.KeyExistsAsync(keyPath, cancellationToken).ConfigureAwait(false)
                    : (await _registryEngine.EnumerateValuesAsync(keyPath, cancellationToken).ConfigureAwait(false))
                        // Windows registry value names are case-insensitive. A case-sensitive readback
                        // could report success while a differently-cased value still exists.
                        .Contains(valueName, StringComparer.OrdinalIgnoreCase);
                if (stillExists)
                {
                    throw new BackupException(
                        $"注册表清理复验失败，目标仍存在: {item.Path}", OperationFailureKind.VerificationFailed);
                }

                cleanedItems.Add(new CleanedItem(item, null, backup.Id, Success: true, Error: null)
                {
                    WasVerified = true,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or LeslieCleanerException or System.Security.SecurityException)
            {
                OperationFailureKind failureKind = OperationFailureClassifier.Classify(ex);
                _logService.LogException(ex, "CleanEngine.CleanRegistryItem",
                    new Dictionary<string, object?>
                    {
                        ["Target"] = item.Path,
                        ["FailureKind"] = failureKind.ToString(),
                        ["RequiresElevation"] = failureKind == OperationFailureKind.RequiresElevation,
                    });
                cleanedItems.Add(new CleanedItem(item, null, backup?.Id, Success: false, Error: ex.Message)
                {
                    FailureKind = failureKind,
                    WasVerified = false,
                });
            }

            progress?.Report(new CleanProgress(fileItems.Count + index + 1, request.Items.Count, item.Path));
        }

        await WriteManifestAsync(new OperationManifest(
            operationId, DateTimeOffset.UtcNow, quarantineEntryIds, registryBackupIds), cancellationToken).ConfigureAwait(false);

        _logService.LogEvent(LogEventKind.DeleteFinish, "清理完成", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["FreedBytes"] = freedBytes,
            ["Success"] = cleanedItems.Count(static i => i.Success),
            ["Failed"] = cleanedItems.Count(static i => !i.Success),
        });

        return new CleanResult { OperationId = operationId, Items = cleanedItems, FreedBytes = freedBytes, WasCancelled = false };
    }

    /// <inheritdoc />
    public async Task<bool> RollbackAsync(string operationId, CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RollbackCoreAsync(operationId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<bool> RollbackCoreAsync(string operationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ValidateOperationId(operationId);
        OperationManifest manifest = await ReadManifestAsync(operationId, cancellationToken).ConfigureAwait(false);
        bool allSucceeded = true;

        _logService.LogEvent(LogEventKind.RestoreStart, "清理回滚开始",
            new Dictionary<string, object?> { ["OperationId"] = operationId });

        foreach (Guid entryId in manifest.QuarantineEntryIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _quarantineService.RestoreAsync(entryId, RestoreConflictPolicy.Rename, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or QuarantineException)
            {
                _logService.LogException(ex, "CleanEngine.RollbackQuarantine",
                    new Dictionary<string, object?> { ["EntryId"] = entryId });
                allSucceeded = false;
            }
        }

        foreach (Guid backupId in manifest.RegistryBackupIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _backupService.RestoreAsync(backupId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupException)
            {
                _logService.LogException(ex, "CleanEngine.RollbackRegistry",
                    new Dictionary<string, object?> { ["BackupId"] = backupId });
                allSucceeded = false;
            }
        }

        if (allSucceeded && File.Exists(GetManifestPath(operationId)))
        {
            File.Delete(GetManifestPath(operationId));
        }

        _logService.LogEvent(LogEventKind.RestoreFinish, "清理回滚完成", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["AllSucceeded"] = allSucceeded,
        });

        return allSucceeded;
    }

    /// <summary>
    /// 拆分注册表目标编码："键路径|值名" → (键路径, 值名)；无分隔符 → (键路径, null)。
    /// </summary>
    /// <param name="encoded">编码后的目标路径。</param>
    public static (string KeyPath, string? ValueName) SplitRegistryTarget(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        int separatorIndex = encoded.LastIndexOf('|');
        return separatorIndex < 0
            ? (encoded, null)
            : (encoded[..separatorIndex], encoded[(separatorIndex + 1)..]);
    }

    /// <summary>优先使用无歧义的类型化注册表目标；仅为旧扫描结果保留分隔符兼容。</summary>
    public static (string KeyPath, string? ValueName) ResolveRegistryTarget(ScannedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.RegistryKeyPath is null)
        {
            return SplitRegistryTarget(item.Path);
        }

        if (string.IsNullOrWhiteSpace(item.RegistryKeyPath))
        {
            throw new BackupException("注册表目标缺少有效键路径。", OperationFailureKind.InvalidTarget);
        }

        return (item.RegistryKeyPath, item.RegistryValueName);
    }

    /// <summary>创建注册表目标的无歧义、大小写不敏感字典身份；NUL 不是合法注册表名称字符。</summary>
    public static string BuildRegistryTargetIdentity(ScannedItem item)
    {
        (string keyPath, string? valueName) = ResolveRegistryTarget(item);
        return BuildRegistryTargetIdentity(keyPath, valueName);
    }

    /// <summary>创建键/值二元组身份，区分键目标、默认值和普通值。</summary>
    public static string BuildRegistryTargetIdentity(string keyPath, string? valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        return valueName is null
            ? keyPath + "\0K"
            : keyPath + "\0V" + valueName;
    }

    private string GetManifestPath(string operationId) =>
        Path.Combine(_pathService.ProgramDataRoot, "Operations", operationId + ".json");

    private async Task WriteManifestAsync(OperationManifest manifest, CancellationToken cancellationToken)
    {
        string path = GetManifestPath(manifest.OperationId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
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

    private async Task<OperationManifest> ReadManifestAsync(string operationId, CancellationToken cancellationToken)
    {
        string path = GetManifestPath(operationId);
        if (!File.Exists(path))
        {
            throw new LeslieCleanerException($"清理操作清单不存在，无法回滚: {operationId}");
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        OperationManifest? manifest = await JsonSerializer.DeserializeAsync<OperationManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return manifest ?? throw new LeslieCleanerException($"清理操作清单损坏: {path}");
    }

    private static void ValidateOperationId(string operationId)
    {
        if (!OperationIdPattern.IsMatch(operationId))
        {
            throw new ArgumentException("操作标识只能包含字母、数字、下划线和连字符，且长度不超过 80。", nameof(operationId));
        }
    }

    private sealed record OperationManifest(
        string OperationId,
        DateTimeOffset CreatedUtc,
        List<Guid> QuarantineEntryIds,
        List<Guid> RegistryBackupIds);
}

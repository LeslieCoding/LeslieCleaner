using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 隔离区服务实现。文件删除默认移入 %ProgramData%\LeslieCleaner\Quarantine，
/// 每条目一个目录（payload + metadata.json），默认保留 30 天，支持完整还原。
/// 全部 IO 异步化并支持取消；跨卷移动自动降级为异步复制 + 删除。
/// </summary>
public sealed class QuarantineService : IQuarantineService
{
    /// <summary>保留天数配置键（默认值 30）。</summary>
    public const string RetentionDaysConfigKey = "quarantine:retentionDays";

    /// <summary>默认保留天数。</summary>
    public const int DefaultRetentionDays = 30;

    private const string PayloadFileName = "payload";
    private const string MetadataFileName = "metadata.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IPathService _pathService;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;

    /// <summary>以路径、配置与日志服务创建隔离区服务。</summary>
    /// <param name="pathService">路径服务。</param>
    /// <param name="configService">配置服务（读取保留天数）。</param>
    /// <param name="logService">日志服务。</param>
    public QuarantineService(IPathService pathService, IConfigService configService, ILogService logService)
    {
        _pathService = pathService;
        _configService = configService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<QuarantineEntry> QuarantineFileAsync(
        string filePath,
        string reason,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new QuarantineException($"待隔离文件不存在: {fullPath}", OperationFailureKind.NotFound);
        }

        _logService.LogEvent(LogEventKind.DeleteStart, "文件移入隔离区开始", new Dictionary<string, object?>
        {
            ["Path"] = fullPath,
            ["Reason"] = reason,
            ["OperationId"] = operationId,
        });

        var id = Guid.NewGuid();
        string entryDirectory = Path.Combine(
            _pathService.EnsureDirectory(_pathService.QuarantineDirectory), id.ToString("N"));
        Directory.CreateDirectory(entryDirectory);
        string payloadPath = Path.Combine(entryDirectory, PayloadFileName);
        string metadataPath = Path.Combine(entryDirectory, MetadataFileName);

        var fileInfo = new FileInfo(fullPath);
        int retentionDays = _configService.GetValue(RetentionDaysConfigKey, DefaultRetentionDays);
        DateTimeOffset quarantinedUtc = DateTimeOffset.UtcNow;

        var entry = new QuarantineEntry
        {
            Id = id,
            OriginalPath = fullPath,
            QuarantinedPath = payloadPath,
            MetadataPath = metadataPath,
            SizeBytes = fileInfo.Length,
            QuarantinedUtc = quarantinedUtc,
            ExpiresUtc = quarantinedUtc.AddDays(retentionDays),
            Reason = reason,
            OperationId = operationId,
        };

        try
        {
            await MoveFileAsync(fullPath, payloadPath, cancellationToken).ConfigureAwait(false);
            VerifyMove(fullPath, payloadPath, entry.SizeBytes, sourceMustBeAbsent: true);
            await WriteMetadataAsync(entry, cancellationToken).ConfigureAwait(false);
            QuarantineEntry persisted = await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            if (persisted.Id != entry.Id || persisted.SizeBytes != entry.SizeBytes ||
                !string.Equals(persisted.OriginalPath, entry.OriginalPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new QuarantineException("隔离元数据复验失败。", OperationFailureKind.VerificationFailed);
            }
        }
        catch (Exception exception)
        {
            bool rolledBack = TryRollbackFailedQuarantine(fullPath, payloadPath, metadataPath, entryDirectory);
            if (exception is OperationCanceledException && rolledBack)
            {
                throw;
            }

            OperationFailureKind failureKind = OperationFailureClassifier.Classify(exception);
            string recovery = rolledBack
                ? "源文件已安全保留。"
                : $"自动回退未完成，请从隔离载荷恢复: {payloadPath}";
            throw new QuarantineException($"文件隔离失败: {fullPath}。{recovery}", exception, failureKind);
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "文件移入隔离区完成", new Dictionary<string, object?>
        {
            ["Path"] = fullPath,
            ["EntryId"] = id,
            ["SizeBytes"] = entry.SizeBytes,
        });

        return entry;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantineEntry>> QuarantineFilesAsync(
        IReadOnlyList<string> filePaths,
        string reason,
        string? operationId = null,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var entries = new List<QuarantineEntry>(filePaths.Count);
        for (int i = 0; i < filePaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = filePaths[i];
            try
            {
                entries.Add(await QuarantineFileAsync(path, reason, operationId, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or QuarantineException)
            {
                // 单文件失败不中断整体：记录日志并继续，失败项由调用方核对数量差异
                _logService.LogException(ex, "QuarantineService.QuarantineFilesAsync",
                    new Dictionary<string, object?> { ["Path"] = path });
            }

            progress?.Report(new CleanProgress(i + 1, filePaths.Count, path));
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<string> RestoreAsync(
        Guid entryId,
        RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Rename,
        CancellationToken cancellationToken = default)
    {
        QuarantineEntry entry = await ReadEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (!File.Exists(entry.QuarantinedPath))
        {
            throw new QuarantineException($"隔离载荷缺失，无法还原: {entry.QuarantinedPath}");
        }

        _logService.LogEvent(LogEventKind.RestoreStart, "隔离区还原开始", new Dictionary<string, object?>
        {
            ["EntryId"] = entryId,
            ["OriginalPath"] = entry.OriginalPath,
        });

        string destination = ResolveDestination(entry.OriginalPath, conflictPolicy);
        string? parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await MoveFileAsync(entry.QuarantinedPath, destination, cancellationToken).ConfigureAwait(false);
        VerifyMove(entry.QuarantinedPath, destination, entry.SizeBytes, sourceMustBeAbsent: true);

        // 还原成功且复验通过后移除空条目目录（含 metadata.json）
        string entryDirectory = Path.GetDirectoryName(entry.MetadataPath)!;
        if (Directory.Exists(entryDirectory))
        {
            Directory.Delete(entryDirectory, recursive: true);
        }

        _logService.LogEvent(LogEventKind.RestoreFinish, "隔离区还原完成", new Dictionary<string, object?>
        {
            ["EntryId"] = entryId,
            ["RestoredTo"] = destination,
        });

        return destination;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantineEntry>> ListEntriesAsync(CancellationToken cancellationToken = default)
    {
        string root = _pathService.QuarantineDirectory;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var entries = new List<QuarantineEntry>();
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
                entries.Add(await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "QuarantineService.ListEntriesAsync",
                    new Dictionary<string, object?> { ["MetadataPath"] = metadataPath });
            }
        }

        entries.Sort(static (a, b) => b.QuarantinedUtc.CompareTo(a.QuarantinedUtc));
        return entries;
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<QuarantineEntry> entries = await ListEntriesAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int purged = 0;

        foreach (QuarantineEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.ExpiresUtc > now)
            {
                continue;
            }

            string entryDirectory = Path.GetDirectoryName(entry.MetadataPath)!;
            try
            {
                if (Directory.Exists(entryDirectory))
                {
                    Directory.Delete(entryDirectory, recursive: true);
                }

                purged++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "QuarantineService.PurgeExpiredAsync",
                    new Dictionary<string, object?> { ["EntryId"] = entry.Id });
            }
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "隔离区过期条目清理完成",
            new Dictionary<string, object?> { ["PurgedCount"] = purged });
        return purged;
    }

    private async Task<QuarantineEntry> ReadEntryAsync(Guid entryId, CancellationToken cancellationToken)
    {
        string metadataPath = Path.Combine(_pathService.QuarantineDirectory, entryId.ToString("N"), MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            throw new QuarantineException($"隔离条目不存在: {entryId}");
        }

        return await ReadMetadataAsync(metadataPath, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveDestination(string originalPath, RestoreConflictPolicy policy)
    {
        if (!File.Exists(originalPath))
        {
            return originalPath;
        }

        switch (policy)
        {
            case RestoreConflictPolicy.Fail:
                throw new QuarantineException($"还原目标已存在: {originalPath}");
            case RestoreConflictPolicy.Overwrite:
                File.Delete(originalPath);
                return originalPath;
            default:
                string directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
                string stem = Path.GetFileNameWithoutExtension(originalPath);
                string extension = Path.GetExtension(originalPath);
                for (int i = 1; ; i++)
                {
                    string candidate = Path.Combine(directory, $"{stem} (restored {i}){extension}");
                    if (!File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
        }
    }

    private static async Task MoveFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
        {
            File.Move(source, destination);
            return;
        }

        // 跨卷：先写独立 partial 文件，完整复验后再原子发布载荷，最后删除源文件。
        const int bufferSize = 81920;
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream output = new(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(source).Length != new FileInfo(partial).Length)
            {
                throw new QuarantineException("跨卷隔离复制长度复验失败。", OperationFailureKind.VerificationFailed);
            }

            File.Move(partial, destination);
            File.Delete(source);
        }
        finally
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
    }

    private static async Task WriteMetadataAsync(QuarantineEntry entry, CancellationToken cancellationToken)
    {
        string temporary = entry.MetadataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, entry.MetadataPath);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void VerifyMove(string source, string destination, long expectedLength, bool sourceMustBeAbsent)
    {
        if ((sourceMustBeAbsent && File.Exists(source)) || !File.Exists(destination) ||
            new FileInfo(destination).Length != expectedLength)
        {
            throw new QuarantineException(
                $"文件移动复验失败: {source} -> {destination}", OperationFailureKind.VerificationFailed);
        }
    }

    private static bool TryRollbackFailedQuarantine(
        string source, string payload, string metadata, string entryDirectory)
    {
        try
        {
            if (File.Exists(payload))
            {
                if (!File.Exists(source))
                {
                    string? parent = Path.GetDirectoryName(source);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    File.Move(payload, source);
                }
                else
                {
                    File.Delete(payload);
                }
            }

            if (File.Exists(metadata))
            {
                File.Delete(metadata);
            }

            if (Directory.Exists(entryDirectory) && !Directory.EnumerateFileSystemEntries(entryDirectory).Any())
            {
                Directory.Delete(entryDirectory);
            }

            return File.Exists(source) && !File.Exists(payload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
    private static async Task<QuarantineEntry> ReadMetadataAsync(string metadataPath, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        QuarantineEntry? entry = await JsonSerializer.DeserializeAsync<QuarantineEntry>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return entry ?? throw new QuarantineException($"隔离元数据损坏: {metadataPath}");
    }
}

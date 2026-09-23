using System.Security.Cryptography;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Core.Threading;

namespace LeslieCleaner.Modules.Cleaner.Duplicates;

/// <summary>
/// 重复文件查找引擎。三层递进：大小分组 → 部分哈希（头尾各 64KB + 大小）→
/// 全量 SHA256（异步流式 + 取消），避免对全库逐文件全量哈希。
/// 视频/音频按扩展名分层后在层内走同样的三层；相似图片走感知哈希（汉明距离 ≤ 5）。
/// 批量删除经 <see cref="IQuarantineService"/> 进隔离区（可回滚）。
/// </summary>
public sealed class DuplicateFinderEngine
{
    private const int PartialHashBytes = 64 * 1024;

    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".ts"];
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".wav", ".aac", ".ogg", ".wma", ".m4a"];

    private readonly IQuarantineService _quarantineService;
    private readonly ILogService _logService;
    private readonly PauseTokenSource _pauseTokenSource = new();

    private CancellationTokenSource? _activeScan;

    /// <summary>以隔离区与日志服务创建引擎。</summary>
    /// <param name="quarantineService">隔离区服务。</param>
    /// <param name="logService">日志服务。</param>
    public DuplicateFinderEngine(IQuarantineService quarantineService, ILogService logService)
    {
        _quarantineService = quarantineService;
        _logService = logService;
    }

    /// <summary>当前是否暂停。</summary>
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <summary>暂停扫描。</summary>
    public void Pause() => _pauseTokenSource.Pause();

    /// <summary>继续扫描。</summary>
    public void Resume() => _pauseTokenSource.Resume();

    /// <summary>取消扫描。</summary>
    public void Cancel() => _activeScan?.Cancel();

    /// <summary>
    /// 三层递进查找重复文件（含视频/音频分层）。
    /// </summary>
    /// <param name="roots">扫描根目录。</param>
    /// <param name="minSizeBytes">参与比较的最小文件大小（默认 1KB，过滤噪声）。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        IReadOnlyList<string> roots,
        long minSizeBytes = 1024,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeScan = linkedCts;
        try
        {
            _logService.LogEvent(LogEventKind.ScanStart, "重复文件扫描开始",
                new Dictionary<string, object?> { ["Roots"] = roots.Count });

            // 第一层：大小分组
            var sizeGroups = new Dictionary<long, List<string>>();
            long scanned = 0;
            foreach (string file in EnumerateFiles(roots, linkedCts.Token))
            {
                await _pauseTokenSource.Token.WaitWhilePausedAsync(linkedCts.Token).ConfigureAwait(false);
                FileInfo info = new(file);
                if (info.Length < minSizeBytes)
                {
                    continue;
                }

                if (!sizeGroups.TryGetValue(info.Length, out List<string>? group))
                {
                    group = [];
                    sizeGroups[info.Length] = group;
                }

                group.Add(file);
                scanned++;
                if ((scanned & 0x3FF) == 0)
                {
                    progress?.Report(new ScanProgress("大小分组", scanned, null, file));
                }
            }

            var candidates = sizeGroups.Values.Where(static g => g.Count > 1).ToList();
            progress?.Report(new ScanProgress("部分哈希", 0, candidates.Count, null));

            // 第二层：部分哈希（头尾 64KB）
            var partialGroups = new List<(string Key, List<string> Files)>();
            int stage = 0;
            foreach (List<string> group in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var byPartial = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (string file in group)
                {
                    string? partial = await TryComputePartialHashAsync(file, linkedCts.Token).ConfigureAwait(false);
                    if (partial is null)
                    {
                        continue;
                    }

                    MediaKind kind = ClassifyMedia(file);
                    string key = $"{kind}:{partial}";
                    if (!byPartial.TryGetValue(key, out List<string>? list))
                    {
                        list = [];
                        byPartial[key] = list;
                    }

                    list.Add(file);
                }

                partialGroups.AddRange(byPartial.Where(static p => p.Value.Count > 1)
                    .Select(static p => (p.Key, p.Value)));
                stage++;
                progress?.Report(new ScanProgress("部分哈希", stage, candidates.Count, null));
            }

            // 第三层：全量哈希
            var results = new List<DuplicateGroup>();
            stage = 0;
            foreach ((string _, List<string> files) in partialGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var byFull = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (string file in files)
                {
                    await _pauseTokenSource.Token.WaitWhilePausedAsync(linkedCts.Token).ConfigureAwait(false);
                    string? full = await TryComputeFullHashAsync(file, linkedCts.Token).ConfigureAwait(false);
                    if (full is null)
                    {
                        continue;
                    }

                    if (!byFull.TryGetValue(full, out List<string>? list))
                    {
                        list = [];
                        byFull[full] = list;
                    }

                    list.Add(file);
                }

                foreach (KeyValuePair<string, List<string>> pair in byFull.Where(static p => p.Value.Count > 1))
                {
                    results.Add(new DuplicateGroup(pair.Key, pair.Value, new FileInfo(pair.Value[0]).Length));
                }

                stage++;
                progress?.Report(new ScanProgress("全量哈希", stage, partialGroups.Count, null));
            }

            _logService.LogEvent(LogEventKind.ScanFinish, "重复文件扫描完成", new Dictionary<string, object?>
            {
                ["Groups"] = results.Count,
                ["WastedBytes"] = results.Sum(static g => g.WastedBytes),
            });
            return results;
        }
        finally
        {
            _activeScan = null;
        }
    }

    /// <summary>
    /// 查找相似图片（aHash + dHash 任一距离 ≤ 5 即相似）。
    /// </summary>
    /// <param name="roots">扫描根目录。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<SimilarImageGroup>> FindSimilarImagesAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var hashes = new List<(string Path, ulong AHash, ulong DHash)>();
        long processed = 0;
        foreach (string file in EnumerateFiles(roots, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PerceptualHash.IsSupportedImage(file))
            {
                continue;
            }

            try
            {
                ulong aHash = await Task.Run(() => PerceptualHash.ComputeAverageHash(file), cancellationToken).ConfigureAwait(false);
                ulong dHash = await Task.Run(() => PerceptualHash.ComputeDifferenceHash(file), cancellationToken).ConfigureAwait(false);
                hashes.Add((file, aHash, dHash));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
            {
                _logService.LogException(ex, "DuplicateFinderEngine.PerceptualHash",
                    new Dictionary<string, object?> { ["File"] = file });
            }

            processed++;
            if ((processed & 0x3F) == 0)
            {
                progress?.Report(new ScanProgress("感知哈希", processed, null, file));
            }
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<SimilarImageGroup>();
        for (int i = 0; i < hashes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (used.Contains(hashes[i].Path))
            {
                continue;
            }

            var similar = new List<(string Path, int Distance)>();
            for (int j = i + 1; j < hashes.Count; j++)
            {
                if (used.Contains(hashes[j].Path))
                {
                    continue;
                }

                int distance = Math.Min(
                    PerceptualHash.HammingDistance(hashes[i].AHash, hashes[j].AHash),
                    PerceptualHash.HammingDistance(hashes[i].DHash, hashes[j].DHash));
                if (distance <= PerceptualHash.SimilarityThreshold)
                {
                    similar.Add((hashes[j].Path, distance));
                    used.Add(hashes[j].Path);
                }
            }

            if (similar.Count > 0)
            {
                used.Add(hashes[i].Path);
                groups.Add(new SimilarImageGroup(hashes[i].Path, similar));
            }
        }

        return groups;
    }

    /// <summary>
    /// 按保留策略批量删除冗余文件（进隔离区，可回滚）。
    /// </summary>
    /// <param name="groups">重复组。</param>
    /// <param name="policy">保留策略。</param>
    /// <param name="preferredDirectory"><see cref="KeepPolicy.PreferredDirectory"/> 的优先目录。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理结果（含回滚凭据 OperationId）。</returns>
    public async Task<string> DeleteRedundantAsync(
        IReadOnlyList<DuplicateGroup> groups,
        KeepPolicy policy,
        string? preferredDirectory = null,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);
        string operationId = Guid.NewGuid().ToString("N");
        var redundant = new List<string>();
        foreach (DuplicateGroup group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (_, IReadOnlyList<string> files) = DuplicateRetention.Select(group.Files, policy, preferredDirectory);
            redundant.AddRange(files);
        }

        if (redundant.Count > 0)
        {
            await _quarantineService.QuarantineFilesAsync(redundant, "重复文件清理", operationId, progress, cancellationToken).ConfigureAwait(false);
        }

        return operationId;
    }

    /// <summary>媒体分层判定。</summary>
    /// <param name="path">文件路径。</param>
    public static MediaKind ClassifyMedia(string path)
    {
        string extension = Path.GetExtension(path);
        if (VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return MediaKind.Video;
        }

        return AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? MediaKind.Audio : MediaKind.None;
    }

    private IEnumerable<string> EnumerateFiles(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", options);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "DuplicateFinderEngine.EnumerateFiles",
                    new Dictionary<string, object?> { ["Root"] = root });
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }
        }
    }

    private async Task<string?> TryComputePartialHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                PartialHashBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            byte[] buffer = new byte[PartialHashBytes];

            int headRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            sha.TransformBlock(buffer, 0, headRead, null, 0);

            long length = stream.Length;
            if (length > PartialHashBytes)
            {
                stream.Seek(-PartialHashBytes, SeekOrigin.End);
                int tailRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                sha.TransformBlock(buffer, 0, tailRead, null, 0);
            }

            byte[] sizeBytes = BitConverter.GetBytes(length);
            sha.TransformFinalBlock(sizeBytes, 0, sizeBytes.Length);
            return Convert.ToHexString(sha.Hash!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "DuplicateFinderEngine.PartialHash",
                new Dictionary<string, object?> { ["Path"] = path });
            return null;
        }
    }

    private async Task<string?> TryComputeFullHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "DuplicateFinderEngine.FullHash",
                new Dictionary<string, object?> { ["Path"] = path });
            return null;
        }
    }
}

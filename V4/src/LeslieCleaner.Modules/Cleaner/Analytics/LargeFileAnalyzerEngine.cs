using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Core.Threading;

namespace LeslieCleaner.Modules.Cleaner.Analytics;

/// <summary>
/// 大文件分析引擎。输出 Top N 大文件、文件类型统计、Treemap 数据模型与
/// 规则引擎清理建议（禁止伪 AI：每条建议引用真实依据——位置/大小/未访问天数）。
/// 支持暂停/取消，禁区规则与索引引擎一致。
/// </summary>
public sealed class LargeFileAnalyzerEngine
{
    /// <summary>Top 大文件默认数量。</summary>
    public const int DefaultTopCount = 100;

    /// <summary>"长期未访问"阈值天数。</summary>
    public const int StaleDaysThreshold = 180;

    /// <summary>"超大文件"阈值（4 GiB）。</summary>
    public const long HugeFileBytes = 4L * 1024 * 1024 * 1024;

    private readonly ILogService _logService;
    private readonly PauseTokenSource _pauseTokenSource = new();
    private readonly Func<string, bool> _forbiddenProbe;

    private CancellationTokenSource? _activeScan;

    /// <summary>以日志服务创建引擎。</summary>
    /// <param name="logService">日志服务。</param>
    /// <param name="forbiddenProbe">禁区判定（测试注入）；为 <see langword="null"/> 时跳过禁区检查（由调用方保证范围）。</param>
    public LargeFileAnalyzerEngine(ILogService logService, Func<string, bool>? forbiddenProbe = null)
    {
        _logService = logService;
        _forbiddenProbe = forbiddenProbe ?? (static _ => false);
    }

    /// <summary>当前是否暂停。</summary>
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <summary>暂停。</summary>
    public void Pause() => _pauseTokenSource.Pause();

    /// <summary>继续。</summary>
    public void Resume() => _pauseTokenSource.Resume();

    /// <summary>取消。</summary>
    public void Cancel() => _activeScan?.Cancel();

    /// <summary>
    /// 执行分析。
    /// </summary>
    /// <param name="roots">扫描根目录。</param>
    /// <param name="topCount">Top 大文件数量。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<LargeFileAnalysisResult> AnalyzeAsync(
        IReadOnlyList<string> roots,
        int topCount = DefaultTopCount,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeScan = linkedCts;
        bool wasCancelled = false;
        try
        {
            _logService.LogEvent(LogEventKind.ScanStart, "大文件分析开始",
                new Dictionary<string, object?> { ["Roots"] = roots.Count });

            var topFiles = new SortedList<long, Queue<LargeFileEntry>>(topCount + 1);
            var typeStats = new Dictionary<string, (long Count, long Bytes)>(StringComparer.OrdinalIgnoreCase);
            var rootNodes = new List<DirectoryNode>();
            long scanned = 0;
            long totalBytes = 0;

            foreach (string root in roots)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var rootNode = new DirectoryNode(Path.GetFullPath(root));
                rootNodes.Add(rootNode);
                await ScanDirectoryAsync(
                    root, rootNode, topFiles, topCount, typeStats, progress,
                    () => Interlocked.Read(ref scanned), linkedCts.Token).ConfigureAwait(false);
                scanned += rootNode.FileCount;
                totalBytes += rootNode.TotalBytes;
            }

            IReadOnlyList<LargeFileEntry> top = [.. topFiles
                .OrderByDescending(static p => p.Key)
                .SelectMany(static p => p.Value)
                .Take(topCount)];
            IReadOnlyList<FileTypeStat> stats = [.. typeStats
                .Select(static p => new FileTypeStat(p.Key, p.Value.Count, p.Value.Bytes))
                .OrderByDescending(static s => s.TotalBytes)];
            IReadOnlyList<TreemapNode> treemap = [.. rootNodes.Select(static n => n.ToTreemapNode())];
            IReadOnlyList<string> suggestions = BuildSuggestions(top, DateTimeOffset.UtcNow);

            _logService.LogEvent(LogEventKind.ScanFinish, "大文件分析完成", new Dictionary<string, object?>
            {
                ["ScannedFiles"] = scanned,
                ["TotalBytes"] = totalBytes,
            });

            return new LargeFileAnalysisResult(top, stats, treemap, suggestions, scanned, totalBytes, WasCancelled: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
            return new LargeFileAnalysisResult([], [], [], [], 0, 0, WasCancelled: true);
        }
        finally
        {
            _activeScan = null;
            if (wasCancelled)
            {
                _logService.LogEvent(LogEventKind.ScanFinish, "大文件分析已取消");
            }
        }
    }

    /// <summary>
    /// 规则引擎建议（纯函数）。
    /// </summary>
    /// <param name="topFiles">Top 大文件。</param>
    /// <param name="nowUtc">当前时间。</param>
    public static IReadOnlyList<string> BuildSuggestions(IReadOnlyList<LargeFileEntry> topFiles, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(topFiles);
        var suggestions = new List<string>();
        string downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        foreach (LargeFileEntry file in topFiles)
        {
            double staleDays = (nowUtc - file.LastWriteUtc).TotalDays;
            if (file.Path.StartsWith(downloadsDirectory, StringComparison.OrdinalIgnoreCase) &&
                staleDays > StaleDaysThreshold)
            {
                suggestions.Add(
                    $"\"{file.Path}\" 位于 Downloads 且 {staleDays:F0} 天未修改（{FormatSize(file.SizeBytes)}），建议清理或归档。");
            }
            else if (file.SizeBytes >= HugeFileBytes)
            {
                suggestions.Add(
                    $"\"{file.Path}\" 大小 {FormatSize(file.SizeBytes)} 超过 4GiB，建议确认是否仍需保留。");
            }
        }

        return suggestions;
    }

    private async Task ScanDirectoryAsync(
        string directory,
        DirectoryNode node,
        SortedList<long, Queue<LargeFileEntry>> topFiles,
        int topCount,
        Dictionary<string, (long Count, long Bytes)> typeStats,
        IProgress<ScanProgress>? progress,
        Func<long> scannedSoFar,
        CancellationToken cancellationToken)
    {
        string[] files;
        string[] subDirectories;
        try
        {
            files = Directory.GetFiles(directory);
            subDirectories = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "LargeFileAnalyzerEngine.ScanDirectory",
                new Dictionary<string, object?> { ["Directory"] = directory });
            return;
        }

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _pauseTokenSource.Token.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            if (_forbiddenProbe(file))
            {
                continue;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "LargeFileAnalyzerEngine.FileInfo",
                    new Dictionary<string, object?> { ["File"] = file });
                continue;
            }

            node.FileCount++;
            node.OwnBytes += info.Length;
            node.TotalBytes += info.Length;
            string extension = Path.GetExtension(info.Name);
            if (string.IsNullOrEmpty(extension))
            {
                extension = "(none)";
            }

            typeStats.TryGetValue(extension, out (long Count, long Bytes) stat);
            typeStats[extension] = (stat.Count + 1, stat.Bytes + info.Length);

            AddTopFile(topFiles, topCount, new LargeFileEntry(
                info.FullName, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)));

            if ((node.FileCount & 0x3FF) == 0)
            {
                progress?.Report(new ScanProgress("分析文件", scannedSoFar() + node.FileCount, null, file));
            }
        }

        foreach (string subDirectory in subDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_forbiddenProbe(subDirectory))
            {
                continue;
            }

            try
            {
                if ((File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "LargeFileAnalyzerEngine.GetAttributes",
                    new Dictionary<string, object?> { ["Directory"] = subDirectory });
                continue;
            }

            var child = new DirectoryNode(subDirectory);
            node.Children.Add(child);
            await ScanDirectoryAsync(subDirectory, child, topFiles, topCount, typeStats, progress, scannedSoFar, cancellationToken).ConfigureAwait(false);
            node.FileCount += child.FileCount;
            node.TotalBytes += child.TotalBytes;
        }
    }

    private static void AddTopFile(
        SortedList<long, Queue<LargeFileEntry>> topFiles, int topCount, LargeFileEntry entry)
    {
        if (!topFiles.TryGetValue(entry.SizeBytes, out Queue<LargeFileEntry>? bucket))
        {
            bucket = new Queue<LargeFileEntry>();
            topFiles[entry.SizeBytes] = bucket;
        }

        bucket.Enqueue(entry);

        int total = topFiles.Values.Sum(static q => q.Count);
        while (total > topCount)
        {
            Queue<LargeFileEntry> smallest = topFiles.Values[0];
            smallest.Dequeue();
            if (smallest.Count == 0)
            {
                topFiles.RemoveAt(0);
            }

            total--;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F1} GiB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MiB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F1} KiB",
        _ => $"{bytes} B",
    };

    private sealed class DirectoryNode(string path)
    {
        public string Path { get; } = path;

        public List<DirectoryNode> Children { get; } = [];

        public long FileCount;

        public long OwnBytes;

        public long TotalBytes;

        public TreemapNode ToTreemapNode()
        {
            var children = new List<TreemapNode>();
            if (OwnBytes > 0)
            {
                // 本层直接文件作为合成叶子，保证各层面积可加总
                children.Add(new TreemapNode("(本层文件)", Path, OwnBytes, []));
            }

            children.AddRange(Children.Select(static c => c.ToTreemapNode()));
            return new TreemapNode(
                System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : Path,
                Path,
                TotalBytes,
                [.. children.OrderByDescending(static c => c.SizeBytes)]);
        }
    }
}

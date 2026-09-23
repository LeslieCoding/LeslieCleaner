using System.Threading.Channels;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Core.Threading;
using LeslieCleaner.Data;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Infrastructure.Indexing;

/// <summary>
/// 文件索引引擎（卸载残留 Stage 4 的基础设施）。
/// 特性：增量索引（modified 时间戳比对）、FileSystemWatcher 变更监控、
/// NTFS USN/MFT 加速（无权限自动降级普通枚举）、CPU*2 并行目录爬取（Channel 背压）、
/// Pause/Resume/Cancel、MMF 外存路径去重（陈旧记录差异归并）。
/// </summary>
public sealed class FileIndexEngine : IFileIndexEngine, IDisposable
{
    /// <summary>USN 加速开关配置键（默认 true，热更新生效）。</summary>
    public const string PreferUsnConfigKey = "fileindex:preferUsn";

    /// <summary>单目录枚举超时（防止特殊目录/云同步目录 IO 阻塞拖死整体）。</summary>
    public static readonly TimeSpan DirectoryEnumerationTimeout = TimeSpan.FromSeconds(30);

    private const int BatchSize = 2000;
    private const int RecordChannelCapacity = 8192;
    private const int DirectoryChannelCapacity = 1024;
    private static readonly TimeSpan WatcherDebounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    // NTFS 云占位/脱机属性（不触发云端召回，直接跳过）
    private const FileAttributes RecallOnDataAccessAttribute = (FileAttributes)0x00040000;
    private const FileAttributes OfflineAttribute = (FileAttributes)0x00001000;

    private readonly IDatabase _database;
    private readonly FileIndexRepository _repository;
    private readonly ILogService _logService;
    private readonly IConfigService? _configService;
    private readonly PauseTokenSource _pauseTokenSource = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _watchersGate = new();
    private readonly Timer _watchDebounceTimer;
    private readonly HashSet<string> _pendingWatcherPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingGate = new();

    private CancellationTokenSource? _activeScan;
    private int _disposed;

    /// <summary>索引被文件系统监控增量更新后触发。</summary>
    public event EventHandler? IndexChanged;

    /// <summary>以数据库与日志服务创建引擎。</summary>
    /// <param name="database">已注册的数据库（初始化由引擎幂等触发）。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="configService">配置服务（读取 USN 开关），可为 <see langword="null"/>。</param>
    /// <param name="directoryEnumerationTimeout">单目录枚举超时覆盖（测试注入）；为 <see langword="null"/> 时用默认值。</param>
    public FileIndexEngine(IDatabase database, ILogService logService, IConfigService? configService = null,
        TimeSpan? directoryEnumerationTimeout = null)
    {
        _database = database;
        _repository = new FileIndexRepository(database);
        _logService = logService;
        _configService = configService;
        _directoryEnumerationTimeout = directoryEnumerationTimeout ?? DirectoryEnumerationTimeout;
        _watchDebounceTimer = new Timer(OnWatcherDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private readonly TimeSpan _directoryEnumerationTimeout;

    /// <inheritdoc />
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <inheritdoc />
    public void Pause() => _pauseTokenSource.Pause();

    /// <inheritdoc />
    public void Resume() => _pauseTokenSource.Resume();

    /// <inheritdoc />
    public void Cancel() => _activeScan?.Cancel();

    /// <inheritdoc />
    public async Task<long> IndexAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _database.InitializeAsync(cancellationToken).ConfigureAwait(false);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeScan = linkedCts;
        try
        {
            _logService.LogEvent(LogEventKind.ScanStart, "文件索引扫描开始",
                new Dictionary<string, object?> { ["Roots"] = roots.Count });

            Dictionary<string, long> snapshot = await LoadSnapshotAsync(roots, linkedCts.Token).ConfigureAwait(false);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recordChannel = Channel.CreateBounded<FileIndexRecord>(
                new BoundedChannelOptions(RecordChannelCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

            Task writer = ConsumeRecordsAsync(recordChannel.Reader, linkedCts.Token);
            long scanned = 0;
            long changed = 0;
            long timedOutDirs = 0;
            var scanState = new ScanState(recordChannel.Writer, seenPaths, snapshot, progress);
            using var heartbeat = new Timer(
                _ => ReportHeartbeat(scanState), null, HeartbeatInterval, HeartbeatInterval);

            bool usedUsn = await TryIndexWithUsnAsync(roots, scanState, linkedCts.Token).ConfigureAwait(false);
            if (!usedUsn)
            {
                await IndexWithEnumerationAsync(roots, scanState, linkedCts.Token).ConfigureAwait(false);
            }

            scanned = Interlocked.Read(ref scanState.Scanned);
            changed = Interlocked.Read(ref scanState.Changed);
            timedOutDirs = Interlocked.Read(ref scanState.TimedOutDirectories);

            recordChannel.Writer.Complete();
            await writer.ConfigureAwait(false);

            long removed = RemoveStaleRecords(roots, snapshot, seenPaths, linkedCts.Token);

            _logService.LogEvent(LogEventKind.ScanFinish, "文件索引扫描完成", new Dictionary<string, object?>
            {
                ["Scanned"] = scanned,
                ["Changed"] = changed,
                ["Removed"] = removed,
                ["TimedOutDirectories"] = timedOutDirs,
                ["UsedUsn"] = usedUsn,
            });

            return changed;
        }
        finally
        {
            _activeScan = null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileIndexRecord>> QueryByPublisherAsync(string publisher, CancellationToken cancellationToken = default) =>
        _repository.QueryByPublisherAsync(publisher, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FileIndexRecord>> QueryByProductAsync(string productName, CancellationToken cancellationToken = default) =>
        _repository.QueryByProductAsync(productName, cancellationToken);

    /// <summary>
    /// 启动文件系统监控：允许根下的创建/修改/删除/重命名事件经 2s 防抖后触发增量更新。
    /// </summary>
    /// <param name="roots">监控根目录集合。</param>
    public void StartWatching(IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        StopWatching();

        lock (_watchersGate)
        {
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };
                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemEvent;
                _watchers.Add(watcher);
            }
        }

        _logService.LogEvent(LogEventKind.UserAction, "文件索引监控已启动",
            new Dictionary<string, object?> { ["Roots"] = roots.Count });
    }

    /// <summary>停止文件系统监控。</summary>
    public void StopWatching()
    {
        lock (_watchersGate)
        {
            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }

    /// <summary>释放监控与计时器资源。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        StopWatching();
        _watchDebounceTimer.Dispose();
    }

    private async Task<bool> TryIndexWithUsnAsync(IReadOnlyList<string> roots, ScanState state, CancellationToken cancellationToken)
    {
        bool preferUsn = _configService?.GetValue(PreferUsnConfigKey, true) ?? true;
        if (!preferUsn || !OperatingSystem.IsWindows())
        {
            return false;
        }

        var byVolume = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            string? volumeRoot = Path.GetPathRoot(Path.GetFullPath(root));
            if (string.IsNullOrEmpty(volumeRoot))
            {
                return false;
            }

            if (!byVolume.TryGetValue(volumeRoot, out List<string>? list))
            {
                list = [];
                byVolume[volumeRoot] = list;
            }

            list.Add(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        }

        if (byVolume.Count != 1)
        {
            // 跨卷根集合：回退普通枚举（保持实现简单可靠）
            return false;
        }

        KeyValuePair<string, List<string>> volume = byVolume.First();
        UsnMftEnumerator? enumerator = null;
        try
        {
            enumerator = UsnMftEnumerator.TryCreate(volume.Key);
            if (enumerator is null)
            {
                return false;
            }

            await Task.Run(() =>
            {
                foreach (UsnMftEnumerator.UsnFileEntry entry in enumerator.EnumerateAll(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.IsDirectory)
                    {
                        continue;
                    }

                    if (!IsUnderAnyRoot(entry.Path, volume.Value) || ScanScope.IsForbidden(entry.Path))
                    {
                        continue;
                    }

                    ProcessCandidateAsync(state, entry.Path, entry.Name, sizeBytes: 0, modifiedUtcTicks: entry.LastChangeUtcTicks,
                        hasReliableSize: false, cancellationToken).GetAwaiter().GetResult();
                }
            }, cancellationToken).ConfigureAwait(false);

            _logService.LogEvent(LogEventKind.PerformanceMetrics, "USN/MFT 加速索引已启用",
                new Dictionary<string, object?> { ["Volume"] = volume.Key });
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // USN 路径任何失败：记日志并优雅降级为普通枚举
            _logService.LogException(ex, "FileIndexEngine.TryIndexWithUsnAsync");
            return false;
        }
        finally
        {
            enumerator?.Dispose();
        }
    }

    private async Task IndexWithEnumerationAsync(IReadOnlyList<string> roots, ScanState state, CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(DirectoryChannelCapacity) { SingleReader = false, FullMode = BoundedChannelFullMode.Wait });
        int pendingDirectories = 0;
        int workerCount = Math.Max(2, Environment.ProcessorCount * 2);

        foreach (string root in roots)
        {
            if (Directory.Exists(root) && !ScanScope.IsForbiddenDirectory(root))
            {
                Interlocked.Increment(ref pendingDirectories);
                await directoryChannel.Writer.WriteAsync(root, cancellationToken).ConfigureAwait(false);
            }
        }

        if (Volatile.Read(ref pendingDirectories) == 0)
        {
            directoryChannel.Writer.Complete();
            return;
        }

        var workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Run(async () =>
            {
                await foreach (string directory in directoryChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        await _pauseTokenSource.Token.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
                        await EnumerateDirectoryAsync(directory, state, directoryChannel.Writer, pendingDirectories, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logService.LogException(ex, "FileIndexEngine.EnumerateDirectory",
                            new Dictionary<string, object?> { ["Directory"] = directory });
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pendingDirectories) == 0)
                        {
                            directoryChannel.Writer.TryComplete();
                        }
                    }
                }
            }, cancellationToken);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task EnumerateDirectoryAsync(
        string directory,
        ScanState state,
        ChannelWriter<string> directoryWriter,
        int pendingDirectories,
        CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref state.CurrentDirectory, directory);
        (string[] Files, string[] SubDirectories)? entries = EnumerateDirectoryEntries(directory, state, cancellationToken);
        if (entries is null)
        {
            return; // 超时或失败：已记录日志并跳过（不影响整体完成）
        }

        foreach (string file in entries.Value.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ScanScope.IsForbidden(file))
            {
                continue;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(file);
                // 重解析点与云占位/脱机文件跳过：不触发云端召回，不做内容级访问
                if ((info.Attributes & (FileAttributes.ReparsePoint | RecallOnDataAccessAttribute | OfflineAttribute)) != 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "FileIndexEngine.FileInfo",
                    new Dictionary<string, object?> { ["File"] = file });
                continue;
            }

            await ProcessCandidateAsync(state, file, info.Name, info.Length, info.LastWriteTimeUtc.Ticks,
                hasReliableSize: true, cancellationToken).ConfigureAwait(false);
        }

        foreach (string subDirectory in entries.Value.SubDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ScanScope.IsForbiddenDirectory(subDirectory))
            {
                continue;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(subDirectory);
                if ((attributes & (FileAttributes.ReparsePoint | RecallOnDataAccessAttribute | OfflineAttribute)) != 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "FileIndexEngine.GetAttributes",
                    new Dictionary<string, object?> { ["Directory"] = subDirectory });
                continue;
            }

            Interlocked.Increment(ref pendingDirectories);
            if (!directoryWriter.TryWrite(subDirectory))
            {
                // 目录队列满（背压）：当前线程直接处理，避免阻塞
                await EnumerateDirectoryAsync(subDirectory, state, directoryWriter, pendingDirectories, cancellationToken).ConfigureAwait(false);
                if (Interlocked.Decrement(ref pendingDirectories) == 0)
                {
                    directoryWriter.TryComplete();
                }
            }
        }

        Interlocked.Increment(ref state.DirectoriesProcessed);
    }

    /// <summary>
    /// 单目录枚举（带超时）：枚举超过 <see cref="DirectoryEnumerationTimeout"/> 自动放弃该目录
    /// 并记录（防特殊目录/网络重定向/云同步目录 IO 阻塞拖死整体扫描）。
    /// </summary>
    private (string[] Files, string[] SubDirectories)? EnumerateDirectoryEntries(
        string directory, ScanState state, CancellationToken cancellationToken)
    {
        try
        {
            // 立即物化：枚举异常在 MoveNext 抛出，惰性枚举无法被外层 try 捕获
            var task = Task.Run(
                () => (Files: Directory.EnumerateFiles(directory).ToArray(),
                    SubDirectories: Directory.EnumerateDirectories(directory).ToArray()),
                cancellationToken);
            if (task.Wait(_directoryEnumerationTimeout, cancellationToken))
            {
                return task.Result;
            }

            Interlocked.Increment(ref state.TimedOutDirectories);
            _logService.LogEvent(LogEventKind.Exception, "目录枚举超时，已跳过", new Dictionary<string, object?>
            {
                ["Directory"] = directory,
                ["TimeoutSeconds"] = _directoryEnumerationTimeout.TotalSeconds,
            });
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or AggregateException)
        {
            _logService.LogException(ex is AggregateException aggregate && aggregate.InnerException is not null
                ? aggregate.InnerException
                : ex, "FileIndexEngine.EnumerateDirectoryEntries",
                new Dictionary<string, object?> { ["Directory"] = directory });
            return null;
        }
    }

    private async Task ProcessCandidateAsync(
        ScanState state,
        string path,
        string name,
        long sizeBytes,
        long modifiedUtcTicks,
        bool hasReliableSize,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.Scanned);
        lock (state.SeenGate)
        {
            state.SeenPaths.Add(path);
        }

        if (state.Snapshot.TryGetValue(path, out long indexedTicks) && indexedTicks == modifiedUtcTicks)
        {
            return; // 增量：未变化
        }

        if (!hasReliableSize)
        {
            // USN 记录无文件大小：变化项补一次 FileInfo（数量远小于全量）
            try
            {
                sizeBytes = new FileInfo(path).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                _logService.LogException(ex, "FileIndexEngine.ProcessCandidate",
                    new Dictionary<string, object?> { ["Path"] = path });
                return;
            }
        }

        string? publisher = null;
        string? productName = null;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && sizeBytes > 0)
        {
            (publisher, productName) = TryReadVersionInfo(path);
        }

        var record = new FileIndexRecord(
            path, name, publisher, productName, sizeBytes,
            new DateTimeOffset(modifiedUtcTicks, TimeSpan.Zero), Hash: null, DateTimeOffset.UtcNow);

        Interlocked.Increment(ref state.Changed);
        await state.RecordWriter.WriteAsync(record, cancellationToken).ConfigureAwait(false);

        if ((state.Scanned & 0x3FF) == 0)
        {
            state.Progress?.Report(new ScanProgress("索引枚举", state.Scanned, null, path));
        }
    }

    private (string? Publisher, string? ProductName) TryReadVersionInfo(string path)
    {
        try
        {
            System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            return (NullIfEmpty(info.CompanyName), NullIfEmpty(info.ProductName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _logService.LogException(ex, "FileIndexEngine.TryReadVersionInfo",
                new Dictionary<string, object?> { ["Path"] = path });
            return (null, null);
        }
    }

    private async Task ConsumeRecordsAsync(ChannelReader<FileIndexRecord> reader, CancellationToken cancellationToken)
    {
        var batch = new List<FileIndexRecord>(BatchSize);
        await foreach (FileIndexRecord record in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(record);
            if (batch.Count >= BatchSize)
            {
                await _repository.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _repository.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, long>> LoadSnapshotAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        IReadOnlyList<(string Path, long Modified)> rows = await _database.QueryAsync(
            "SELECT path, modified FROM file_index;",
            static reader => (reader.GetString(0), reader.GetInt64(1)),
            null,
            cancellationToken).ConfigureAwait(false);

        var snapshot = new Dictionary<string, long>(rows.Count, StringComparer.OrdinalIgnoreCase);
        foreach ((string path, long modified) in rows)
        {
            snapshot[path] = modified;
        }

        return snapshot;
    }

    /// <summary>
    /// 陈旧记录清理：快照键与本次扫描所见路径（HashSet，O(n) 无排序）求差，
    /// 批量删除索引中已消失的文件。替代原 MMF 全量外排归并（增量场景慢于全量）。
    /// </summary>
    private long RemoveStaleRecords(
        IReadOnlyList<string> roots,
        Dictionary<string, long> snapshot,
        HashSet<string> seenPaths,
        CancellationToken cancellationToken)
    {
        var stale = new List<string>();
        lock (snapshot)
        {
            foreach (string path in snapshot.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seenPaths.Contains(path) && IsUnderAnyRoot(path, roots))
                {
                    stale.Add(path);
                }
            }
        }

        if (stale.Count == 0)
        {
            return 0;
        }

        const string deleteSql = "DELETE FROM file_index WHERE path = $path;";
        return _database.ExecuteBatchAsync(
            deleteSql,
            stale.Select(static path => (IReadOnlyList<SqliteParameter>)[new SqliteParameter("$path", path)]),
            cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private void ReportHeartbeat(ScanState state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _logService.LogEvent(LogEventKind.PerformanceMetrics, "文件索引心跳", new Dictionary<string, object?>
        {
            ["DirectoriesProcessed"] = Interlocked.Read(ref state.DirectoriesProcessed),
            ["FilesScanned"] = Interlocked.Read(ref state.Scanned),
            ["Changed"] = Interlocked.Read(ref state.Changed),
            ["TimedOutDirectories"] = Interlocked.Read(ref state.TimedOutDirectories),
            ["CurrentDirectory"] = Volatile.Read(ref state.CurrentDirectory),
        });
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (ScanScope.IsForbidden(e.FullPath))
        {
            return;
        }

        lock (_pendingGate)
        {
            _pendingWatcherPaths.Add(e.FullPath);
        }

        try
        {
            _watchDebounceTimer.Change(WatcherDebounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Dispose 竞态：事件可安全丢弃
        }
    }

    private void OnWatcherDebounceElapsed(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        string[] paths;
        lock (_pendingGate)
        {
            paths = [.. _pendingWatcherPaths];
            _pendingWatcherPaths.Clear();
        }

        if (paths.Length == 0)
        {
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await _database.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                var upserts = new List<FileIndexRecord>();
                var deletes = new List<string>();
                foreach (string path in paths)
                {
                    try
                    {
                        var info = new FileInfo(path);
                        if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            string? publisher = null;
                            string? productName = null;
                            if (info.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && info.Length > 0)
                            {
                                (publisher, productName) = TryReadVersionInfo(path);
                            }

                            upserts.Add(new FileIndexRecord(
                                info.FullName, info.Name, publisher, productName, info.Length,
                                new DateTimeOffset(info.LastWriteTimeUtc.Ticks, TimeSpan.Zero), null, DateTimeOffset.UtcNow));
                        }
                        else if (!info.Exists && !Directory.Exists(path))
                        {
                            deletes.Add(path);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logService.LogException(ex, "FileIndexEngine.WatcherUpdate",
                            new Dictionary<string, object?> { ["Path"] = path });
                    }
                }

                if (upserts.Count > 0)
                {
                    await _repository.UpsertBatchAsync(upserts, CancellationToken.None).ConfigureAwait(false);
                }

                if (deletes.Count > 0)
                {
                    await _database.ExecuteBatchAsync(
                        "DELETE FROM file_index WHERE path = $path;",
                        deletes.Select(static path => (IReadOnlyList<SqliteParameter>)[new SqliteParameter("$path", path)]),
                        CancellationToken.None).ConfigureAwait(false);
                }

                _logService.LogEvent(LogEventKind.PerformanceMetrics, "文件系统监控增量更新", new Dictionary<string, object?>
                {
                    ["Upserts"] = upserts.Count,
                    ["Deletes"] = deletes.Count,
                });
                IndexChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                _logService.LogException(ex, "FileIndexEngine.OnWatcherDebounceElapsed");
            }
        });
    }

    private static bool IsUnderAnyRoot(string path, IReadOnlyList<string> roots)
    {
        foreach (string root in roots)
        {
            ReadOnlySpan<char> rootSpan = root.AsSpan();
            if (path.AsSpan().StartsWith(rootSpan, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class ScanState(
        ChannelWriter<FileIndexRecord> recordWriter,
        HashSet<string> seenPaths,
        Dictionary<string, long> snapshot,
        IProgress<ScanProgress>? progress)
    {
        public ChannelWriter<FileIndexRecord> RecordWriter { get; } = recordWriter;

        public HashSet<string> SeenPaths { get; } = seenPaths;

        public Dictionary<string, long> Snapshot { get; } = snapshot;

        public IProgress<ScanProgress>? Progress { get; } = progress;

        public object SeenGate { get; } = new();

        public long Scanned;

        public long Changed;

        public long DirectoriesProcessed;

        public long TimedOutDirectories;

        public string? CurrentDirectory;
    }
}

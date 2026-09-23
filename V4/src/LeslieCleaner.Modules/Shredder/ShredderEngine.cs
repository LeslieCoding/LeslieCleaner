using System.Collections.Concurrent;
using System.Security.Cryptography;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Core.Threading;

namespace LeslieCleaner.Modules.Shredder;

/// <summary>
/// 碎纸机引擎。DoD 3/7 遍、Gutmann 35 遍覆写（真实模式表），
/// SSD 自动切换安全策略（加密随机单遍 + 截断 + 删除）。
/// 任务队列支持 AddFiles/AddFolder/Start/Pause/Resume/Cancel 与进度报告。
/// 关键路径防护：盘根、Windows 整树、Program Files 整树一律拦截；
/// 符号链接只删除不覆写（避免覆写穿透到目标）。
/// 粉碎即不可恢复：这是本模块的设计目的，全部操作留日志。
/// </summary>
public sealed class ShredderEngine
{
    private const int WriteBufferSize = 1024 * 1024;

    private readonly ILogService _logService;
    private readonly ConcurrentQueue<ShredTask> _queue = new();
    private readonly List<ShredTask> _allTasks = [];
    private readonly object _tasksGate = new();
    private readonly PauseTokenSource _pauseTokenSource = new();
    private readonly Func<string, bool?> _ssdProbe;
    private readonly ISecurityOperationGate? _securityOperationGate;

    private CancellationTokenSource? _activeRun;
    private int _running;

    /// <summary>以日志服务创建引擎。</summary>
    /// <param name="logService">日志服务。</param>
    /// <param name="ssdProbe">SSD 探针（测试注入）；为 <see langword="null"/> 时用真实 IOCTL 探测。</param>
    /// <param name="securityOperationGate">启动安全检查失败时阻止不可恢复粉碎的运行时闸门。</param>
    public ShredderEngine(
        ILogService logService,
        Func<string, bool?>? ssdProbe = null,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _logService = logService;
        _ssdProbe = ssdProbe ?? StorageMediaDetector.IsSsd;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>当前是否暂停。</summary>
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <summary>当前是否有执行中的队列。</summary>
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    /// <summary>
    /// 向队列追加文件。返回任务标识列表（含被防护拦截项，状态 Blocked）。
    /// </summary>
    /// <param name="paths">文件路径集合。</param>
    public IReadOnlyList<ShredTask> AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var added = new List<ShredTask>();
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(path);
            ShredTask task = GuardCheck(fullPath, isDirectory: false, out string? blockReason)
                ? new ShredTask(Guid.NewGuid(), fullPath, false, ShredTaskStatus.Blocked, 0, 0, blockReason)
                : new ShredTask(Guid.NewGuid(), fullPath, false, ShredTaskStatus.Pending, 0, 0, null);
            Enqueue(task);
            added.Add(task);
        }

        return added;
    }

    /// <summary>
    /// 向队列追加文件夹（递归粉碎其中全部文件）。
    /// </summary>
    /// <param name="directoryPath">文件夹路径。</param>
    public ShredTask AddFolder(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        string fullPath = Path.GetFullPath(directoryPath);
        ShredTask task = GuardCheck(fullPath, isDirectory: true, out string? blockReason)
            ? new ShredTask(Guid.NewGuid(), fullPath, true, ShredTaskStatus.Blocked, 0, 0, blockReason)
            : new ShredTask(Guid.NewGuid(), fullPath, true, ShredTaskStatus.Pending, 0, 0, null);
        Enqueue(task);
        return task;
    }

    /// <summary>当前任务快照（含历史状态）。</summary>
    public IReadOnlyList<ShredTask> GetTasks()
    {
        lock (_tasksGate)
        {
            return [.. _allTasks];
        }
    }

    /// <summary>
    /// 从队列移除指定任务（仅 Pending/Blocked 可移除；执行中或已完成的不动）。
    /// 返回实际移除数量。
    /// </summary>
    /// <param name="taskIds">任务标识集合。</param>
    public int RemovePendingTasks(IEnumerable<Guid> taskIds)
    {
        ArgumentNullException.ThrowIfNull(taskIds);
        var ids = new HashSet<Guid>(taskIds);
        lock (_tasksGate)
        {
            return _allTasks.RemoveAll(t =>
                ids.Contains(t.Id) && t.Status is ShredTaskStatus.Pending or ShredTaskStatus.Blocked);
        }
    }

    /// <summary>
    /// 启动队列执行（幂等：执行中再次调用直接返回当前任务集合）。
    /// </summary>
    /// <param name="method">粉碎方法；SSD 上自动降级为 <see cref="ShredMethod.SsdSafe"/>（除显式选择外）。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<ShredTask>> StartAsync(
        ShredMethod method = ShredMethod.DoD3Pass,
        IProgress<ShredProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _securityOperationGate?.EnsureSensitiveOperationAllowed("文件粉碎");
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return GetTasks();
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeRun = linkedCts;
        try
        {
            _logService.LogEvent(LogEventKind.DeleteStart, "粉碎队列开始",
                new Dictionary<string, object?> { ["Method"] = method.ToString() });

            while (_queue.TryDequeue(out ShredTask? task))
            {
                if (!TryMarkRunning(task.Id, out task))
                {
                    continue;
                }

                linkedCts.Token.ThrowIfCancellationRequested();
                await _pauseTokenSource.Token.WaitWhilePausedAsync(linkedCts.Token).ConfigureAwait(false);
                try
                {
                    task = await ExecuteTaskAsync(task, method, progress, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    UpdateTask(task with { Status = ShredTaskStatus.Cancelled });
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(ex, "ShredderEngine.ExecuteTask",
                        new Dictionary<string, object?> { ["Path"] = task.Path });
                    task = task with { Status = ShredTaskStatus.Failed, Error = ex.Message };
                }

                UpdateTask(task);
            }

            _logService.LogEvent(LogEventKind.DeleteFinish, "粉碎队列完成");
            return GetTasks();
        }
        finally
        {
            _activeRun = null;
            Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <summary>暂停队列。</summary>
    public void Pause() => _pauseTokenSource.Pause();

    /// <summary>继续队列。</summary>
    public void Resume() => _pauseTokenSource.Resume();

    /// <summary>取消队列。</summary>
    public void Cancel() => _activeRun?.Cancel();

    /// <summary>
    /// 关键路径防护判定（纯函数）：盘根、Windows 整树、Program Files 整树返回
    /// <see langword="true"/>（拦截）。
    /// </summary>
    /// <param name="fullPath">完整路径。</param>
    public static bool IsProtectedPath(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        string? root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd('\\', '/'), root?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return true; // 盘根
        }

        var protectedPrefixes = new List<string>(3);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windows))
        {
            protectedPrefixes.Add(windows);
        }

        string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        if (!string.IsNullOrEmpty(programFiles))
        {
            protectedPrefixes.Add(programFiles);
        }

        string? programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            protectedPrefixes.Add(programFilesX86);
        }

        foreach (string prefix in protectedPrefixes)
        {
            if (fullPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(prefix.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GuardCheck(string fullPath, bool isDirectory, out string? blockReason)
    {
        if (IsProtectedPath(fullPath))
        {
            blockReason = "关键路径防护拦截（盘根/Windows/Program Files）";
            return true;
        }

        blockReason = null;
        return isDirectory ? !Directory.Exists(fullPath) && Fail(out blockReason, "目录不存在") : !File.Exists(fullPath) && Fail(out blockReason, "文件不存在");

        static bool Fail(out string? reason, string message)
        {
            reason = message;
            return true;
        }
    }

    private async Task<ShredTask> ExecuteTaskAsync(
        ShredTask task,
        ShredMethod method,
        IProgress<ShredProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<string> files = task.IsDirectory
            ? [.. Directory.EnumerateFiles(task.Path, "*", SearchOption.AllDirectories)]
            : [task.Path];

        ShredMethod effectiveMethod = method;
        bool? isSsd = _ssdProbe(task.Path);
        if (isSsd == true && method != ShredMethod.SsdSafe)
        {
            effectiveMethod = ShredMethod.SsdSafe;
            _logService.LogEvent(LogEventKind.UserAction, "检测到 SSD，切换安全粉碎策略",
                new Dictionary<string, object?> { ["Path"] = task.Path });
        }

        IReadOnlyList<ShredPatternProvider.PassPattern> passes = ShredPatternProvider.GetPasses(effectiveMethod);
        int processed = 0;
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _pauseTokenSource.Token.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            await ShredFileAsync(file, passes, processed, files.Count, progress, cancellationToken).ConfigureAwait(false);
            processed++;
            UpdateTask(task with { TotalFiles = files.Count, ProcessedFiles = processed });
        }

        if (task.IsDirectory)
        {
            try
            {
                Directory.Delete(task.Path, recursive: true);
            }
            catch (IOException ex)
            {
                // 目录树残留（如只读子目录）：文件已粉碎，目录删除失败不阻断
                _logService.LogException(ex, "ShredderEngine.DeleteDirectory",
                    new Dictionary<string, object?> { ["Path"] = task.Path });
            }
        }

        return task with { Status = ShredTaskStatus.Completed, TotalFiles = files.Count, ProcessedFiles = processed };
    }

    private async Task ShredFileAsync(
        string path,
        IReadOnlyList<ShredPatternProvider.PassPattern> passes,
        int processedFiles,
        int totalFiles,
        IProgress<ShredProgress>? progress,
        CancellationToken cancellationToken)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            // 符号链接/联接点：只删除链接本身，绝不覆写（防止穿透到目标）
            File.Delete(path);
            _logService.LogEvent(LogEventKind.DeleteFinish, "符号链接已删除（未覆写）",
                new Dictionary<string, object?> { ["Path"] = path });
            return;
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        long length = new FileInfo(path).Length;
        for (int passIndex = 0; passIndex < passes.Count; passIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShredPatternProvider.PassPattern pass = passes[passIndex];
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None,
                WriteBufferSize, FileOptions.Asynchronous | FileOptions.WriteThrough);
            byte[] buffer = new byte[WriteBufferSize];
            long remaining = length;
            long written = 0;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunk = (int)Math.Min(buffer.Length, remaining);
                ShredPatternProvider.FillBuffer(pass, buffer.AsSpan(0, chunk));
                await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
                remaining -= chunk;
                written += chunk;
                progress?.Report(new ShredProgress(path, passIndex + 1, passes.Count, processedFiles, totalFiles, written, length));
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        // 截断 → 改名（随机名）→ 删除，抹除目录项线索
        await using (FileStream truncate = new(path, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            truncate.SetLength(0);
        }

        string? directory = Path.GetDirectoryName(path);
        string renamed = Path.Combine(directory ?? string.Empty, RandomNumberGenerator.GetHexString(16));
        File.Move(path, renamed);
        File.Delete(renamed);
    }

    private void Enqueue(ShredTask task)
    {
        lock (_tasksGate)
        {
            _allTasks.Add(task);
        }

        if (task.Status == ShredTaskStatus.Pending)
        {
            _queue.Enqueue(task);
        }
    }

    private void UpdateTask(ShredTask updated)
    {
        lock (_tasksGate)
        {
            int index = _allTasks.FindIndex(t => t.Id == updated.Id);
            if (index >= 0)
            {
                _allTasks[index] = updated;
            }
        }
    }

    private bool TryMarkRunning(Guid taskId, out ShredTask task)
    {
        lock (_tasksGate)
        {
            int index = _allTasks.FindIndex(task => task.Id == taskId);
            if (index < 0 || _allTasks[index].Status != ShredTaskStatus.Pending)
            {
                task = default!;
                return false;
            }

            task = _allTasks[index] with { Status = ShredTaskStatus.Running };
            _allTasks[index] = task;
            return true;
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Startup.Classification;

namespace LeslieCleaner.Modules.Startup;

/// <summary>
/// 启动项生命周期编排：七日扫描缓存、受控延迟启动、失败自动恢复、历史趋势和报告。
/// 本类型不采集 ETW；性能数据必须由调用方以明确质量标签提交。
/// </summary>
public sealed class StartupLifecycleManager
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IStartupEntryOperations _operations;
    private readonly IDelayedStartupTaskController _taskController;
    private readonly IPathService _paths;
    private readonly IStartupSystemStateProbe _stateProbe;
    private readonly ILogService _log;
    private readonly IStartupClock _clock;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    /// <summary>创建生命周期管理器。</summary>
    public StartupLifecycleManager(
        IStartupEntryOperations operations,
        IDelayedStartupTaskController taskController,
        IPathService paths,
        ILogService log,
        IStartupSystemStateProbe stateProbe,
        IStartupClock? clock = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _taskController = taskController ?? throw new ArgumentNullException(nameof(taskController));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _stateProbe = stateProbe ?? throw new ArgumentNullException(nameof(stateProbe));
        _clock = clock ?? new SystemStartupClock();
    }

    /// <summary>默认复用七日内缓存；ForceRefresh 会执行真实扫描。</summary>
    public async Task<StartupScanSnapshot> ScanAsync(
        StartupScanRequest? request = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new StartupScanRequest();
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ScanCache? cache = request.ForceRefresh
                ? null
                : await ReadAsync<ScanCache>(CachePath, cancellationToken).ConfigureAwait(false);
            if (cache is not null && cache.CapturedUtc <= _clock.UtcNow &&
                _clock.UtcNow - cache.CapturedUtc <= CacheLifetime)
            {
                await AppendHistoryAsync(new StartupOperationRecord(
                    _clock.UtcNow, StartupOperationKind.CachedScan, null, true,
                    $"使用七日缓存，共 {cache.Items.Count} 项。"), cancellationToken).ConfigureAwait(false);
                return new StartupScanSnapshot(cache.Items, cache.CapturedUtc,
                    cache.CapturedUtc + CacheLifetime, true);
            }

            IReadOnlyList<StartupItem> items = await _operations.EnumerateAsync(progress, cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset captured = _clock.UtcNow;
            await WriteAtomicAsync(CachePath, new ScanCache(captured, items), cancellationToken)
                .ConfigureAwait(false);
            await AppendHistoryAsync(new StartupOperationRecord(captured, StartupOperationKind.Scan,
                null, true, $"完成真实扫描，共 {items.Count} 项。"), cancellationToken).ConfigureAwait(false);
            return new StartupScanSnapshot(items, captured, captured + CacheLifetime, false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>显式使扫描缓存失效；模块执行任何变更时也会自动失效。</summary>
    public async Task InvalidateScanCacheAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeleteFile(CachePath);
            await AppendHistoryAsync(new StartupOperationRecord(_clock.UtcNow,
                StartupOperationKind.CacheInvalidated, null, true, Sanitize(reason)), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// 安全配置延迟启动：只允许 UserApplication 且必须显式确认；先备份禁用原入口，
    /// 再创建并验证受控任务。创建、验证或持久化失败时自动删除任务并恢复原入口。
    /// </summary>
    public async Task<DelayedStartupResult> ConfigureDelayedStartAsync(
        DelayedStartupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? denied = ValidateDelay(request);
            if (denied is not null)
            {
                await AppendHistoryAsync(new StartupOperationRecord(_clock.UtcNow,
                    StartupOperationKind.Delay, request.Detail.Item.Id, false, denied), cancellationToken)
                    .ConfigureAwait(false);
                return new DelayedStartupResult(request.Detail.Item.Id, false, false, false, denied);
            }

            List<DelayedStartupRecord> records = await ReadListAsync<DelayedStartupRecord>(DelayPath,
                cancellationToken).ConfigureAwait(false);
            DelayedStartupRecord? prior = records.FirstOrDefault(record => record.ItemId == request.Detail.Item.Id);
            if (prior is not null)
            {
                bool taskExists = await _taskController.ExistsAsync(prior.Task.TaskName, cancellationToken)
                    .ConfigureAwait(false);
                if (taskExists && prior.Task.Delay == TimeSpan.FromSeconds((int)request.Delay))
                {
                    return new DelayedStartupResult(request.Detail.Item.Id, true, true, false, null);
                }

                return new DelayedStartupResult(request.Detail.Item.Id, false, false, false,
                    "已存在不同或损坏的延迟配置；请先恢复原入口。");
            }

            (string executable, string arguments) = StartupCommandSafety.Parse(request.Detail.Item.Command!);
            if (!File.Exists(executable))
            {
                return new DelayedStartupResult(request.Detail.Item.Id, false, false, false,
                    "启动程序不存在；未修改原启动入口。");
            }

            var task = new DelayedStartupTaskSpec(
                BuildTaskName(request.Detail.Item.Id), executable, arguments,
                TimeSpan.FromSeconds((int)request.Delay),
                $"LeslieCleaner controlled delayed startup: {Sanitize(request.Detail.Item.Name)}");
            StartupChangeResult disabled = await _operations.DisableAsync(request.Detail.Item, cancellationToken)
                .ConfigureAwait(false);
            if (!disabled.Success)
            {
                return new DelayedStartupResult(request.Detail.Item.Id, false, false, false,
                    disabled.Error ?? "无法备份并禁用原启动入口。");
            }

            if (await _stateProbe.IsEnabledAsync(request.Detail.Item, cancellationToken).ConfigureAwait(false))
            {
                StartupChangeResult rollback = await _operations.EnableAsync(request.Detail.Item.Id, CancellationToken.None)
                    .ConfigureAwait(false);
                return new DelayedStartupResult(request.Detail.Item.Id, false, false, rollback.Success,
                    $"原启动入口状态复查仍为启用；已停止创建任务并恢复（成功={rollback.Success}）。");
            }

            try
            {
                TimeSpan operationTimeout = request.Timeout ?? TimeSpan.FromSeconds(30);
                if (operationTimeout <= TimeSpan.Zero || operationTimeout > TimeSpan.FromMinutes(2))
                {
                    throw new ArgumentOutOfRangeException(nameof(request),
                        "任务超时必须大于 0 且不超过 2 分钟。");
                }

                using CancellationTokenSource timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(operationTimeout);
                await _taskController.CreateAsync(task, timeout.Token).ConfigureAwait(false);
                if (!await _taskController.ExistsAsync(task.TaskName, timeout.Token).ConfigureAwait(false))
                {
                    throw new LeslieCleanerException("延迟任务创建后状态验证失败。");
                }

                records.Add(new DelayedStartupRecord(request.Detail.Item.Id,
                    request.Detail.Item.Name, task, _clock.UtcNow, request.Detail.Item));
                await WriteAtomicAsync(DelayPath, records, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                bool restored = await RollbackAsync(request.Detail.Item.Id, task).ConfigureAwait(false);
                await AppendHistoryWithoutCancellationAsync(new StartupOperationRecord(_clock.UtcNow,
                    StartupOperationKind.Delay, request.Detail.Item.Id, false,
                    $"延迟任务取消或超时；原入口恢复={restored}。"));
                throw;
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                bool restored = await RollbackAsync(request.Detail.Item.Id, task).ConfigureAwait(false);
                _log.LogException(ex, "StartupLifecycleManager.ConfigureDelayedStart",
                    new Dictionary<string, object?> { ["ItemId"] = request.Detail.Item.Id });
                await AppendHistoryAsync(new StartupOperationRecord(_clock.UtcNow,
                    StartupOperationKind.Delay, request.Detail.Item.Id, false,
                    $"延迟任务失败；原入口恢复={restored}。"), CancellationToken.None)
                    .ConfigureAwait(false);
                return new DelayedStartupResult(request.Detail.Item.Id, false, false, restored,
                    Sanitize(ex.Message));
            }

            DeleteFile(CachePath);
            await AppendHistoryAsync(new StartupOperationRecord(_clock.UtcNow,
                StartupOperationKind.Delay, request.Detail.Item.Id, true,
                $"已配置 {(int)request.Delay} 秒受控延迟。"), cancellationToken).ConfigureAwait(false);
            return new DelayedStartupResult(request.Detail.Item.Id, true, false, false, null);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>删除受控任务并从备份恢复原启动入口；恢复失败时重建延迟任务。</summary>
    public async Task<DelayedStartupResult> RestoreDelayedStartAsync(
        string itemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<DelayedStartupRecord> records = await ReadListAsync<DelayedStartupRecord>(DelayPath,
                cancellationToken).ConfigureAwait(false);
            DelayedStartupRecord? record = records.FirstOrDefault(candidate => candidate.ItemId == itemId);
            if (record is null)
            {
                return new DelayedStartupResult(itemId, true, true, true, null);
            }

            await _taskController.DeleteAsync(record.Task.TaskName, cancellationToken).ConfigureAwait(false);
            StartupChangeResult restored = await _operations.EnableAsync(itemId, cancellationToken)
                .ConfigureAwait(false);
            bool originalEnabled = restored.Success && record.OriginalItem is not null &&
                await _stateProbe.IsEnabledAsync(record.OriginalItem, cancellationToken).ConfigureAwait(false);
            if (!restored.Success || !originalEnabled)
            {
                bool recoveredTask = false;
                try
                {
                    await _taskController.CreateAsync(record.Task, CancellationToken.None).ConfigureAwait(false);
                    recoveredTask = await _taskController.ExistsAsync(record.Task.TaskName, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    _log.LogException(ex, "StartupLifecycleManager.RecoverDelayedTask");
                }

                return new DelayedStartupResult(itemId, false, false, false,
                    $"原入口恢复失败；延迟任务恢复={recoveredTask}。{restored.Error}");
            }

            records.RemoveAll(candidate => candidate.ItemId == itemId);
            await WriteAtomicAsync(DelayPath, records, cancellationToken).ConfigureAwait(false);
            DeleteFile(CachePath);
            await AppendHistoryAsync(new StartupOperationRecord(_clock.UtcNow,
                StartupOperationKind.RestoreDelay, itemId, true, "已恢复原启动入口。"), cancellationToken)
                .ConfigureAwait(false);
            return new DelayedStartupResult(itemId, true, false, true, null);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>读取受控延迟启动记录。</summary>
    public async Task<IReadOnlyList<DelayedStartupRecord>> GetDelayedStartsAsync(
        CancellationToken cancellationToken = default) =>
        await ReadListAsync<DelayedStartupRecord>(DelayPath, cancellationToken).ConfigureAwait(false);

    /// <summary>记录由外部来源提交且带明确可信度的开机性能数据。</summary>
    public async Task RecordBootObservationAsync(
        StartupBootObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateObservation(observation);
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<StartupBootObservation> points = await ReadListAsync<StartupBootObservation>(ObservationPath,
                cancellationToken).ConfigureAwait(false);
            points.Add(observation with { Source = Sanitize(observation.Source) });
            points = [.. points.OrderBy(point => point.ObservedUtc).TakeLast(180)];
            await WriteAtomicAsync(ObservationPath, points, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>没有数据时返回 Unavailable；不会生成示例或虚构测量。</summary>
    public async Task<StartupTrend> GetTrendAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StartupBootObservation> points =
            [.. (await ReadListAsync<StartupBootObservation>(ObservationPath, cancellationToken)
                .ConfigureAwait(false)).OrderBy(point => point.ObservedUtc)];
        StartupBootObservation[] valid = [.. points.Where(point =>
            point.Quality != StartupDataQuality.Unavailable && point.BootDuration is not null)];
        if (valid.Length == 0)
        {
            return new StartupTrend(points, StartupDataQuality.Unavailable, null,
                "没有可用开机时长；未采集或伪造 ETW 数值。");
        }

        StartupDataQuality quality = valid.All(point => point.Quality == StartupDataQuality.Measured)
            ? StartupDataQuality.Measured
            : StartupDataQuality.Estimated;
        TimeSpan? change = valid.Length > 1
            ? valid[^1].BootDuration!.Value - valid[0].BootDuration!.Value
            : null;
        return new StartupTrend(points, quality, change,
            quality == StartupDataQuality.Measured
                ? "趋势来自外部提交的实测数据。"
                : "趋势包含估计数据，不能视为真实 ETW 测量。");
    }

    /// <summary>读取最多 1000 条操作历史。</summary>
    public async Task<IReadOnlyList<StartupOperationRecord>> GetHistoryAsync(
        CancellationToken cancellationToken = default) =>
        await ReadListAsync<StartupOperationRecord>(HistoryPath, cancellationToken).ConfigureAwait(false);

    /// <summary>生成带数据可信度标记的优化报告。</summary>
    public async Task<StartupOptimizationReport> CreateReportAsync(
        CancellationToken cancellationToken = default)
    {
        ScanCache? cache = await ReadAsync<ScanCache>(CachePath, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StartupItem> items = cache?.Items ?? [];
        IReadOnlyList<DelayedStartupRecord> delayed = await GetDelayedStartsAsync(cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> delayedIds = [.. delayed.Select(record => record.ItemId)];
        double? before = items.Count == 0 ? null : items.Sum(ImpactScore);
        double? after = items.Count == 0 ? null : items.Sum(item =>
            !item.Enabled ? 0 : delayedIds.Contains(item.Id) ? ImpactScore(item) * 0.5 : ImpactScore(item));
        IReadOnlyList<StartupOperationRecord> history = await GetHistoryAsync(cancellationToken)
            .ConfigureAwait(false);
        return new StartupOptimizationReport(_clock.UtcNow, items.Count,
            items.Count(item => !item.Enabled), delayed.Count, before, after,
            items.Count == 0 ? StartupDataQuality.Unavailable : StartupDataQuality.Estimated,
            await GetTrendAsync(cancellationToken).ConfigureAwait(false),
            [.. history.OrderByDescending(item => item.TimestampUtc).Take(100)]);
    }

    /// <summary>原子导出 UTF-8 JSON 报告。</summary>
    public async Task<string> ExportReportAsync(string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Path.IsPathFullyQualified(destinationPath) ||
            !Path.GetExtension(destinationPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("报告必须导出到绝对 .json 路径。", nameof(destinationPath));
        }

        string fullPath = Path.GetFullPath(destinationPath);
        await WriteAtomicAsync(fullPath, await CreateReportAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    private static string? ValidateDelay(DelayedStartupRequest request)
    {
        StartupItemDetail detail = request.Detail;
        if (!request.UserConfirmed) return "延迟启动必须由用户显式确认。";
        if (detail.Classification.Kind != StartupSourceKind.UserApplication)
            return "只允许 UserApplication 配置延迟启动。";
        if (detail.Protection != DisableProtection.Allowed ||
            detail.Item.Kind is StartupItemKind.Service or StartupItemKind.Driver or StartupItemKind.ScheduledTask)
            return "Microsoft、驱动、服务、受保护项和既有计划任务不允许延迟启动。";
        if (!detail.Item.Enabled) return "原启动入口未启用。";
        if (string.IsNullOrWhiteSpace(detail.Item.Command)) return "缺少可验证启动命令。";
        try { _ = StartupCommandSafety.Parse(detail.Item.Command); }
        catch (ArgumentException ex) { return Sanitize(ex.Message); }
        return null;
    }

    private async Task<bool> RollbackAsync(string itemId, DelayedStartupTaskSpec task)
    {
        try { await _taskController.DeleteAsync(task.TaskName, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (IsExpected(ex)) { _log.LogException(ex, "StartupDelay.RollbackTask"); }
        StartupChangeResult restored = await _operations.EnableAsync(itemId, CancellationToken.None)
            .ConfigureAwait(false);
        DeleteFile(CachePath);
        return restored.Success;
    }

    private async Task AppendHistoryWithoutCancellationAsync(StartupOperationRecord record)
    {
        try { await AppendHistoryAsync(record, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (IsExpected(ex)) { _log.LogException(ex, "StartupHistory.AfterCancel"); }
    }

    private async Task AppendHistoryAsync(StartupOperationRecord record, CancellationToken cancellationToken)
    {
        List<StartupOperationRecord> history = await ReadListAsync<StartupOperationRecord>(HistoryPath,
            cancellationToken).ConfigureAwait(false);
        history.Add(record with { Message = Sanitize(record.Message) });
        await WriteAtomicAsync(HistoryPath,
            history.OrderBy(item => item.TimestampUtc).TakeLast(1000).ToArray(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<List<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken) =>
        await ReadAsync<List<T>>(path, cancellationToken).ConfigureAwait(false) ?? [];

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return default;
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException) { return default; }
    }

    private static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("目标路径缺少目录。", nameof(path));
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporary, fullPath, true);
        }
        finally { DeleteFile(temporary); }
    }

    private static void DeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (DirectoryNotFoundException) { }
    }

    private static void ValidateObservation(StartupBootObservation point)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(point.Source);
        if (point.Quality == StartupDataQuality.Unavailable && point.BootDuration is not null)
            throw new ArgumentException("Unavailable 不能包含时长。", nameof(point));
        if (point.Quality != StartupDataQuality.Unavailable &&
            (point.BootDuration is null || point.BootDuration <= TimeSpan.Zero))
            throw new ArgumentException("Measured/Estimated 必须包含正数时长。", nameof(point));
    }

    private static string BuildTaskName(string itemId)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId)))
            .ToLowerInvariant()[..24];
        return $@"\LeslieCleaner\Delayed\{hash}";
    }

    private static double ImpactScore(StartupItem item) => item.Impact switch
    {
        StartupImpact.Low => 20,
        StartupImpact.Medium => 50,
        StartupImpact.High => 80,
        _ => 0,
    };

    private static string Sanitize(string value)
    {
        string result = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return result.Length <= 240 ? result : result[..240];
    }

    private static bool IsExpected(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or LeslieCleanerException or System.ComponentModel.Win32Exception;

    private string StoreDirectory => Path.Combine(_paths.DataDirectory, "Startup");
    private string CachePath => Path.Combine(StoreDirectory, "scan-cache.json");
    private string DelayPath => Path.Combine(StoreDirectory, "delayed-starts.json");
    private string HistoryPath => Path.Combine(StoreDirectory, "operation-history.json");
    private string ObservationPath => Path.Combine(StoreDirectory, "boot-observations.json");

    private sealed record ScanCache(DateTimeOffset CapturedUtc, IReadOnlyList<StartupItem> Items);
}
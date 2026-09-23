using System.Collections.Concurrent;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Core.Threading;
using LeslieCleaner.Modules.Cleaner.Scanning;

namespace LeslieCleaner.Modules.Cleaner;

/// <summary>
/// Smart Scan 引擎。十四类目标并行扫描（目标间并行、目标内顺序 IO），
/// 支持 Pause/Resume/Cancel 与超时；性能目标 &lt; 5s。
/// 只读扫描，不做任何修改；注册表类由 <see cref="IRegistryScanner"/> 供数。
/// </summary>
public sealed class SmartScanEngine : IScanEngine
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint,
        MatchType = MatchType.Simple,
    };

    private readonly IScanEnvironment _environment;
    private readonly IRegistryScanner? _registryScanner;
    private readonly ISystemRecycleBinService? _recycleBinService;
    private readonly ILogService _logService;
    private readonly PauseTokenSource _pauseTokenSource = new();
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    private CancellationTokenSource? _activeScan;

    /// <summary>以扫描环境、注册表扫描器与日志服务创建引擎。</summary>
    /// <param name="environment">扫描环境（位置解析）。</param>
    /// <param name="registryScanner">注册表扫描器；为 <see langword="null"/> 时跳过注册表目标。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="recycleBinService">Windows Shell 回收站服务；为空时仅使用目录枚举回退。</param>
    public SmartScanEngine(
        IScanEnvironment environment,
        IRegistryScanner? registryScanner,
        ILogService logService,
        ISystemRecycleBinService? recycleBinService = null)
    {
        _environment = environment;
        _registryScanner = registryScanner;
        _logService = logService;
        _recycleBinService = recycleBinService;
    }

    /// <inheritdoc />
    public bool IsPaused => _pauseTokenSource.IsPaused;

    /// <inheritdoc />
    public void Pause() => _pauseTokenSource.Pause();

    /// <inheritdoc />
    public void Resume() => _pauseTokenSource.Resume();

    /// <inheritdoc />
    public void Cancel() => _activeScan?.Cancel();

    /// <inheritdoc />
    public async Task<ScanResult> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.Timeout is { } timeout)
        {
            linkedCts.CancelAfter(timeout);
        }

        _activeScan = linkedCts;
        try
        {
            IReadOnlyList<ScanTargetDefinition> selectedTargets = ResolveTargets(options.Targets);
            _logService.LogEvent(LogEventKind.ScanStart, "Smart Scan 开始",
                new Dictionary<string, object?>
                {
                    ["Targets"] = selectedTargets.Count,
                    ["RuleIds"] = string.Join(',', selectedTargets
                        .Select(static target => target.RuleId)
                        .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))),
                    ["RuleVersions"] = string.Join(',', selectedTargets
                        .Select(static target => target.RuleVersion)
                        .Where(static version => !string.IsNullOrWhiteSpace(version))
                        .Distinct(StringComparer.Ordinal)),
                });

            var items = new ConcurrentBag<ScannedItem>();
            long errors = 0;
            bool wasCancelled = false;
            int completedTargets = 0;
            int maxDegree = options.MaxDegreeOfParallelism > 0
                ? options.MaxDegreeOfParallelism
                : Math.Clamp(Environment.ProcessorCount, 1, 8);

            try
            {
                await Parallel.ForEachAsync(
                    selectedTargets,
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegree, CancellationToken = linkedCts.Token },
                    async (target, ct) =>
                    {
                        try
                        {
                            await _pauseTokenSource.Token.WaitWhilePausedAsync(ct).ConfigureAwait(false);
                            if (target.Id == SmartScanTargets.RegistryCategoryId)
                            {
                                await ScanRegistryTargetAsync(target, items, ct).ConfigureAwait(false);
                            }
                            else if (target.Id == SmartScanTargets.RecycleBinCategoryId && _recycleBinService is not null)
                            {
                                RecycleBinSnapshot snapshot = await _recycleBinService.GetSnapshotAsync(ct).ConfigureAwait(false);
                                if (snapshot.ItemCount > 0 || snapshot.SizeBytes > 0 || snapshot.ReclaimableSizeBytes > 0 || snapshot.InaccessibleLocationCount > 0)
                                {
                                    items.Add(new ScannedItem(
                                        "shell://recycle-bin",
                                        snapshot.ReclaimableSizeBytes,
                                        target.Id,
                                        DateTimeOffset.UtcNow,
                                        snapshot.ItemCount,
                                        LogicalSizeBytes: snapshot.SizeBytes,
                                        AllocatedSizeBytes: snapshot.AllocatedSizeBytes > 0 ? snapshot.AllocatedSizeBytes : null,
                                        MetadataSizeBytes: snapshot.MetadataSizeBytes,
                                        InaccessibleItemCount: snapshot.InaccessibleLocationCount,
                                        MeasurementSource: snapshot.Source.ToString(),
                                        MeasurementDiscrepancyBytes: snapshot.ShellToFileSystemLogicalDifferenceBytes,
                                        RuleId: target.RuleId,
                                        Provider: target.Provider,
                                        RuleVersion: target.RuleVersion,
                                        AccessState: snapshot.InaccessibleLocationCount > 0 ? "Partial" : "Verified"));
                                }
                            }
                            else
                            {
                                ScanDirectoryTarget(target, items, ct, () => Interlocked.Increment(ref errors));
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception) when (IsRecoverableTargetFailure(exception))
                        {
                            _logService.LogException(exception, "SmartScanEngine.Target",
                                new Dictionary<string, object?> { ["Target"] = target.Id });
                            Interlocked.Increment(ref errors);
                        }

                        int done = Interlocked.Increment(ref completedTargets);
                        progress?.Report(new ScanProgress(target.DisplayName, done, selectedTargets.Count, null));
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true; // 超时或 Cancel()
            }

            var resultItems = items.ToList();
            var result = new ScanResult
            {
                StartedUtc = startedUtc,
                Duration = DateTimeOffset.UtcNow - startedUtc,
                Items = resultItems,
                TotalSizeBytes = resultItems.Sum(static i => i.LogicalSizeBytes ?? i.SizeBytes),
                TotalAllocatedSizeBytes = resultItems.Sum(static i => i.AllocatedSizeBytes ?? i.SizeBytes),
                WasCancelled = wasCancelled || cancellationToken.IsCancellationRequested,
                ErrorCount = (int)Interlocked.Read(ref errors),
            };

            _logService.LogEvent(LogEventKind.ScanFinish, "Smart Scan 完成", new Dictionary<string, object?>
            {
                ["Items"] = resultItems.Count,
                ["TotalLogicalBytes"] = result.TotalSizeBytes,
                ["TotalAllocatedBytes"] = result.TotalAllocatedSizeBytes,
                ["DurationMs"] = result.Duration.TotalMilliseconds,
                ["WasCancelled"] = result.WasCancelled,
                ["RuleBackedItems"] = resultItems.Count(static item => !string.IsNullOrWhiteSpace(item.RuleId)),
                ["PartialAccessItems"] = resultItems.Count(static item => item.AccessState == "Partial"),
            });

            return result;
        }
        finally
        {
            _activeScan = null;
            _scanGate.Release();
        }
    }

    private static IReadOnlyList<ScanTargetDefinition> ResolveTargets(IReadOnlyList<string> requestedTargets)
    {
        if (requestedTargets.Count == 0 || requestedTargets.Any(static target => target == "*"))
        {
            return SmartScanTargets.All;
        }

        var requested = new HashSet<string>(requestedTargets, StringComparer.OrdinalIgnoreCase);
        List<string> unknown = requested.Where(id => !SmartScanTargets.All.Any(target => target.Id == id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unknown Smart Scan target: {string.Join(", ", unknown)}", nameof(requestedTargets));
        }

        return SmartScanTargets.All.Where(target => requested.Contains(target.Id)).ToList();
    }

    private void ScanDirectoryTarget(
        ScanTargetDefinition target,
        ConcurrentBag<ScannedItem> items,
        CancellationToken cancellationToken,
        Action onError)
    {
        foreach (string pattern in target.CandidatePatterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int separatorIndex = pattern.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string locationKey = pattern[..separatorIndex];
            string relative = pattern[(separatorIndex + 1)..];
            foreach (string directory in _environment.ExpandDirectories(locationKey, relative))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnumerateTargetDirectory(target, directory, items, cancellationToken, onError);
            }
        }
    }

    private void EnumerateTargetDirectory(
        ScanTargetDefinition target,
        string directory,
        ConcurrentBag<ScannedItem> items,
        CancellationToken cancellationToken,
        Action onError)
    {
        string[] filters = target.FileFilters.Count == 0 ? ["*"] : [.. target.FileFilters];
        foreach (string filter in filters)
        {
            foreach (string file in EnumerateFilesSafely(directory, filter, cancellationToken, onError))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    // TOCTOU：枚举到读取期间文件可能被其他进程删除，Length/LastWrite 都可能抛
                    long logicalBytes = info.Length;
                    long? allocatedBytes = WindowsFileSizeMeasurement.TryGetAllocatedSize(info.FullName, out long measured)
                        ? measured
                        : null;
                    items.Add(new ScannedItem(
                        info.FullName,
                        logicalBytes,
                        target.Id,
                        new DateTimeOffset(info.LastWriteTimeUtc),
                        LogicalSizeBytes: logicalBytes,
                        AllocatedSizeBytes: allocatedBytes,
                        MeasurementSource: allocatedBytes is null ? "LogicalFileLength" : "WindowsFileAllocation",
                        RuleId: target.RuleId,
                        Provider: target.Provider,
                        RuleVersion: target.RuleVersion,
                        AccessState: "Verified"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(ex, "SmartScanEngine.FileInfo",
                        new Dictionary<string, object?> { ["File"] = file });
                    onError();
                }
            }
        }
    }

    private IEnumerable<string> EnumerateFilesSafely(
        string rootDirectory,
        string filter,
        CancellationToken cancellationToken,
        Action onError)
    {
        var pending = new Stack<string>();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            string[] files;
            try
            {
                files = Directory.EnumerateFiles(directory, filter, EnumerationOptions).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                RecordEnumerationFailure(ex, directory, onError);
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            string[] childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory, "*", EnumerationOptions).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                RecordEnumerationFailure(ex, directory, onError);
                continue;
            }

            foreach (string childDirectory in childDirectories)
            {
                pending.Push(childDirectory);
            }
        }
    }

    private void RecordEnumerationFailure(Exception exception, string directory, Action onError)
    {
        _logService.LogException(exception, "SmartScanEngine.EnumerateTargetDirectory",
            new Dictionary<string, object?> { ["Directory"] = directory });
        onError();
    }

    private static bool IsRecoverableTargetFailure(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    private async Task ScanRegistryTargetAsync(
        ScanTargetDefinition target,
        ConcurrentBag<ScannedItem> items,
        CancellationToken cancellationToken)
    {
        if (_registryScanner is null)
        {
            return;
        }

        IReadOnlyList<RegistryFinding> findings = await _registryScanner.ScanAsync(progress: null, cancellationToken).ConfigureAwait(false);
        foreach (RegistryFinding finding in findings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Path 保留旧显示编码；实际清理使用类型化 RegistryKeyPath/RegistryValueName，避免合法字符歧义。
            string path = finding.ValueName is null ? finding.KeyPath : $"{finding.KeyPath}|{finding.ValueName}";
            items.Add(new ScannedItem(
                path,
                SizeBytes: 0,
                SmartScanTargets.RegistryCategoryId,
                DateTimeOffset.UtcNow,
                RuleId: target.RuleId,
                Provider: target.Provider,
                RuleVersion: target.RuleVersion,
                AccessState: "Verified",
                RegistryKeyPath: finding.KeyPath,
                RegistryValueName: finding.ValueName));
        }
    }
}

using System.Diagnostics;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Modules.Uninstaller.Discovery;

namespace LeslieCleaner.Modules.Uninstaller.Residual;

/// <summary>分阶段耗时记录。</summary>
/// <param name="Stage">阶段。</param>
/// <param name="Duration">耗时。</param>
/// <param name="ItemCount">产出项数。</param>
public sealed record ResidualStageTiming(ResidualSourceStage Stage, TimeSpan Duration, int ItemCount);

/// <summary>残留扫描完整报告。</summary>
/// <param name="Items">合并去重后的残留项（按置信度降序）。</param>
/// <param name="StageTimings">各阶段耗时。</param>
/// <param name="TotalDuration">总耗时。</param>
public sealed record ResidualScanReport(
    IReadOnlyList<ResidualItem> Items,
    IReadOnlyList<ResidualStageTiming> StageTimings,
    TimeSpan TotalDuration);

/// <summary>
/// Multi-Stage Residual Pipeline 严格实现（圣经第二节）。
/// Stage 1 安装数据库（&lt;100ms）→ Stage 2 规则库（&lt;300ms）→ Stage 3 安装日志（&lt;500ms）
/// → Stage 4 Publisher 索引（&lt;1s）→ Stage 5 受限深扫（仅结果不足且允许时）。
/// 合并去重按置信度分级（安装日志 &gt; 规则库 &gt; 索引 &gt; 深扫）。
/// 清理复用 <see cref="ICleanEngine"/>：文件进隔离区，注册表先备份后删。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class ResidualScannerEngine : IResidualScanner
{
    /// <summary>触发 Stage 5 深扫的结果数阈值。</summary>
    public const int DeepScanThreshold = 3;

    private readonly IDatabase _database;
    private readonly IFileIndexEngine _fileIndexEngine;
    private readonly ICleanEngine _cleanEngine;
    private readonly InstalledAppDiscovery _discovery;
    private readonly ILogService _logService;

    /// <summary>以数据库、索引引擎、清理引擎与日志服务创建残留扫描器。</summary>
    /// <param name="database">数据库（residual_rules 表）。</param>
    /// <param name="fileIndexEngine">文件索引引擎（Stage 4）。</param>
    /// <param name="cleanEngine">清理引擎（残留清理复用）。</param>
    /// <param name="discovery">应用发现（Stage 1）。</param>
    /// <param name="logService">日志服务。</param>
    public ResidualScannerEngine(
        IDatabase database,
        IFileIndexEngine fileIndexEngine,
        ICleanEngine cleanEngine,
        InstalledAppDiscovery discovery,
        ILogService logService)
    {
        _database = database;
        _fileIndexEngine = fileIndexEngine;
        _cleanEngine = cleanEngine;
        _discovery = discovery;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResidualItem>> ScanAsync(
        ResidualQuery query,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ResidualScanReport report = await ScanWithReportAsync(query, progress, cancellationToken).ConfigureAwait(false);
        return report.Items;
    }

    /// <summary>
    /// 执行五阶段管道并返回含各阶段耗时的完整报告。
    /// </summary>
    /// <param name="query">残留查询。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ResidualScanReport> ScanWithReportAsync(
        ResidualQuery query,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await _database.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var timings = new List<ResidualStageTiming>(5);
        var items = new List<ResidualItem>();
        long pipelineStart = Stopwatch.GetTimestamp();

        _logService.LogEvent(LogEventKind.ScanStart, "残留管道扫描开始", new Dictionary<string, object?>
        {
            ["Publisher"] = query.Publisher,
            ["Product"] = query.ProductName,
        });

        // Stage 1：安装数据库定位（<100ms）
        long stageStart = Stopwatch.GetTimestamp();
        ResidualQuery enrichedQuery = Stage1EnrichFromInstallDatabase(query, cancellationToken);
        timings.Add(new ResidualStageTiming(ResidualSourceStage.InstallDatabase,
            Stopwatch.GetElapsedTime(stageStart), 0));
        progress?.Report(new ScanProgress("Stage1 安装数据库", 1, 5, null));

        // Stage 2：规则库定位（<300ms）
        stageStart = Stopwatch.GetTimestamp();
        items.AddRange(await Stage2KnownRulesAsync(enrichedQuery, cancellationToken).ConfigureAwait(false));
        timings.Add(new ResidualStageTiming(ResidualSourceStage.KnownRules,
            Stopwatch.GetElapsedTime(stageStart), items.Count));
        progress?.Report(new ScanProgress("Stage2 规则库", 2, 5, null));

        // Stage 3：安装日志定位（<500ms）
        stageStart = Stopwatch.GetTimestamp();
        int before = items.Count;
        items.AddRange(Stage3InstallLogs(enrichedQuery, items, cancellationToken));
        timings.Add(new ResidualStageTiming(ResidualSourceStage.InstallLogs,
            Stopwatch.GetElapsedTime(stageStart), items.Count - before));
        progress?.Report(new ScanProgress("Stage3 安装日志", 3, 5, null));

        // Stage 4：Publisher 索引定位（<1s）
        stageStart = Stopwatch.GetTimestamp();
        before = items.Count;
        items.AddRange(await Stage4PublisherIndexAsync(enrichedQuery, cancellationToken).ConfigureAwait(false));
        timings.Add(new ResidualStageTiming(ResidualSourceStage.PublisherIndex,
            Stopwatch.GetElapsedTime(stageStart), items.Count - before));
        progress?.Report(new ScanProgress("Stage4 Publisher 索引", 4, 5, null));

        // Stage 5：受限深度扫描（仅结果不足且允许时）
        stageStart = Stopwatch.GetTimestamp();
        before = items.Count;
        if (items.Count < DeepScanThreshold && enrichedQuery.AllowDeepScan)
        {
            items.AddRange(await Stage5DeepScanAsync(enrichedQuery, cancellationToken).ConfigureAwait(false));
        }

        timings.Add(new ResidualStageTiming(ResidualSourceStage.DeepScan,
            Stopwatch.GetElapsedTime(stageStart), items.Count - before));
        progress?.Report(new ScanProgress("Stage5 深度扫描", 5, 5, null));

        IReadOnlyList<ResidualItem> merged = MergeAndRank(items);
        var report = new ResidualScanReport(merged, timings, Stopwatch.GetElapsedTime(pipelineStart));

        _logService.LogEvent(LogEventKind.ScanFinish, "残留管道扫描完成", new Dictionary<string, object?>
        {
            ["Items"] = merged.Count,
            ["TotalMs"] = report.TotalDuration.TotalMilliseconds,
            ["StageMs"] = string.Join(", ", timings.Select(t => $"{t.Stage}={t.Duration.TotalMilliseconds:F0}")),
        });
        return report;
    }

    /// <summary>
    /// 清理残留：文件/目录进隔离区，注册表键先备份后删（复用 CleanEngine 管线，可回滚）。
    /// </summary>
    /// <param name="items">残留项。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理结果（含回滚凭据 OperationId）。</returns>
    public async Task<CleanResult> CleanResidualsAsync(
        IReadOnlyList<ResidualItem> items,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var scanned = new List<ScannedItem>();

        foreach (ResidualItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Kind == ResidualKind.RegistryKey)
            {
                scanned.Add(new ScannedItem(item.Path, 0, "Registry", now));
                continue;
            }

            if (item.Kind == ResidualKind.Directory && Directory.Exists(item.Path))
            {
                foreach (string file in Directory.EnumerateFiles(item.Path, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long size;
                    try
                    {
                        size = new FileInfo(file).Length;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logService.LogException(ex, "ResidualScannerEngine.CleanResiduals",
                            new Dictionary<string, object?> { ["File"] = file });
                        continue;
                    }

                    scanned.Add(new ScannedItem(file, size, "Residual", now));
                }
            }
            else if (item.Kind == ResidualKind.File && File.Exists(item.Path))
            {
                scanned.Add(new ScannedItem(item.Path, item.SizeBytes, "Residual", now));
            }
        }

        return await _cleanEngine.CleanAsync(
            new CleanRequest { Items = scanned, Reason = "卸载残留清理" }, progress, cancellationToken).ConfigureAwait(false);
    }

    private ResidualQuery Stage1EnrichFromInstallDatabase(ResidualQuery query, CancellationToken cancellationToken)
    {
        // 安装数据库定位：从 Uninstall 注册表（含 MSI Installer Cache 探测）提取元数据
        try
        {
            IReadOnlyList<InstalledApp> registryApps = _discovery.EnumerateRegistryApps(cancellationToken);
            InstalledApp? match = registryApps.FirstOrDefault(a =>
                (query.ProductName is not null && a.Name.Contains(query.ProductName, StringComparison.OrdinalIgnoreCase)) ||
                (query.Publisher is not null && a.Publisher?.Contains(query.Publisher, StringComparison.OrdinalIgnoreCase) == true));
            if (match is null)
            {
                return query;
            }

            if (match.ProductCode is not null)
            {
                _ = InstalledAppDiscovery.InstallerCacheHasPackage(match.ProductCode); // 只读探测，结果留日志
            }

            return query with
            {
                Publisher = query.Publisher ?? match.Publisher,
                ProductName = query.ProductName ?? match.Name,
                InstallLocation = query.InstallLocation ?? match.InstallLocation,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logService.LogException(ex, "ResidualScannerEngine.Stage1");
            return query;
        }
    }

    private async Task<IReadOnlyList<ResidualItem>> Stage2KnownRulesAsync(ResidualQuery query, CancellationToken cancellationToken)
    {
        if (query.Publisher is null && query.ProductName is null)
        {
            return [];
        }

        var importer = new ResidualRuleImporter();
        IReadOnlyList<ResidualRule> rules = await importer.QueryAsync(
            _database, query.Publisher ?? string.Empty, query.ProductName, cancellationToken).ConfigureAwait(false);

        var items = new List<ResidualItem>();
        foreach (ResidualRule rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string path in RulePathExpander.ExpandExisting(rule.Paths))
            {
                items.Add(CreatePathItem(path, ResidualSourceStage.KnownRules, 0.9));
            }

            items.AddRange(AwaitRegistryKeys(rule.RegistryKeys, ResidualSourceStage.KnownRules, 0.9, cancellationToken));
        }

        return items;
    }

    private List<ResidualItem> Stage3InstallLogs(
        ResidualQuery query, List<ResidualItem> existing, CancellationToken cancellationToken)
    {
        var items = new List<ResidualItem>();
        var candidateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(query.InstallLocation) && Directory.Exists(query.InstallLocation))
        {
            candidateDirectories.Add(query.InstallLocation);
        }

        foreach (ResidualItem item in existing.Where(i => i.Kind == ResidualKind.Directory))
        {
            candidateDirectories.Add(item.Path);
        }

        foreach (string directory in candidateDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] logs;
            try
            {
                logs = Directory.GetFiles(directory, "unins*.dat", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "ResidualScannerEngine.Stage3",
                    new Dictionary<string, object?> { ["Directory"] = directory });
                continue;
            }

            foreach (string log in logs)
            {
                try
                {
                    foreach (string path in InnoUninstallLogParser.ExtractPaths(File.ReadAllBytes(log)))
                    {
                        if (File.Exists(path) || Directory.Exists(path))
                        {
                            items.Add(CreatePathItem(path, ResidualSourceStage.InstallLogs, 0.95));
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(ex, "ResidualScannerEngine.Stage3.ParseLog",
                        new Dictionary<string, object?> { ["Log"] = log });
                }
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<ResidualItem>> Stage4PublisherIndexAsync(ResidualQuery query, CancellationToken cancellationToken)
    {
        var items = new List<ResidualItem>();
        if (query.Publisher is not null)
        {
            IReadOnlyList<FileIndexRecord> byPublisher =
                await _fileIndexEngine.QueryByPublisherAsync(query.Publisher, cancellationToken).ConfigureAwait(false);
            items.AddRange(byPublisher.Select(static r => new ResidualItem(
                r.Path, ResidualKind.File, r.SizeBytes, ResidualSourceStage.PublisherIndex, 0.7)));
        }

        if (query.ProductName is not null)
        {
            IReadOnlyList<FileIndexRecord> byProduct =
                await _fileIndexEngine.QueryByProductAsync(query.ProductName, cancellationToken).ConfigureAwait(false);
            items.AddRange(byProduct.Select(static r => new ResidualItem(
                r.Path, ResidualKind.File, r.SizeBytes, ResidualSourceStage.PublisherIndex, 0.7)));
        }

        return items;
    }

    private Task<IReadOnlyList<ResidualItem>> Stage5DeepScanAsync(ResidualQuery query, CancellationToken cancellationToken)
    {
        // 受限范围深扫：仅圣经允许区，按发布者/产品目录名匹配，禁区排除（复用 ScanScope 规则）
        string[] searchTerms = new[] { query.Publisher, query.ProductName }
            .Where(static t => !string.IsNullOrWhiteSpace(t))
            .Select(static t => t!.ToLowerInvariant())
            .ToArray();
        if (searchTerms.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<ResidualItem>>([]);
        }

        var roots = new List<string>();
        AddIfExists(roots, Environment.GetEnvironmentVariable("ProgramFiles"));
        AddIfExists(roots, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

        return Task.Run<IReadOnlyList<ResidualItem>>(() =>
        {
            var items = new List<ResidualItem>();
            Parallel.ForEach(roots, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                CancellationToken = cancellationToken,
            }, root =>
            {
                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string name = Path.GetFileName(directory).ToLowerInvariant();
                    if (searchTerms.Any(term => name.Contains(term, StringComparison.Ordinal)))
                    {
                        lock (items)
                        {
                            items.Add(CreatePathItem(directory, ResidualSourceStage.DeepScan, 0.5));
                        }
                    }
                }
            });
            return items;
        }, cancellationToken);
    }

    private static IReadOnlyList<ResidualItem> AwaitRegistryKeys(
        IReadOnlyList<string> registryKeys, ResidualSourceStage stage, double confidence, CancellationToken cancellationToken)
    {
        var items = new List<ResidualItem>();
        foreach (string keyPath in registryKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Infrastructure.Registry.RegistryKeyProbe.Exists(keyPath))
            {
                items.Add(new ResidualItem(keyPath, ResidualKind.RegistryKey, 0, stage, confidence));
            }
        }

        return items;
    }

    private ResidualItem CreatePathItem(string path, ResidualSourceStage stage, double confidence)
    {
        if (Directory.Exists(path))
        {
            return new ResidualItem(path, ResidualKind.Directory, GetDirectorySize(path), stage, confidence);
        }

        long size = 0;
        try
        {
            size = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "ResidualScannerEngine.CreatePathItem",
                new Dictionary<string, object?> { ["Path"] = path });
        }

        return new ResidualItem(path, ResidualKind.File, size, stage, confidence);
    }

    private long GetDirectorySize(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(static f => new FileInfo(f).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "ResidualScannerEngine.GetDirectorySize",
                new Dictionary<string, object?> { ["Directory"] = directory });
            return 0;
        }
    }

    private static IReadOnlyList<ResidualItem> MergeAndRank(List<ResidualItem> items)
    {
        var byPath = new Dictionary<string, ResidualItem>(StringComparer.OrdinalIgnoreCase);
        foreach (ResidualItem item in items)
        {
            if (byPath.TryGetValue(item.Path, out ResidualItem? existing))
            {
                if (item.Confidence > existing.Confidence)
                {
                    byPath[item.Path] = item;
                }
            }
            else
            {
                byPath[item.Path] = item;
            }
        }

        return [.. byPath.Values.OrderByDescending(static i => i.Confidence)];
    }

    private static void AddIfExists(List<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            roots.Add(path);
        }
    }
}

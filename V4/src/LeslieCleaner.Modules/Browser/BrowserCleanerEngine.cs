using System.Diagnostics;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Modules.Browser;

/// <summary>
/// 浏览器清理引擎。覆盖 Chrome/Edge/Firefox/Brave/Arc/Opera/Vivaldi：
/// 缓存（进隔离区）、Cookies（域名白名单保留）、历史/下载历史/表单（sqlite 行级清理，
/// 修改前整文件备份可回滚）。登录状态保留：不触碰 Login Data/密码库，Cookies 默认保留
/// 常见登录域名（可配置）。浏览器运行中自动跳过并报告。
/// </summary>
public sealed class BrowserCleanerEngine : IBrowserPrivacyCleaner
{
    /// <summary>Cookie 白名单配置键（字符串数组）。</summary>
    public const string CookieWhitelistConfigKey = "browser:cookieWhitelist";

    /// <summary>自动清理规则配置键（BrowserAutoCleanRule 数组）。</summary>
    public const string AutoCleanRulesConfigKey = "browser:autoCleanRules";

    /// <summary>自动清理上次执行时间配置键（规则键 → ISO 时间）。</summary>
    public const string AutoCleanLastRunConfigKey = "browser:autoCleanLastRun";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly BrowserDataType[] AllDataTypes =
        [BrowserDataType.Cache, BrowserDataType.Cookies, BrowserDataType.History,
         BrowserDataType.DownloadHistory, BrowserDataType.FormData];

    private readonly IPathService _pathService;
    private readonly IQuarantineService _quarantineService;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly BrowserEnvironment _environment;
    private readonly Func<string, bool> _processRunningProbe;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>以基础设施服务创建引擎。</summary>
    /// <param name="pathService">路径服务。</param>
    /// <param name="quarantineService">隔离区服务。</param>
    /// <param name="configService">配置服务（白名单/自动规则）。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="environment">浏览器环境；为 <see langword="null"/> 时取系统环境。</param>
    /// <param name="processRunningProbe">进程运行探针（测试注入）；为 <see langword="null"/> 时用真实进程枚举。</param>
    /// <param name="securityOperationGate">Blocks destructive browser cleaning when integrity validation fails.</param>
    public BrowserCleanerEngine(
        IPathService pathService,
        IQuarantineService quarantineService,
        IConfigService configService,
        ILogService logService,
        BrowserEnvironment? environment = null,
        Func<string, bool>? processRunningProbe = null,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _pathService = pathService;
        _quarantineService = quarantineService;
        _configService = configService;
        _logService = logService;
        _environment = environment ?? BrowserEnvironment.FromSystem();
        _processRunningProbe = processRunningProbe ?? DefaultProcessProbe;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>
    /// 发现本机已安装的浏览器用户配置。未安装的自动跳过。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<BrowserProfile>> LocateProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = new List<BrowserProfile>();
        foreach (BrowserDefinition definition in BrowserCatalog.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string userDataRoot = BrowserCatalog.ResolveTemplate(definition.UserDataRoot, _environment);
            if (!Directory.Exists(userDataRoot))
            {
                continue;
            }

            if (definition.Engine == BrowserEngineKind.Firefox)
            {
                profiles.AddRange(LocateFirefoxProfiles(definition, userDataRoot));
            }
            else
            {
                profiles.AddRange(LocateChromiumProfiles(definition, userDataRoot));
            }
        }

        return Task.FromResult<IReadOnlyList<BrowserProfile>>(profiles);
    }

    /// <summary>
    /// 扫描指定数据类型（默认全部），产出带大小与跳过标记的命中项。
    /// </summary>
    /// <param name="dataTypes">数据类型集合；为 <see langword="null"/> 表示全部。</param>
    /// <param name="progress">进度回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<BrowserScanItem>> ScanAsync(
        IReadOnlyList<BrowserDataType>? dataTypes = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        BrowserDataType[] types = dataTypes?.ToArray() ?? AllDataTypes;
        IReadOnlyList<BrowserProfile> profiles = await LocateProfilesAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<BrowserScanItem>();
        int processed = 0;

        _logService.LogEvent(LogEventKind.ScanStart, "浏览器数据扫描开始",
            new Dictionary<string, object?> { ["Profiles"] = profiles.Count });

        foreach (BrowserProfile profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BrowserDefinition definition = BrowserCatalog.All.First(d => d.Id == profile.BrowserId);
            bool running = definition.ProcessNames.Any(name => _processRunningProbe(name));

            foreach (BrowserDataType dataType in types)
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.AddRange(ScanDataType(profile, dataType, running, cancellationToken));
            }

            processed++;
            progress?.Report(new ScanProgress($"扫描 {profile.DisplayName}", processed, profiles.Count, null));
        }

        _logService.LogEvent(LogEventKind.ScanFinish, "浏览器数据扫描完成",
            new Dictionary<string, object?> { ["Items"] = items.Count });
        return items;
    }

    /// <summary>
    /// 执行清理：缓存文件进隔离区；sqlite 先整文件备份再行级清理。
    /// </summary>
    /// <param name="dataTypes">数据类型集合；为 <see langword="null"/> 表示全部。</param>
    /// <param name="progress">进度回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<BrowserCleanResult> CleanAsync(
        IReadOnlyList<BrowserDataType>? dataTypes = null,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _securityOperationGate?.EnsureSensitiveOperationAllowed("Browser privacy cleaning");
        string operationId = Guid.NewGuid().ToString("N");
        IReadOnlyList<BrowserScanItem> items = await ScanAsync(dataTypes, progress: null, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> whitelist = _configService.GetValue(CookieWhitelistConfigKey, BrowserCatalog.DefaultCookieWhitelist);

        long freedBytes = 0;
        int cleaned = 0;
        var skippedBrowsers = new List<string>();
        var databaseBackups = new List<DatabaseBackup>();

        var actionable = items.Where(static i => !i.Skipped && i.SizeBytes >= 0).ToList();
        skippedBrowsers.AddRange(items.Where(static i => i.Skipped).Select(static i => i.Profile.BrowserId).Distinct());

        int index = 0;
        foreach (BrowserScanItem item in actionable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            try
            {
                if (item.DataType == BrowserDataType.Cache)
                {
                    freedBytes += await CleanCacheAsync(item, operationId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    long freed = await CleanDatabaseAsync(item, whitelist, operationId, databaseBackups, cancellationToken).ConfigureAwait(false);
                    freedBytes += Math.Max(freed, 0);
                }

                cleaned++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                _logService.LogException(ex, "BrowserCleanerEngine.CleanAsync",
                    new Dictionary<string, object?> { ["Path"] = item.Path, ["DataType"] = item.DataType.ToString() });
            }

            progress?.Report(new CleanProgress(index, actionable.Count, item.Path));
        }

        await WriteManifestAsync(operationId, databaseBackups, cancellationToken).ConfigureAwait(false);
        _logService.LogEvent(LogEventKind.DeleteFinish, "浏览器数据清理完成", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["FreedBytes"] = freedBytes,
            ["SkippedBrowsers"] = skippedBrowsers,
        });

        return new BrowserCleanResult(operationId, freedBytes, cleaned, skippedBrowsers);
    }

    /// <summary>
    /// 回滚一次清理：把备份的 sqlite 文件复制回原位。
    /// </summary>
    /// <param name="operationId">清理操作标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>全部恢复成功返回 <see langword="true"/>。</returns>
    public async Task<bool> RollbackAsync(string operationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        string manifestPath = GetManifestPath(operationId);
        if (!File.Exists(manifestPath))
        {
            throw new Core.Diagnostics.LeslieCleanerException($"浏览器清理清单不存在: {operationId}");
        }

        _logService.LogEvent(LogEventKind.RestoreStart, "浏览器清理回滚开始",
            new Dictionary<string, object?> { ["OperationId"] = operationId });

        BrowserCleanManifest? manifest;
        await using (FileStream stream = new(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
        {
            manifest = await JsonSerializer.DeserializeAsync<BrowserCleanManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        bool allSucceeded = true;
        foreach (DatabaseBackup backup in manifest?.Backups ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Copy(backup.BackupPath, backup.OriginalPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "BrowserCleanerEngine.RollbackAsync",
                    new Dictionary<string, object?> { ["OriginalPath"] = backup.OriginalPath });
                allSucceeded = false;
            }
        }

        if (allSucceeded)
        {
            File.Delete(manifestPath);
        }

        _logService.LogEvent(LogEventKind.RestoreFinish, "浏览器清理回滚完成", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["AllSucceeded"] = allSucceeded,
        });
        return allSucceeded;
    }

    /// <summary>
    /// 评估自动清理规则：到期规则立即执行并更新上次执行时间。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>被执行的规则数。</returns>
    public async Task<int> RunDueAutoCleanRulesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BrowserAutoCleanRule> rules =
            _configService.GetValue(AutoCleanRulesConfigKey, Array.Empty<BrowserAutoCleanRule>());
        Dictionary<string, string> lastRun =
            _configService.GetValue(AutoCleanLastRunConfigKey, new Dictionary<string, string>());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int executed = 0;
        foreach (BrowserAutoCleanRule rule in rules.Where(static r => r.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string ruleKey = GetRuleKey(rule);
            DateTimeOffset? last = lastRun.TryGetValue(ruleKey, out string? raw) &&
                DateTimeOffset.TryParse(raw, out DateTimeOffset parsed) ? parsed : null;

            if (!IsRuleDue(rule, last, now))
            {
                continue;
            }

            IReadOnlyList<BrowserProfile> profiles = await LocateProfilesAsync(cancellationToken).ConfigureAwait(false);
            if (rule.BrowserId is not null && profiles.All(p => p.BrowserId != rule.BrowserId))
            {
                continue; // 目标浏览器未安装
            }

            await CleanAsync([rule.DataType], progress: null, cancellationToken).ConfigureAwait(false);
            lastRun[ruleKey] = now.ToString("O");
            executed++;
        }

        if (executed > 0)
        {
            await _configService.SetValueAsync(AutoCleanLastRunConfigKey, lastRun, cancellationToken).ConfigureAwait(false);
        }

        return executed;
    }

    /// <summary>
    /// 规则到期判定（纯函数）：从未执行或距上次执行超过间隔天数即到期。
    /// </summary>
    /// <param name="rule">自动清理规则。</param>
    /// <param name="lastRunUtc">上次执行时间；从未执行为 <see langword="null"/>。</param>
    /// <param name="nowUtc">当前时间。</param>
    public static bool IsRuleDue(BrowserAutoCleanRule rule, DateTimeOffset? lastRunUtc, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Enabled && (lastRunUtc is null || nowUtc - lastRunUtc.Value >= TimeSpan.FromDays(rule.IntervalDays));
    }

    /// <inheritdoc />
    public async Task<int> CleanPrivacyDataAsync(
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        BrowserCleanResult result = await CleanAsync(
            [BrowserDataType.Cookies, BrowserDataType.History, BrowserDataType.DownloadHistory, BrowserDataType.FormData],
            progress, cancellationToken).ConfigureAwait(false);
        return result.CleanedItems;
    }

    private IEnumerable<BrowserScanItem> ScanDataType(
        BrowserProfile profile,
        BrowserDataType dataType,
        bool running,
        CancellationToken cancellationToken)
    {
        if (dataType == BrowserDataType.Cache)
        {
            foreach (string cacheDirectory in EnumerateCacheDirectories(profile))
            {
                yield return new BrowserScanItem(profile, dataType, cacheDirectory,
                    GetDirectorySize(cacheDirectory, cancellationToken), running);
            }
        }
        else
        {
            string databasePath = profile.Engine == BrowserEngineKind.Chromium
                ? BrowserCatalog.GetChromiumDatabasePath(dataType, profile.ProfileDirectory)
                : BrowserCatalog.GetFirefoxDatabasePath(dataType, profile.ProfileDirectory);
            if (File.Exists(databasePath))
            {
                yield return new BrowserScanItem(profile, dataType, databasePath, new FileInfo(databasePath).Length, running);
            }
        }
    }

    private IEnumerable<string> EnumerateCacheDirectories(BrowserProfile profile)
    {
        if (profile.Engine == BrowserEngineKind.Firefox)
        {
            string cache2 = Path.Combine(profile.CacheDirectory, "cache2");
            if (Directory.Exists(cache2))
            {
                yield return cache2;
            }

            yield break;
        }

        foreach (string subDirectory in BrowserCatalog.ChromiumCacheSubdirectories)
        {
            string candidate = Path.Combine(profile.ProfileDirectory, subDirectory);
            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private async Task<long> CleanCacheAsync(BrowserScanItem item, string operationId, CancellationToken cancellationToken)
    {
        string[] files;
        try
        {
            files = EnumerateFilesSafely(item.Path, cancellationToken).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "BrowserCleanerEngine.CleanCacheAsync",
                new Dictionary<string, object?> { ["Path"] = item.Path });
            return 0;
        }

        long freed = files.Sum(static f => new FileInfo(f).Length);
        if (files.Length > 0)
        {
            await _quarantineService.QuarantineFilesAsync(files, $"浏览器缓存清理 ({item.Profile.DisplayName})",
                operationId, progress: null, cancellationToken).ConfigureAwait(false);
        }

        return freed;
    }

    private async Task<long> CleanDatabaseAsync(
        BrowserScanItem item,
        IReadOnlyList<string> whitelist,
        string operationId,
        List<DatabaseBackup> backups,
        CancellationToken cancellationToken)
    {
        string backupDirectory = Path.Combine(
            _pathService.EnsureDirectory(_pathService.BackupDirectory), "Browser", operationId);
        Directory.CreateDirectory(backupDirectory);
        string backupPath = Path.Combine(backupDirectory,
            $"{item.Profile.BrowserId}-{item.DataType}-{Path.GetFileName(item.Path)}");
        File.Copy(item.Path, backupPath, overwrite: true);
        backups.Add(new DatabaseBackup(item.Path, backupPath));

        long sizeBefore = new FileInfo(item.Path).Length;
        await Task.Run(() => CleanDatabaseCore(item, whitelist), cancellationToken).ConfigureAwait(false);
        long sizeAfter = new FileInfo(item.Path).Length;
        return sizeBefore - sizeAfter;
    }

    private void CleanDatabaseCore(BrowserScanItem item, IReadOnlyList<string> whitelist)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = item.Path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        try
        {
            switch (item.DataType)
            {
                case BrowserDataType.Cookies:
                    ExecuteCookieClean(connection, item.Profile.Engine, whitelist);
                    break;
                case BrowserDataType.History:
                    if (item.Profile.Engine == BrowserEngineKind.Chromium)
                    {
                        ExecuteNonQuery(connection, "DELETE FROM urls; DELETE FROM visits;");
                    }
                    else
                    {
                        ExecuteNonQuery(connection, "DELETE FROM moz_places; DELETE FROM moz_historyvisits;");
                    }

                    break;
                case BrowserDataType.DownloadHistory:
                    if (item.Profile.Engine == BrowserEngineKind.Chromium)
                    {
                        ExecuteNonQuery(connection, "DELETE FROM downloads; DELETE FROM downloads_url_chains;");
                    }
                    else
                    {
                        ExecuteNonQuery(connection, """
                            DELETE FROM moz_annos WHERE anno_attribute_id IN (
                                SELECT id FROM moz_anno_attributes WHERE name = 'downloads/destinationFileURI');
                            """);
                    }

                    break;
                case BrowserDataType.FormData:
                    ExecuteNonQuery(connection, item.Profile.Engine == BrowserEngineKind.Chromium
                        ? "DELETE FROM autofill;"
                        : "DELETE FROM moz_formhistory;");
                    break;
            }
        }
        finally
        {
            ExecuteNonQuery(connection, "VACUUM;");
        }
    }

    private void ExecuteCookieClean(SqliteConnection connection, BrowserEngineKind engine, IReadOnlyList<string> whitelist)
    {
        // 白名单后缀匹配：host 以白名单域名结尾即保留（覆盖子域）
        string table = engine == BrowserEngineKind.Chromium ? "cookies" : "moz_cookies";
        string hostColumn = engine == BrowserEngineKind.Chromium ? "host_key" : "host";

        var conditions = new List<string>();
        using SqliteCommand command = connection.CreateCommand();
        for (int i = 0; i < whitelist.Count; i++)
        {
            string parameterName = $"$w{i}";
            conditions.Add($"{hostColumn} LIKE {parameterName}");
            command.Parameters.AddWithValue(parameterName, "%" + whitelist[i]);
        }

        command.CommandText = conditions.Count == 0
            ? $"DELETE FROM {table};"
            : $"DELETE FROM {table} WHERE NOT ({string.Join(" OR ", conditions)});";
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private IEnumerable<BrowserProfile> LocateChromiumProfiles(BrowserDefinition definition, string userDataRoot)
    {
        // 含 Local State → 多配置根（Default + Profile *）；否则根本身即配置（如 Opera）
        if (File.Exists(Path.Combine(userDataRoot, "Local State")))
        {
            foreach (string directory in Directory.EnumerateDirectories(userDataRoot))
            {
                if (IsReparsePoint(directory))
                {
                    RecordRejectedProfilePath(definition.Id, directory, "ReparsePoint");
                    continue;
                }

                string name = Path.GetFileName(directory);
                if (name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new BrowserProfile(
                        definition.Id, $"{definition.DisplayName} ({name})", definition.Engine, directory, directory);
                }
            }
        }
        else if (Directory.Exists(Path.Combine(userDataRoot, "Cache")) ||
                 File.Exists(Path.Combine(userDataRoot, "History")))
        {
            yield return new BrowserProfile(
                definition.Id, definition.DisplayName, definition.Engine, userDataRoot, userDataRoot);
        }
    }

    private IEnumerable<BrowserProfile> LocateFirefoxProfiles(BrowserDefinition definition, string userDataRoot)
    {
        string profilesIni = Path.Combine(userDataRoot, "profiles.ini");
        if (!File.Exists(profilesIni))
        {
            yield break;
        }

        string cacheRoot = BrowserCatalog.ResolveTemplate(definition.CacheRoot!, _environment);
        string? relativePath = null;
        foreach (string line in File.ReadLines(profilesIni))
        {
            if (line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = line["Path=".Length..].Trim().Replace('/', '\\');
            }
            else if (line.StartsWith("[", StringComparison.Ordinal) && relativePath is not null)
            {
                // 进入下一节，结算上一节
                BrowserProfile? profile = BuildFirefoxProfile(definition, userDataRoot, cacheRoot, relativePath);
                if (profile is not null)
                {
                    yield return profile;
                }

                relativePath = null;
            }
        }

        BrowserProfile? last = BuildFirefoxProfile(definition, userDataRoot, cacheRoot, relativePath);
        if (last is not null)
        {
            yield return last;
        }
    }

    private BrowserProfile? BuildFirefoxProfile(
        BrowserDefinition definition, string userDataRoot, string cacheRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                RecordRejectedProfilePath(definition.Id, relativePath, "RootedOrEmpty");
            }

            return null;
        }

        string rootDirectory = Path.GetFullPath(userDataRoot);
        string profileDirectory;
        try
        {
            profileDirectory = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            RecordRejectedProfilePath(definition.Id, relativePath, "InvalidPath");
            return null;
        }

        if (!IsPathWithinRoot(profileDirectory, rootDirectory) || IsReparsePoint(profileDirectory))
        {
            RecordRejectedProfilePath(definition.Id, relativePath, "OutsideRootOrReparsePoint");
            return null;
        }

        if (!Directory.Exists(profileDirectory))
        {
            return null;
        }

        string name = Path.GetFileName(profileDirectory);
        return new BrowserProfile(
            definition.Id, $"{definition.DisplayName} ({name})", definition.Engine,
            profileDirectory, Path.Combine(cacheRoot, name));
    }

    private void RecordRejectedProfilePath(string browserId, string path, string reason) =>
        _logService.LogEvent(LogEventKind.Exception, "Browser profile path rejected",
            new Dictionary<string, object?>
            {
                ["BrowserId"] = browserId,
                ["Reason"] = reason,
                ["Path"] = path,
            });

    private static bool IsPathWithinRoot(string candidate, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private long GetDirectorySize(string directory, CancellationToken cancellationToken)
    {
        try
        {
            long total = 0;
            foreach (string file in EnumerateFilesSafely(directory, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += new FileInfo(file).Length;
            }

            return total;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "BrowserCleanerEngine.GetDirectorySize",
                new Dictionary<string, object?> { ["Directory"] = directory });
            return 0;
        }
    }

    private IEnumerable<string> EnumerateFilesSafely(string rootDirectory, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            if (IsReparsePoint(directory))
            {
                _logService.LogEvent(LogEventKind.Exception, "Browser cache reparse point skipped",
                    new Dictionary<string, object?> { ["Path"] = directory });
                continue;
            }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(ex, "BrowserCleanerEngine.EnumerateFiles", new Dictionary<string, object?> { ["Path"] = directory });
                continue;
            }

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsReparsePoint(file))
                {
                    yield return file;
                }
            }

            string[] directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(ex, "BrowserCleanerEngine.EnumerateDirectories", new Dictionary<string, object?> { ["Path"] = directory });
                continue;
            }

            foreach (string childDirectory in directories)
            {
                if (!IsReparsePoint(childDirectory))
                {
                    pending.Push(childDirectory);
                }
            }
        }
    }

    private static bool DefaultProcessProbe(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string GetRuleKey(BrowserAutoCleanRule rule) =>
        $"{rule.BrowserId ?? "*"}:{rule.DataType}";

    private string GetManifestPath(string operationId) =>
        Path.Combine(_pathService.ProgramDataRoot, "Operations", $"browser-{operationId}.json");

    private async Task WriteManifestAsync(
        string operationId, List<DatabaseBackup> backups, CancellationToken cancellationToken)
    {
        string path = GetManifestPath(operationId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, new BrowserCleanManifest(operationId, backups), JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DatabaseBackup(string OriginalPath, string BackupPath);

    private sealed record BrowserCleanManifest(string OperationId, List<DatabaseBackup> Backups);
}

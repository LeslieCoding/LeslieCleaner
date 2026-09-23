using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Privacy;

/// <summary>
/// Privacy Guard 引擎。覆盖圣经清单八类：最近文件、剪贴板历史、搜索历史、
/// 运行历史、Office 历史、浏览器隐私数据（经 <see cref="IBrowserPrivacyCleaner"/> 接口复用）、
/// 地理位置缓存、语音输入缓存。每项先存在性探测，不存在自动跳过；
/// 注册表目标复用 <see cref="ICleanEngine"/> 的"备份后删键/删值"管线，文件进隔离区。
/// </summary>
public sealed class PrivacyGuardEngine
{
    /// <summary>隐私目标在 <see cref="ICleanEngine"/> 中使用的注册表类别标记。</summary>
    public const string RegistryCategoryMarker = "Registry";

    private const string ExplorerSubKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string RecentDocsKey = ExplorerSubKey + @"\RecentDocs";
    private const string WordWheelQueryKey = ExplorerSubKey + @"\WordWheelQuery";
    private const string RunMruKey = ExplorerSubKey + @"\RunMRU";
    private const string OfficeRootKey = @"HKCU\Software\Microsoft\Office";
    private const string DeviceAccessKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\DeviceAccess\Global";

    private static readonly string[] OfficeAppNames = ["Word", "Excel", "PowerPoint", "Access", "Outlook", "Visio", "Project"];

    private readonly IRegistryEngine _registryEngine;
    private readonly ICleanEngine _cleanEngine;
    private readonly IBrowserPrivacyCleaner? _browserPrivacyCleaner;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>以注册表引擎、清理引擎与日志服务创建隐私引擎。</summary>
    /// <param name="registryEngine">注册表引擎。</param>
    /// <param name="cleanEngine">清理引擎（复用备份/隔离/回滚管线）。</param>
    /// <param name="browserPrivacyCleaner">浏览器隐私清理（可为 <see langword="null"/>，此时浏览器类跳过）。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="securityOperationGate">Blocks destructive privacy cleaning when integrity validation fails.</param>
    public PrivacyGuardEngine(
        IRegistryEngine registryEngine,
        ICleanEngine cleanEngine,
        IBrowserPrivacyCleaner? browserPrivacyCleaner,
        ILogService logService,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _registryEngine = registryEngine;
        _cleanEngine = cleanEngine;
        _browserPrivacyCleaner = browserPrivacyCleaner;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>
    /// 扫描全部隐私类别（存在性探测，不存在自动跳过）。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<PrivacyItem>> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logService.LogEvent(LogEventKind.ScanStart, "隐私数据扫描开始");
        var items = new List<PrivacyItem>();

        await ScanRecentFilesAsync(items, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("最近文件", 1, 8, null));

        ScanFileCategory(items, PrivacyCategory.ClipboardHistory, "剪贴板历史",
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Clipboard"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Clipboard"),
        ]);
        progress?.Report(new ScanProgress("剪贴板历史", 2, 8, null));

        await ScanRegistryValuesAsync(items, PrivacyCategory.SearchHistory, "搜索历史", WordWheelQueryKey, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("搜索历史", 3, 8, null));

        await ScanRegistryValuesAsync(items, PrivacyCategory.RunHistory, "运行历史", RunMruKey, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("运行历史", 4, 8, null));

        await ScanOfficeMruAsync(items, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("Office 历史", 5, 8, null));

        if (_browserPrivacyCleaner is not null)
        {
            items.Add(new PrivacyItem(PrivacyCategory.BrowserPrivacy, "浏览器隐私数据（Cookies/历史/表单）", "browser:privacy", IsRegistryTarget: false, 0));
        }

        progress?.Report(new ScanProgress("浏览器隐私数据", 6, 8, null));

        await ScanRegistryValuesAsync(items, PrivacyCategory.LocationCache, "地理位置访问记录", DeviceAccessKey, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("地理位置缓存", 7, 8, null));

        ScanFileCategory(items, PrivacyCategory.SpeechCache, "语音输入缓存",
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Speech_OneCore"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Speech"),
        ]);
        progress?.Report(new ScanProgress("语音输入缓存", 8, 8, null));

        _logService.LogEvent(LogEventKind.ScanFinish, "隐私数据扫描完成",
            new Dictionary<string, object?> { ["Items"] = items.Count });
        return items;
    }

    /// <summary>
    /// 清理指定隐私项：注册表目标经 CleanEngine 备份后删除，文件进隔离区，
    /// 浏览器隐私委托 <see cref="IBrowserPrivacyCleaner"/>。
    /// </summary>
    /// <param name="items">待清理项。</param>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理结果（含回滚凭据 OperationId）。</returns>
    public async Task<CleanResult> CleanAsync(
        IReadOnlyList<PrivacyItem> items,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        _securityOperationGate?.EnsureSensitiveOperationAllowed("Privacy data cleaning");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        bool hasBrowserItem = items.Any(static i => i.Category == PrivacyCategory.BrowserPrivacy);
        var scanned = new List<ScannedItem>();
        foreach (PrivacyItem item in items.Where(static i => i.Category != PrivacyCategory.BrowserPrivacy))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned.Add(new ScannedItem(
                item.Target, item.SizeBytes,
                item.IsRegistryTarget ? RegistryCategoryMarker : item.Category.ToString(), now));
        }

        CleanResult result = await _cleanEngine.CleanAsync(
            new CleanRequest { Items = scanned, Reason = "隐私数据清理" }, progress, cancellationToken).ConfigureAwait(false);

        if (hasBrowserItem && _browserPrivacyCleaner is not null)
        {
            await _browserPrivacyCleaner.CleanPrivacyDataAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// 回滚一次隐私清理（委托 <see cref="ICleanEngine.RollbackAsync"/>）。
    /// </summary>
    /// <param name="operationId">清理操作标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<bool> RollbackAsync(string operationId, CancellationToken cancellationToken = default) =>
        _cleanEngine.RollbackAsync(operationId, cancellationToken);

    private async Task ScanRecentFilesAsync(List<PrivacyItem> items, CancellationToken cancellationToken)
    {
        // Recent 文件夹（跳转列表/快捷方式）
        string recentDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Recent");
        ScanFileCategory(items, PrivacyCategory.RecentFiles, "最近文件", [recentDirectory]);

        // RecentDocs 注册表（值级）
        await ScanRegistryValuesAsync(items, PrivacyCategory.RecentFiles, "最近文件记录（注册表）", RecentDocsKey, cancellationToken).ConfigureAwait(false);
    }

    private void ScanFileCategory(List<PrivacyItem> items, PrivacyCategory category, string description, IReadOnlyList<string> candidateDirectories)
    {
        foreach (string directory in candidateDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "PrivacyGuardEngine.ScanFileCategory",
                    new Dictionary<string, object?> { ["Directory"] = directory });
                continue;
            }

            foreach (string file in files)
            {
                long size;
                try
                {
                    size = new FileInfo(file).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(ex, "PrivacyGuardEngine.FileInfo",
                        new Dictionary<string, object?> { ["File"] = file });
                    continue;
                }

                items.Add(new PrivacyItem(category, description, file, IsRegistryTarget: false, size));
            }
        }
    }

    private async Task ScanRegistryValuesAsync(
        List<PrivacyItem> items, PrivacyCategory category, string description, string keyPath, CancellationToken cancellationToken)
    {
        if (!await _registryEngine.KeyExistsAsync(keyPath, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IReadOnlyList<string> valueNames = await _registryEngine.EnumerateValuesAsync(keyPath, cancellationToken).ConfigureAwait(false);
        foreach (string valueName in valueNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(new PrivacyItem(category, $"{description}（{valueName}）", $"{keyPath}|{valueName}", IsRegistryTarget: true, 0));
        }
    }

    private async Task ScanOfficeMruAsync(List<PrivacyItem> items, CancellationToken cancellationToken)
    {
        if (!await _registryEngine.KeyExistsAsync(OfficeRootKey, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IReadOnlyList<string> versions = await _registryEngine.EnumerateSubKeysAsync(OfficeRootKey, cancellationToken).ConfigureAwait(false);
        foreach (string version in versions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string app in OfficeAppNames)
            {
                string fileMruKey = $@"{OfficeRootKey}\{version}\{app}\File MRU";
                if (await _registryEngine.KeyExistsAsync(fileMruKey, cancellationToken).ConfigureAwait(false))
                {
                    items.Add(new PrivacyItem(
                        PrivacyCategory.OfficeHistory, $"Office {version} {app} 文件历史",
                        fileMruKey, IsRegistryTarget: true, 0));
                }
            }
        }
    }
}

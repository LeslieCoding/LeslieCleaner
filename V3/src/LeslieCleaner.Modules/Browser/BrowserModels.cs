namespace LeslieCleaner.Modules.Browser;

/// <summary>浏览器数据类型。</summary>
public enum BrowserDataType
{
    /// <summary>页面/资源缓存。</summary>
    Cache,

    /// <summary>Cookies（支持域名白名单保留）。</summary>
    Cookies,

    /// <summary>浏览历史。</summary>
    History,

    /// <summary>下载历史。</summary>
    DownloadHistory,

    /// <summary>表单自动填充数据。</summary>
    FormData,
}

/// <summary>浏览器内核。</summary>
public enum BrowserEngineKind
{
    /// <summary>Chromium 系（User Data 结构）。</summary>
    Chromium,

    /// <summary>Firefox（profiles.ini + cache2/sqlite）。</summary>
    Firefox,
}

/// <summary>一个已发现的浏览器用户配置。</summary>
/// <param name="BrowserId">浏览器标识（chrome/edge/firefox/brave/arc/opera/vivaldi）。</param>
/// <param name="DisplayName">显示名（含配置名，如 "Chrome (Default)"）。</param>
/// <param name="Engine">内核。</param>
/// <param name="ProfileDirectory">配置目录（Chromium 为 User Data\Default 级，Firefox 为 Profiles\xxx 级）。</param>
/// <param name="CacheDirectory">缓存目录（Firefox 位于 LocalAppData 镜像路径；Chromium 为配置目录本身，缓存为其子目录）。</param>
public sealed record BrowserProfile(
    string BrowserId,
    string DisplayName,
    BrowserEngineKind Engine,
    string ProfileDirectory,
    string CacheDirectory);

/// <summary>一条浏览器扫描命中项。</summary>
/// <param name="Profile">所属配置。</param>
/// <param name="DataType">数据类型。</param>
/// <param name="Path">涉及路径（缓存目录或 sqlite 文件）。</param>
/// <param name="SizeBytes">占用字节（sqlite 类为文件大小）。</param>
/// <param name="Skipped">是否因浏览器运行中被跳过。</param>
public sealed record BrowserScanItem(
    BrowserProfile Profile,
    BrowserDataType DataType,
    string Path,
    long SizeBytes,
    bool Skipped);

/// <summary>自动清理规则（配置驱动）。</summary>
/// <param name="BrowserId">浏览器标识；<see langword="null"/> 表示全部浏览器。</param>
/// <param name="DataType">数据类型。</param>
/// <param name="IntervalDays">清理间隔天数。</param>
/// <param name="Enabled">是否启用。</param>
public sealed record BrowserAutoCleanRule(string? BrowserId, BrowserDataType DataType, int IntervalDays, bool Enabled);

/// <summary>浏览器清理结果。</summary>
/// <param name="OperationId">操作标识（回滚凭据）。</param>
/// <param name="FreedBytes">释放字节数。</param>
/// <param name="CleanedItems">已清理项数。</param>
/// <param name="SkippedBrowsers">因运行中被跳过的浏览器标识。</param>
public sealed record BrowserCleanResult(
    string OperationId,
    long FreedBytes,
    int CleanedItems,
    IReadOnlyList<string> SkippedBrowsers);

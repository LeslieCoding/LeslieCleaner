using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Modules.Browser;

/// <summary>
/// 浏览器环境：LocalAppData / RoamingAppData 两个根的解析结果。
/// 生产实例经 <see cref="FromSystem"/>（环境变量+特殊文件夹）创建；测试直接指定临时目录。
/// </summary>
/// <param name="LocalAppData">%LocalAppData% 等价目录。</param>
/// <param name="RoamingAppData">%AppData% 等价目录。</param>
public sealed record BrowserEnvironment(string LocalAppData, string RoamingAppData)
{
    /// <summary>从当前系统环境创建实例。</summary>
    public static BrowserEnvironment FromSystem() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
}

/// <summary>浏览器定义（七个目标浏览器的安装布局）。</summary>
/// <param name="Id">浏览器标识。</param>
/// <param name="DisplayName">显示名。</param>
/// <param name="Engine">内核。</param>
/// <param name="ProcessNames">进程名（运行检测用）。</param>
/// <param name="UserDataRoot">User Data / 配置根（{LOCAL} 与 {ROAMING} 占位）。</param>
/// <param name="CacheRoot">缓存根（Firefox 专用；Chromium 为 <see langword="null"/> 表示缓存在配置目录内）。</param>
public sealed record BrowserDefinition(
    string Id,
    string DisplayName,
    BrowserEngineKind Engine,
    IReadOnlyList<string> ProcessNames,
    string UserDataRoot,
    string? CacheRoot);

/// <summary>浏览器目录常量：未安装自动跳过（目录不存在即未发现）。</summary>
public static class BrowserCatalog
{
    /// <summary>默认登录域名白名单（清理 Cookies 时保留，可经配置覆盖）。</summary>
    public static IReadOnlyList<string> DefaultCookieWhitelist { get; } =
    [
        "google.com", "google.com.hk", "microsoft.com", "live.com", "office.com",
        "github.com", "apple.com", "icloud.com", "baidu.com", "qq.com",
        "weixin.qq.com", "taobao.com", "jd.com", "bilibili.com", "zhihu.com",
        "netease.com", "163.com", "126.com", "alipay.com", "steamcommunity.com",
        "steampowered.com", "epicgames.com", "openai.com", "anthropic.com",
    ];

    /// <summary>七个浏览器定义。</summary>
    public static IReadOnlyList<BrowserDefinition> All { get; } =
    [
        new("chrome", "Google Chrome", BrowserEngineKind.Chromium, ["chrome"],
            @"{LOCAL}\Google\Chrome\User Data", null),
        new("edge", "Microsoft Edge", BrowserEngineKind.Chromium, ["msedge"],
            @"{LOCAL}\Microsoft\Edge\User Data", null),
        new("brave", "Brave", BrowserEngineKind.Chromium, ["brave"],
            @"{LOCAL}\BraveSoftware\Brave-Browser\User Data", null),
        new("arc", "Arc", BrowserEngineKind.Chromium, ["Arc"],
            @"{LOCAL}\Arc\User Data", null),
        new("opera", "Opera", BrowserEngineKind.Chromium, ["opera"],
            @"{ROAMING}\Opera Software\Opera Stable", null),
        new("vivaldi", "Vivaldi", BrowserEngineKind.Chromium, ["vivaldi"],
            @"{LOCAL}\Vivaldi\User Data", null),
        new("firefox", "Mozilla Firefox", BrowserEngineKind.Firefox, ["firefox"],
            @"{ROAMING}\Mozilla\Firefox", @"{LOCAL}\Mozilla\Firefox\Profiles"),
    ];

    /// <summary>Chromium 配置目录内的缓存子目录。</summary>
    public static IReadOnlyList<string> ChromiumCacheSubdirectories { get; } = ["Cache", "Code Cache", "GPUCache"];

    /// <summary>Chromium 各数据类型的 sqlite 文件（相对配置目录）。</summary>
    public static string GetChromiumDatabasePath(BrowserDataType dataType, string profileDirectory) => dataType switch
    {
        BrowserDataType.Cookies => Path.Combine(profileDirectory, "Network", "Cookies"),
        BrowserDataType.History or BrowserDataType.DownloadHistory => Path.Combine(profileDirectory, "History"),
        BrowserDataType.FormData => Path.Combine(profileDirectory, "Web Data"),
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "非 sqlite 数据类型"),
    };

    /// <summary>Firefox 各数据类型的 sqlite 文件（相对配置目录）。</summary>
    public static string GetFirefoxDatabasePath(BrowserDataType dataType, string profileDirectory) => dataType switch
    {
        BrowserDataType.Cookies => Path.Combine(profileDirectory, "cookies.sqlite"),
        BrowserDataType.History or BrowserDataType.DownloadHistory => Path.Combine(profileDirectory, "places.sqlite"),
        BrowserDataType.FormData => Path.Combine(profileDirectory, "formhistory.sqlite"),
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "非 sqlite 数据类型"),
    };

    /// <summary>
    /// 解析定义中的 {LOCAL}/{ROAMING} 占位为实际路径。
    /// </summary>
    /// <param name="template">含占位符的路径模板。</param>
    /// <param name="environment">浏览器环境。</param>
    public static string ResolveTemplate(string template, BrowserEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(environment);
        return template
            .Replace("{LOCAL}", environment.LocalAppData, StringComparison.OrdinalIgnoreCase)
            .Replace("{ROAMING}", environment.RoamingAppData, StringComparison.OrdinalIgnoreCase);
    }
}

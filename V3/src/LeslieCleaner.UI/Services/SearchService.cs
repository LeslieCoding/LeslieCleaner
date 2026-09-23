using LeslieCleaner.Modules.Uninstaller;

namespace LeslieCleaner.UI.Services;

/// <summary>搜索结果项。</summary>
/// <param name="Kind">类别（功能/设置/软件/建议）。</param>
/// <param name="Title">标题。</param>
/// <param name="Detail">副标题。</param>
/// <param name="Payload">负载（功能=页面标题；软件=应用名）。</param>
public sealed record SearchResultItem(string Kind, string Title, string Detail, string Payload);

/// <summary>
/// 全局搜索服务（Ctrl+K）：功能模块 / 设置项 / 已安装软件（卸载发现缓存）/ AI 建议。
/// 关键词大小写不敏感子串匹配，功能与设置优先，按类别分组返回。
/// </summary>
public sealed class SearchService
{
    /// <summary>可搜索设置项（标题 → 设置页定位关键词）。</summary>
    private static readonly (string Title, string Detail)[] SettingEntries =
    [
        ("主题设置", "浅色 / 深色 / 跟随系统"),
        ("隔离区保留天数", "清理项目可还原的保留期"),
        ("维护计划", "自动维护周期配置"),
        ("开机自动启动", "登录后自动运行 LeslieCleaner"),
        ("Cookie 白名单", "浏览器隐私清理时保留的站点"),
    ];

    private readonly Func<IReadOnlyList<(string Title, string Icon)>> _pagesProvider;
    private readonly Func<IReadOnlyList<InstalledApp>> _installedAppsProvider;

    /// <summary>以页面/已装软件提供者创建搜索服务。</summary>
    public SearchService(
        Func<IReadOnlyList<(string Title, string Icon)>> pagesProvider,
        Func<IReadOnlyList<InstalledApp>> installedAppsProvider)
    {
        _pagesProvider = pagesProvider;
        _installedAppsProvider = installedAppsProvider;
    }

    /// <summary>执行搜索（空关键词返回空）。</summary>
    /// <param name="keyword">关键词。</param>
    /// <param name="maxPerKind">每类最多返回条数。</param>
    public IReadOnlyList<SearchResultItem> Search(string keyword, int maxPerKind = 5)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var results = new List<SearchResultItem>();

        results.AddRange(_pagesProvider()
            .Where(p => p.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(maxPerKind)
            .Select(p => new SearchResultItem("功能", p.Title, "打开页面", p.Title)));

        results.AddRange(SettingEntries
            .Where(s => s.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                s.Detail.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(maxPerKind)
            .Select(s => new SearchResultItem("设置", s.Title, s.Detail, "设置")));

        try
        {
            results.AddRange(_installedAppsProvider()
                .Where(a => a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(maxPerKind)
                .Select(a => new SearchResultItem("软件", a.Name, a.Publisher ?? "已安装软件", "智能卸载")));
        }
        catch (InvalidOperationException)
        {
            // 已装软件缓存未就绪时静默跳过
        }

        if ("清理垃圾建议优化加速".Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("建议", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new SearchResultItem("建议", "运行 Smart Scan 清理系统垃圾", "AI 建议", "Smart Scan"));
            results.Add(new SearchResultItem("建议", "审查高影响启动项", "AI 建议", "启动项管理"));
        }

        return results;
    }
}

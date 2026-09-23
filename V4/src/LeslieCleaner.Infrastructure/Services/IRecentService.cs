namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 最近使用项数据记录：标题、图标、最后使用时间（UTC）。
/// </summary>
public record RecentItem(string Title, string Icon, DateTimeOffset LastUsedUtc);

/// <summary>
/// 最近使用项目管理服务：记录访问、查询、清空。
/// </summary>
public interface IRecentService
{
    /// <summary>
    /// 记录某项目最近使用（标题 + 图标）。
    /// </summary>
    /// <param name="title">项目标题。</param>
    /// <param name="icon">图标标识（字符串）。若为空则跳过。</param>
    void Record(string title, string? icon);

    /// <summary>
    /// 获取最近 N 项（最新的在前）。未指定限制时返回全部。
    /// </summary>
    /// <param name="limit">最大返回条数，<c>null</c> 则不限。</param>
    /// <returns>最近的项集合，按 <see cref="RecentItem.LastUsedUtc"/> 逆向排序。</returns>
    IReadOnlyList<RecentItem> GetRecent(int? limit = null);

    /// <summary>
    /// 清空所有最近使用记录。
    /// </summary>
    void Clear();
}

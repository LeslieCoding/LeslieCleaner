namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 收藏项数据记录：标题、图标、排序序号。
/// </summary>
public record FavoriteItem(string Title, string Icon, int Order);

/// <summary>
/// 收藏管理服务：添加/移除收藏、查询收藏列表、清空所有收藏。
/// </summary>
public interface IFavoriteService
{
    /// <summary>
    /// 切换指定标题的收藏状态（已收藏则移除，未收藏则添加）。
    /// </summary>
    /// <param name="title">项目标题。</param>
    void ToggleFavorite(string title);

    /// <summary>
    /// 判断指定标题是否已收藏。
    /// </summary>
    /// <param name="title">项目标题。</param>
    /// <returns>若已收藏则返回 <c>true</c>。</returns>
    bool IsFavorite(string title);

    /// <summary>
    /// 获取按 <see cref="FavoriteItem.Order"/> 排序的收藏列表。
    /// </summary>
    /// <returns>收藏项集合。</returns>
    IReadOnlyList<FavoriteItem> GetFavorites();

    /// <summary>
    /// 清空所有收藏。
    /// </summary>
    void RemoveAll();
}

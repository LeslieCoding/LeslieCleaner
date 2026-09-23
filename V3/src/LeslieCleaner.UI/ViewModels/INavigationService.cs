namespace LeslieCleaner.UI.ViewModels;

/// <summary>页面导航服务（由 MainViewModel 实现，供页面内跳转）。</summary>
public interface INavigationService
{
    /// <summary>导航到指定标题的页面。</summary>
    /// <param name="title">导航项标题。</param>
    void NavigateTo(string title);
}

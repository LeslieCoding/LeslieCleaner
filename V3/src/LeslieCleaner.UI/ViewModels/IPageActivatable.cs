namespace LeslieCleaner.UI.ViewModels;

/// <summary>页面激活钩子：导航到页面时由 MainViewModel 调用一次（触发真实数据加载）。</summary>
public interface IPageActivatable
{
    /// <summary>页面被激活时调用（后台加载数据，禁止阻塞 UI 线程）。</summary>
    Task OnActivatedAsync();
}

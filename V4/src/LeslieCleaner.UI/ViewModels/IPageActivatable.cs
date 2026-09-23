namespace LeslieCleaner.UI.ViewModels;

/// <summary>页面激活钩子：导航到页面时由 MainViewModel 调用一次（触发真实数据加载）。</summary>
public interface IPageActivatable
{
    /// <summary>页面被激活时调用（后台加载数据，禁止阻塞 UI 线程）。</summary>
    Task OnActivatedAsync();
}

/// <summary>支持在用户快速切换页面时取消仍在进行的加载。</summary>
public interface ICancelablePageActivatable : IPageActivatable
{
    /// <summary>页面被激活时调用；离开页面后令牌会被取消。</summary>
    Task OnActivatedAsync(CancellationToken cancellationToken);
}
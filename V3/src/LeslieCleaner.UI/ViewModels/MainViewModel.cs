using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>导航项。</summary>
/// <param name="Title">标题。</param>
/// <param name="Icon">图标字符（Segoe MDL2/emoji 文本）。</param>
/// <param name="ViewModelFactory">页面视图模型工厂（惰性创建）。</param>
public sealed record NavigationItem(string Title, string Icon, Func<object> ViewModelFactory);

/// <summary>
/// 主窗口视图模型：左侧导航 + 当前页面。页面视图模型按需创建、离开即弃
/// （不驻留缓存，导航离开后 GC 可回收；重数据由服务级缓存承接）。
/// </summary>
public sealed partial class MainViewModel : ObservableObject, INavigationService
{
    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private object? _currentPage;

    /// <summary>导航项集合。</summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    /// <summary>注册导航项并默认选中第一项。</summary>
    /// <param name="items">导航项。</param>
    public void RegisterPages(IEnumerable<NavigationItem> items)
    {
        NavigationItems.Clear();
        foreach (NavigationItem item in items)
        {
            NavigationItems.Add(item);
        }

        SelectedItem = NavigationItems.FirstOrDefault();
    }

    /// <summary>按标题导航（截图验证/深链钩子）。</summary>
    /// <param name="title">导航项标题。</param>
    public void NavigateTo(string title)
    {
        NavigationItem? item = NavigationItems.FirstOrDefault(
            i => i.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            SelectedItem = item;
        }
    }

    /// <summary>选中项变化时切换页面（按需创建；离开页随引用丢弃被 GC 回收）。</summary>
    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is null)
        {
            return;
        }

        CurrentPage = value.ViewModelFactory();
        if (CurrentPage is IPageActivatable activatable)
        {
            _ = activatable.OnActivatedAsync();
        }
    }

    /// <summary>返回首页。</summary>
    [RelayCommand]
    private void GoHome() => SelectedItem = NavigationItems.FirstOrDefault();
}

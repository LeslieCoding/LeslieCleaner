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
/// 主窗口视图模型：左侧导航 + 当前页面。页面视图模型按需创建、离开即弃；
/// 页面激活可取消、错误可观察，避免未观察任务导致界面无响应或静默失败。
/// </summary>
public sealed partial class MainViewModel : ObservableObject, INavigationService
{
    private CancellationTokenSource? _activationCts;
    private long _activationGeneration;

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isNavigationBusy;

    [ObservableProperty]
    private string _navigationStatus = "就绪";

    [ObservableProperty]
    private string? _navigationError;

    /// <summary>导航项集合。</summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    /// <summary>最近一次页面激活任务，供宿主和自动化测试观察失败。</summary>
    public Task LastActivationTask { get; private set; } = Task.CompletedTask;

    /// <summary>注册导航项并默认选中第一项。</summary>
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
    public void NavigateTo(string title)
    {
        NavigationItem? item = NavigationItems.FirstOrDefault(
            i => i.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            SelectedItem = item;
        }
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is null)
        {
            return;
        }

        _activationCts?.Cancel();
        _activationCts?.Dispose();
        _activationCts = new CancellationTokenSource();
        CurrentPage = value.ViewModelFactory();
        long generation = Interlocked.Increment(ref _activationGeneration);
        LastActivationTask = ActivateCurrentPageAsync(value.Title, CurrentPage, _activationCts.Token, generation);
    }

    private async Task ActivateCurrentPageAsync(string title, object? page, CancellationToken cancellationToken, long generation)
    {
        NavigationError = null;
        IsNavigationBusy = page is IPageActivatable;
        NavigationStatus = IsNavigationBusy ? $"正在加载{title}…" : $"已打开{title}";
        try
        {
            await Task.Yield();
            if (page is ICancelablePageActivatable cancelable)
            {
                await cancelable.OnActivatedAsync(cancellationToken);
            }
            else if (page is IPageActivatable activatable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await activatable.OnActivatedAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (generation == Volatile.Read(ref _activationGeneration))
            {
                NavigationStatus = $"{title}已就绪";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (generation == Volatile.Read(ref _activationGeneration))
            {
                NavigationStatus = $"已停止加载{title}";
            }
        }
        catch (Exception ex)
        {
            NavigationError = ex.Message;
            NavigationStatus = $"{title}加载失败，可切换页面后重试";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested && generation == Volatile.Read(ref _activationGeneration))
            {
                IsNavigationBusy = false;
            }
        }
    }

    /// <summary>返回首页。</summary>
    [RelayCommand]
    private void GoHome() => SelectedItem = NavigationItems.FirstOrDefault();
}
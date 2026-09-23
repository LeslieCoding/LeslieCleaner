using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.Theming;
using LeslieCleaner.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.App;

/// <summary>
/// 主窗口：导航装配（收藏星标/右键）、确认对话框宿主（免打扰）、
/// Ctrl+K 全局搜索弹层、通知中心（铃铛+未读角标+撤销）、Mica、启动钩子。
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _themeService;
    private readonly NotificationService _notificationService;
    private readonly SearchService _searchService;
    private TaskCompletionSource<bool>? _dialogCompletion;

    /// <summary>以 DI 容器创建主窗口。</summary>
    /// <param name="services">服务容器。</param>
    public MainWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        var mainViewModel = services.GetRequiredService<MainViewModel>();
        DataContext = mainViewModel;
        mainViewModel.RegisterPages(BuildNavigationItems());

        _notificationService = services.GetRequiredService<NotificationService>();
        _notificationService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NotificationService.UnreadCount))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UnreadBadge.Visibility = _notificationService.UnreadCount > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    UnreadBadgeText.Text = _notificationService.UnreadCount > 99
                        ? "99+"
                        : _notificationService.UnreadCount.ToString();
                });
            }
        };
        NotificationList.ItemsSource = _notificationService.Notifications;
        _searchService = services.GetRequiredService<SearchService>();

        _themeService = services.GetRequiredService<ThemeService>();
        _themeService.ThemeApplied += (_, _) => ApplyWindowChrome();

        var dialogService = services.GetRequiredService<DialogService>();
        dialogService.ConfirmHandler = ShowConfirmAsync;

        if (ElevationHelper.IsElevated)
        {
            AdminRestartButton.Visibility = Visibility.Collapsed;
        }

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (_, _) => OpenSearchOverlay()));
        PreviewKeyDown += OnWindowPreviewKeyDown;
        SourceInitialized += (_, _) => ApplyWindowChrome();
    }

    /// <summary>应用启动钩子（--page / --theme / --autoscan）。</summary>
    public void ApplyStartupHooks()
    {
        if (Program.StartupThemeOverride is { } themeOverride)
        {
            ThemeMode mode = themeOverride.Equals("dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeMode.Dark
                : themeOverride.Equals("light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeMode.Light
                    : ThemeMode.Auto;
            _ = _themeService.SetModeAsync(mode);
        }

        var mainViewModel = (MainViewModel)DataContext;
        if (Program.StartupPage is { } page)
        {
            mainViewModel.NavigateTo(page);
        }

        if (Program.StartupItemId is { } startupItemId)
        {
            _ = RestoreStartupItemSelectionAsync(mainViewModel, startupItemId);
        }

        if (Program.RegistryResumeToken is { } registryResumeToken)
        {
            _ = RestoreRegistrySelectionAsync(mainViewModel, registryResumeToken);
        }

        if (Program.AutoScan && mainViewModel.CurrentPage is SmartScanViewModel smartScan)
        {
            _ = smartScan.StartScanCommand.ExecuteAsync(null);
        }
        else if (Program.AutoScan)
        {
            mainViewModel.NavigateTo("Smart Scan");
            if (mainViewModel.CurrentPage is SmartScanViewModel scan)
            {
                _ = scan.StartScanCommand.ExecuteAsync(null);
            }
        }
    }

    private void ApplyWindowChrome()
    {
        if (_themeService.MicaEnabled)
        {
            MicaHelper.Apply(this, _themeService.IsDark);
        }
    }

    /// <summary>等待启动项页完成真实枚举后，再恢复提权前的目标定位。</summary>
    private static async Task RestoreStartupItemSelectionAsync(MainViewModel mainViewModel, string startupItemId)
    {
        await mainViewModel.LastActivationTask;
        if (mainViewModel.CurrentPage is StartupViewModel startup)
        {
            startup.RestoreSelection(startupItemId);
        }
    }

    private static async Task RestoreRegistrySelectionAsync(MainViewModel mainViewModel, string token)
    {
        await mainViewModel.LastActivationTask;
        if (mainViewModel.CurrentPage is RegistryViewModel registry)
        {
            await registry.ResumeAfterElevationAsync(token);
        }
    }

    private IEnumerable<NavigationItem> BuildNavigationItems()
    {
        yield return new NavigationItem("首页", "home", () => _services.GetRequiredService<DashboardViewModel>());
        yield return new NavigationItem("Smart Scan", "zap", () => _services.GetRequiredService<SmartScanViewModel>());
        yield return new NavigationItem("注册表清理", "shield", () => _services.GetRequiredService<RegistryViewModel>());
        yield return new NavigationItem("智能卸载", "trash", () => _services.GetRequiredService<UninstallerViewModel>());
        yield return new NavigationItem("启动项管理", "rocket", () => _services.GetRequiredService<StartupViewModel>());
        yield return new NavigationItem("隐私保护", "lock", () => _services.GetRequiredService<PrivacyViewModel>());
        yield return new NavigationItem("文件粉碎", "scissors", () => _services.GetRequiredService<ShredderViewModel>());
        yield return new NavigationItem("重复文件查找", "copy", () => _services.GetRequiredService<DuplicatesViewModel>());
        yield return new NavigationItem("大文件分析", "chart", () => _services.GetRequiredService<LargeFilesViewModel>());
        yield return new NavigationItem("工具箱", "toolbox", () => _services.GetRequiredService<ToolboxViewModel>());
        yield return new NavigationItem("平台中心", "cloud", () => _services.GetRequiredService<PlatformCenterViewModel>());
        yield return new NavigationItem("运行日志", "file", () => _services.GetRequiredService<LogsViewModel>());
        yield return new NavigationItem("历史记录", "clock", () => _services.GetRequiredService<HistoryViewModel>());
        yield return new NavigationItem("设置", "settings", () => _services.GetRequiredService<SettingsViewModel>());
    }

    private void OnNavItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { DataContext: NavigationItem item } radioButton &&
            DataContext is MainViewModel mainViewModel)
        {
            radioButton.IsChecked = true;
            mainViewModel.SelectedItem = item;
        }
    }

    private void OnAdminRestartClick(object sender, RoutedEventArgs e)
    {
        string? currentPage = (DataContext as MainViewModel)?.SelectedItem?.Title;
        bool started = string.IsNullOrWhiteSpace(currentPage)
            ? ElevationHelper.RestartAsAdmin()
            : ElevationHelper.RestartAsAdmin("--page", currentPage);
        if (!started)
        {
            MessageBox.Show(
                "未启动管理员实例。若你取消了 UAC，可以稍后再次点击“管理员模式”。",
                "管理员模式", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateTo("设置");
        }
    }

    // ============ 通知中心 ============

    private void OnNotificationBellClick(object sender, RoutedEventArgs e)
    {
        NotificationPanel.Visibility = NotificationPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnMarkAllReadClick(object sender, RoutedEventArgs e) => _notificationService.MarkAllRead();

    // ============ 全局搜索（Ctrl+K） ============

    private void OnTopSearchFocus(object sender, RoutedEventArgs e) => OpenSearchOverlay();

    private void OpenSearchOverlay()
    {
        SearchOverlay.Visibility = Visibility.Visible;
        SearchInput.Text = string.Empty;
        SearchResults.ItemsSource = null;
        SearchInput.Focus();
    }

    private void CloseSearchOverlay() => SearchOverlay.Visibility = Visibility.Collapsed;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        IReadOnlyList<SearchResultItem> results = _searchService.Search(SearchInput.Text);
        SearchResults.ItemsSource = results;
        SearchResults.SelectedIndex = results.Count > 0 ? 0 : -1;
    }

    private void OnSearchInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (SearchResults.Items.Count > 0)
                {
                    SearchResults.SelectedIndex = Math.Min(SearchResults.SelectedIndex + 1, SearchResults.Items.Count - 1);
                    SearchResults.ScrollIntoView(SearchResults.SelectedItem);
                }

                e.Handled = true;
                break;
            case Key.Up:
                if (SearchResults.Items.Count > 0)
                {
                    SearchResults.SelectedIndex = Math.Max(SearchResults.SelectedIndex - 1, 0);
                    SearchResults.ScrollIntoView(SearchResults.SelectedItem);
                }

                e.Handled = true;
                break;
            case Key.Enter:
                ActivateSearchResult();
                e.Handled = true;
                break;
            case Key.Escape:
                CloseSearchOverlay();
                e.Handled = true;
                break;
        }
    }

    private void OnSearchResultActivate(object sender, MouseButtonEventArgs e) => ActivateSearchResult();

    private void OnSearchOverlayBackdrop(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            CloseSearchOverlay();
        }
    }

    private void ActivateSearchResult()
    {
        if (SearchResults.SelectedItem is not SearchResultItem item ||
            DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        CloseSearchOverlay();
        mainViewModel.NavigateTo(item.Payload);
    }

    // ============ 确认对话框（免打扰） ============

    private Task<bool> ShowConfirmAsync(string title, string message, string confirmText)
    {
        _dialogCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogConfirmButton.Content = confirmText;
        DialogOverlay.Visibility = Visibility.Visible;
        return _dialogCompletion.Task;
    }

    private void OnDialogConfirm(object sender, RoutedEventArgs e) => CloseDialog(true);

    private void OnDialogCancel(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void CloseDialog(bool result)
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
        _dialogCompletion?.TrySetResult(result);
        _dialogCompletion = null;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (SearchOverlay.Visibility == Visibility.Visible)
        {
            CloseSearchOverlay();
            e.Handled = true;
        }
        else if (NotificationPanel.Visibility == Visibility.Visible)
        {
            NotificationPanel.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (DialogOverlay.Visibility == Visibility.Visible)
        {
            CloseDialog(false);
            e.Handled = true;
        }
    }
}

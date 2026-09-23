using System.Windows;
using System.Windows.Controls;

namespace LeslieCleaner.UI.Views;

/// <summary>启动项管理页（视图，逻辑在 StartupViewModel）。</summary>
public partial class StartupPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public StartupPage() => InitializeComponent();

    private void OnCategoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { DataContext: ViewModels.StartupCategoryNode node } &&
            DataContext is ViewModels.StartupViewModel viewModel)
        {
            viewModel.SelectedCategory = node;
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.StartupViewModel viewModel)
        {
            BatchHeader.HandleSelectAllClick(sender, e, viewModel.Selection);
        }
    }
}

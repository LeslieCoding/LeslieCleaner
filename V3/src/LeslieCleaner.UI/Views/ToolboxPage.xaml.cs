using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>ToolboxPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class ToolboxPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public ToolboxPage() => InitializeComponent();

    private void OnQuarantineSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((ToolboxViewModel)DataContext).QuarantineSelection);

    private void OnServiceSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((ToolboxViewModel)DataContext).ServiceSelection);
}


using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>UninstallerPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class UninstallerPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public UninstallerPage() => InitializeComponent();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((UninstallerViewModel)DataContext).Selection);

    private void OnResidualSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((UninstallerViewModel)DataContext).ResidualSelection);
}


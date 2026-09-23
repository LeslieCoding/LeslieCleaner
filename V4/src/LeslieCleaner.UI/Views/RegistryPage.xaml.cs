using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>RegistryPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class RegistryPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public RegistryPage() => InitializeComponent();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((RegistryViewModel)DataContext).Selection);
}


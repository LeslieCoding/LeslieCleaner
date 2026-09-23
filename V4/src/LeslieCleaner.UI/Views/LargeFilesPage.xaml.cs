using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>LargeFilesPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class LargeFilesPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public LargeFilesPage() => InitializeComponent();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((LargeFilesViewModel)DataContext).Selection);
}


using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>DuplicatesPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class DuplicatesPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public DuplicatesPage() => InitializeComponent();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((DuplicatesViewModel)DataContext).Selection);
}


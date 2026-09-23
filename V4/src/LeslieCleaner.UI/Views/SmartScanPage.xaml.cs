using System.Windows;
using System.Windows.Controls;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.UI.Views;

/// <summary>SmartScanPage（视图，逻辑在对应 ViewModel）。</summary>
public partial class SmartScanPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public SmartScanPage() => InitializeComponent();

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        BatchHeader.HandleSelectAllClick(sender, e, ((SmartScanViewModel)DataContext).Selection);
}


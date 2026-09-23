using System.Windows;
using System.Windows.Controls;

namespace LeslieCleaner.UI.Views;

/// <summary>文件粉碎页（拖拽接收）。</summary>
public partial class ShredderPage : UserControl
{
    /// <summary>初始化组件。</summary>
    public ShredderPage() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths &&
            DataContext is ViewModels.ShredderViewModel viewModel)
        {
            viewModel.AddPathsCommand.Execute(paths);
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ShredderViewModel viewModel)
        {
            BatchHeader.HandleSelectAllClick(sender, e, viewModel.Selection);
        }
    }
}

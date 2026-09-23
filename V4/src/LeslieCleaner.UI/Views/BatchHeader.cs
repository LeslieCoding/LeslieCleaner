using System.Windows;
using LeslieCleaner.UI.Services;

namespace LeslieCleaner.UI.Views;

/// <summary>
/// 批量选择头部共享事件处理（各页代码后置一行转发到此，避免每个页面重复三态点击逻辑）。
/// </summary>
internal static class BatchHeader
{
    /// <summary>
    /// 三态全选 CheckBox 点击：阻止默认循环，按"非全选→全选，全选→全不选"驱动助手。
    /// </summary>
    /// <param name="sender">头部 CheckBox。</param>
    /// <param name="e">路由事件参数（标记 Handled）。</param>
    /// <param name="selection">页面批量选择助手。</param>
    public static void HandleSelectAllClick(object sender, RoutedEventArgs e, BatchSelectionHelper selection)
    {
        e.Handled = true;
        selection.ToggleAll();
    }
}

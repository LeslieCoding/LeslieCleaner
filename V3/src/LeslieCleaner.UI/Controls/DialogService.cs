using System.Windows;

namespace LeslieCleaner.UI.Controls;

/// <summary>
/// 确认对话框服务：主窗口中央遮罩卡片，异步等待用户选择。
/// 由 MainWindow 注册宿主编排器（<see cref="ConfirmHandler"/>），
/// ViewModel 仅调用 <see cref="ConfirmAsync"/>，不接触视觉树。
/// </summary>
public sealed class DialogService
{
    /// <summary>宿主编排委托：返回用户是否确认。</summary>
    public Func<string, string, string, Task<bool>>? ConfirmHandler { get; set; }

    /// <summary>
    /// 弹出确认对话框。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <param name="confirmText">确认按钮文本（默认"确认"）。</param>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "确认")
    {
        if (ConfirmHandler is null)
        {
            return false;
        }

        return await Application.Current.Dispatcher.InvokeAsync(
            () => ConfirmHandler(title, message, confirmText)).Task.Unwrap();
    }
}

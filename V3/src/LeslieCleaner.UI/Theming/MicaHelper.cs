using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LeslieCleaner.UI.Theming;

/// <summary>
/// Mica/亚克力背景助手。Win11 经 DwmSetWindowAttribute 启用
/// SYSTEMBACKDROP_TYPE（优先 Mica），失败降级纯色（异常静默降级，不影响渲染）。
/// </summary>
public static class MicaHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtMica = 2;

    /// <summary>
    /// 对窗口应用 Mica 背景与主题联动标题栏。
    /// </summary>
    /// <param name="window">目标窗口（需已 SourceInitialized）。</param>
    /// <param name="dark">是否深色模式（标题栏联动）。</param>
    public static void Apply(Window window, bool dark)
    {
        ArgumentNullException.ThrowIfNull(window);
        try
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int darkMode = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
            int backdrop = DwmsbtMica;
            _ = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // 静默降级纯色背景，不影响渲染（静态助手无日志通道，DWM 失败无需上报）。
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int valueSize);
}

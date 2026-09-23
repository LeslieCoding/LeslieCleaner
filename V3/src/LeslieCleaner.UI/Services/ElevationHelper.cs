using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace LeslieCleaner.UI.Services;

/// <summary>
/// 权限助手：管理员检测与"以管理员身份重新启动"（ShellExecute runas）。
/// 注册表 HKCR/HKLM、服务、计划任务等操作需要管理员权限——asInvoker 下
/// 相关操作会诚实报权限不足，引导用户经此入口提权重启（UAC 弹窗属正常）。
/// </summary>
public static class ElevationHelper
{
    /// <summary>当前进程是否以管理员身份运行。</summary>
    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>以管理员身份重启自身（UAC 同意后当前实例退出）。</summary>
    public static void RestartAsAdmin()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = processPath,
            Verb = "runas",
            UseShellExecute = true,
        });
        Application.Current.Shutdown();
    }
}

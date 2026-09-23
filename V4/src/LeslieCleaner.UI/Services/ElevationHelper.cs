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
    private static readonly IReadOnlyDictionary<string, int> OneShotArgumentValueCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["--page"] = 1,
            [StartupResumeTarget.ArgumentName] = 1,
            ["--registry-resume"] = 1,
            ["--autoscan"] = 0,
            ["--provision-integrity-baseline"] = 0,
        };

    /// <summary>当前进程是否以管理员身份运行。</summary>
    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// 以管理员身份重启自身（UAC 同意后当前实例退出）。保留主题等持久参数，
    /// 丢弃自动扫描、旧恢复令牌和安装期基线等一次性参数，再追加新的页面定位参数。
    /// </summary>
    /// <param name="additionalArguments">追加到原命令行末尾的参数。</param>
    /// <returns>成功启动管理员实例时为 <see langword="true"/>；用户取消 UAC 或启动失败时为 false。</returns>
    public static bool RestartAsAdmin(params string[] additionalArguments)
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Verb = "runas",
            UseShellExecute = true,
        };
        foreach (string argument in BuildRestartArguments(
                     Environment.GetCommandLineArgs().Skip(1), additionalArguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (Process.Start(startInfo) is null)
            {
                return false;
            }

            Application.Current.Shutdown();
            return true;
        }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED：用户取消 UAC，不应导致应用崩溃或产生假重启状态。
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 合并提权重启参数。一次性动作不会跨进程重放，避免自动扫描抢走恢复页面，
    /// 也避免安装期完整性基线授权被普通管理员重启意外复用。
    /// </summary>
    public static IReadOnlyList<string> BuildRestartArguments(
        IEnumerable<string> currentArguments,
        IEnumerable<string> additionalArguments)
    {
        ArgumentNullException.ThrowIfNull(currentArguments);
        ArgumentNullException.ThrowIfNull(additionalArguments);
        string[] current = currentArguments.ToArray();
        var merged = new List<string>(current.Length);
        for (int index = 0; index < current.Length; index++)
        {
            string argument = current[index];
            if (OneShotArgumentValueCounts.TryGetValue(argument, out int valueCount))
            {
                index = Math.Min(current.Length, index + valueCount);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(argument))
            {
                merged.Add(argument);
            }
        }

        merged.AddRange(additionalArguments.Where(static argument => !string.IsNullOrWhiteSpace(argument)));
        return merged;
    }
}

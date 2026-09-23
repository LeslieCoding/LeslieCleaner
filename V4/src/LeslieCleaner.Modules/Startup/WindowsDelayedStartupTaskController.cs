using System.ComponentModel;
using System.Diagnostics;
using LeslieCleaner.Core.Diagnostics;

namespace LeslieCleaner.Modules.Startup;

/// <summary>
/// 使用 Windows Task Scheduler 创建登录延迟任务。所有参数均通过
/// <see cref="ProcessStartInfo.ArgumentList"/> 传递，不调用 shell。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsDelayedStartupTaskController : IDelayedStartupTaskController
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task CreateAsync(DelayedStartupTaskSpec task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        StartupCommandSafety.ValidateTask(task);
        int totalSeconds = checked((int)task.Delay.TotalSeconds);
        string delay = FormattableString.Invariant($"{totalSeconds / 60:0000}:{totalSeconds % 60:00}");
        string action = string.IsNullOrWhiteSpace(task.Arguments)
            ? $"\"{task.ExecutablePath}\""
            : $"\"{task.ExecutablePath}\" {task.Arguments}";

        return RunAsync(
            ["/create", "/f", "/sc", "onlogon", "/delay", delay, "/rl", "limited",
                "/tn", task.TaskName, "/tr", action],
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string taskName, CancellationToken cancellationToken)
    {
        StartupCommandSafety.ValidateTaskName(taskName);
        if (!await ExistsAsync(taskName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await RunAsync(["/delete", "/f", "/tn", taskName], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string taskName, CancellationToken cancellationToken)
    {
        StartupCommandSafety.ValidateTaskName(taskName);
        ProcessResult result = await RunProcessAsync(["/query", "/tn", taskName], cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    private static async Task RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunProcessAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            throw new LeslieCleanerException(
                $"schtasks 执行失败（退出码 {result.ExitCode}）: {detail.Trim()}");
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.Default,
            StandardErrorEncoding = System.Text.Encoding.Default,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new LeslieCleanerException("无法启动 schtasks.exe");
            }
        }
        catch (Win32Exception ex)
        {
            throw new LeslieCleanerException("无法启动 schtasks.exe", ex);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}

/// <summary>启动命令解析与防注入规则。</summary>
public static class StartupCommandSafety
{
    private static readonly char[] ControlCharacters = ['\0', '\r', '\n'];

    /// <summary>把启动项命令解析为安全的绝对程序路径和参数。</summary>
    /// <param name="command">原启动命令。</param>
    /// <returns>规范化路径与参数。</returns>
    public static (string ExecutablePath, string Arguments) Parse(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (command.IndexOfAny(ControlCharacters) >= 0)
        {
            throw new ArgumentException("启动命令包含控制字符。", nameof(command));
        }

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        string executable;
        string arguments;
        if (expanded[0] == '"')
        {
            int closingQuote = expanded.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                throw new ArgumentException("启动命令的引号不完整。", nameof(command));
            }

            executable = expanded[1..closingQuote];
            arguments = expanded[(closingQuote + 1)..].Trim();
        }
        else
        {
            int executableEnd = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (executableEnd < 0)
            {
                throw new ArgumentException("仅允许明确的 .exe 启动命令。", nameof(command));
            }

            executableEnd += 4;
            executable = expanded[..executableEnd].Trim();
            arguments = expanded[executableEnd..].Trim();
        }

        if (!Path.IsPathFullyQualified(executable))
        {
            throw new ArgumentException("启动程序必须使用绝对路径。", nameof(command));
        }

        string normalized = Path.GetFullPath(executable);
        if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOfAny(ControlCharacters) >= 0 ||
            arguments.IndexOfAny(ControlCharacters) >= 0)
        {
            throw new ArgumentException("启动命令未通过路径与参数安全校验。", nameof(command));
        }

        return (normalized, arguments);
    }

    /// <summary>验证任务规范。</summary>
    public static void ValidateTask(DelayedStartupTaskSpec task)
    {
        ValidateTaskName(task.TaskName);
        _ = Parse(string.IsNullOrEmpty(task.Arguments)
            ? $"\"{task.ExecutablePath}\""
            : $"\"{task.ExecutablePath}\" {task.Arguments}");
        if (task.Delay < TimeSpan.FromSeconds(30) || task.Delay > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(task), "延迟必须在 30 秒至 5 分钟之间。");
        }
    }

    /// <summary>验证由本模块生成的任务名。</summary>
    public static void ValidateTaskName(string taskName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        if (!taskName.StartsWith(@"\LeslieCleaner\Delayed\", StringComparison.Ordinal) ||
            taskName.IndexOfAny(ControlCharacters) >= 0 || taskName.Length > 200)
        {
            throw new ArgumentException("任务名不在 LeslieCleaner 受控命名空间。", nameof(taskName));
        }
    }
}

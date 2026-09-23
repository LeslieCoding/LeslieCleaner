using System.Diagnostics;

namespace LeslieCleaner.Updater;

/// <summary>
/// Updater 辅助进程：等待主程序退出 → 用暂存文件替换安装目录 → 重启主程序。
/// 参数：--pid &lt;主程序PID&gt; --source &lt;暂存目录&gt; --target &lt;安装目录&gt; --restart &lt;主程序路径&gt;
/// 替换失败：不覆盖原文件（已替换的保持，日志记录），返回非零退出码。
/// </summary>
public static class Program
{
    /// <summary>入口。</summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>退出码（0 成功）。</returns>
    public static int Main(string[] args)
    {
        string? source = GetArg(args, "--source");
        string? target = GetArg(args, "--target");
        string? restart = GetArg(args, "--restart");
        int pid = int.TryParse(GetArg(args, "--pid"), out int parsed) ? parsed : 0;

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            Console.Error.WriteLine("缺少 --source/--target 参数");
            return 2;
        }

        try
        {
            WaitForProcessExit(pid, TimeSpan.FromMinutes(5));
            int replaced = ReplaceFiles(source, target);
            Console.WriteLine($"已替换 {replaced} 个文件");

            if (!string.IsNullOrWhiteSpace(restart))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = restart,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(restart) ?? target,
                });
            }

            try
            {
                Directory.Delete(source, recursive: true);
            }
            catch (IOException)
            {
                // 暂存目录清理失败不影响更新结果
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        {
            Console.Error.WriteLine($"更新失败: {ex.Message}");
            return 1;
        }
    }

    /// <summary>等待指定进程退出（带超时）。</summary>
    /// <param name="pid">进程 ID（0 表示不等待）。</param>
    /// <param name="timeout">超时。</param>
    public static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        if (pid <= 0)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(pid);
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                throw new TimeoutException($"等待主程序 {pid} 退出超时（{timeout.TotalMinutes} 分钟）");
            }
        }
        catch (ArgumentException)
        {
            // 进程已不存在：直接继续
        }
    }

    /// <summary>用暂存文件替换目标目录（按清单相对路径逐一覆盖）。</summary>
    /// <param name="sourceDirectory">暂存目录。</param>
    /// <param name="targetDirectory">安装目录。</param>
    /// <returns>替换的文件数。</returns>
    public static int ReplaceFiles(string sourceDirectory, string targetDirectory)
    {
        int replaced = 0;
        foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, sourceFile);
            if (relative.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destination = Path.Combine(targetDirectory, relative);
            string? destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // 先复制到临时名再原子替换，避免覆盖被占用文件时留下半截
            string tempDestination = destination + ".update-tmp";
            File.Copy(sourceFile, tempDestination, overwrite: true);
            File.Move(tempDestination, destination, overwrite: true);
            replaced++;
        }

        return replaced;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}

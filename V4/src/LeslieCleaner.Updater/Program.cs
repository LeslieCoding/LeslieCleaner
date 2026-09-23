using System.Diagnostics;

namespace LeslieCleaner.Updater;

/// <summary>Applies staged update files transactionally after the main process exits.</summary>
public static class Program
{
    /// <summary>Application entry point.</summary>
    /// <param name="args">Updater command-line arguments.</param>
    /// <returns>Zero on success; a nonzero error code otherwise.</returns>
    public static int Main(string[] args)
    {
        string? source = GetArg(args, "--source");
        string? target = GetArg(args, "--target");
        string? restart = GetArg(args, "--restart");
        int pid = int.TryParse(GetArg(args, "--pid"), out int parsed) ? parsed : 0;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            Console.Error.WriteLine("Missing --source or --target argument.");
            return 2;
        }

        try
        {
            WaitForProcessExit(pid, TimeSpan.FromMinutes(5));
            int replaced = ReplaceFiles(source, target);
            Console.WriteLine($"Replaced {replaced} files.");
            if (!string.IsNullOrWhiteSpace(restart))
            {
                Process.Start(new ProcessStartInfo { FileName = restart, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(restart) ?? target });
            }
            TryDeleteDirectory(source);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException or ArgumentException)
        {
            Console.Error.WriteLine($"Update failed: {exception.Message}");
            return 1;
        }
    }

    /// <summary>Waits for the supplied process identifier to exit.</summary>
    /// <param name="pid">Process identifier, or zero to skip waiting.</param>
    /// <param name="timeout">Maximum wait time.</param>
    public static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        if (pid <= 0) return;
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (!process.WaitForExit((int)timeout.TotalMilliseconds)) throw new TimeoutException($"Timed out waiting for process {pid}.");
        }
        catch (ArgumentException) { }
    }

    /// <summary>Replaces all staged files as a group and rolls back any partial update.</summary>
    /// <param name="sourceDirectory">Verified staging directory.</param>
    /// <param name="targetDirectory">Installation directory.</param>
    /// <returns>Number of committed files.</returns>
    public static int ReplaceFiles(string sourceDirectory, string targetDirectory) => ReplaceFiles(sourceDirectory, targetDirectory, null);

    /// <summary>Testable transactional replacement overload.</summary>
    /// <param name="sourceDirectory">Verified staging directory.</param>
    /// <param name="targetDirectory">Installation directory.</param>
    /// <param name="afterReplace">Optional callback invoked after each replacement.</param>
    /// <returns>Number of committed files.</returns>
    public static int ReplaceFiles(string sourceDirectory, string targetDirectory, Action<string, int>? afterReplace)
    {
        string sourceRoot = NormalizeDirectory(sourceDirectory);
        string targetRoot = NormalizeDirectory(targetDirectory);
        EnsureSeparateRoots(sourceRoot, targetRoot);
        RejectReparsePoint(sourceRoot);
        Directory.CreateDirectory(targetRoot);
        RejectReparsePoint(targetRoot);
        string[] sourceFiles = EnumerateSourceFiles(sourceRoot)
            .Where(file => !Path.GetRelativePath(sourceRoot, file).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var replacements = new List<Replacement>(sourceFiles.Length);
        string backupRoot = Path.Combine(targetRoot, $".update-backup-{Guid.NewGuid():N}");
        try
        {
            foreach (string sourceFile in sourceFiles)
            {
                RejectReparsePoint(sourceFile);
                string relative = Path.GetRelativePath(sourceRoot, sourceFile);
                string destination = ResolveContainedPath(targetRoot, relative);
                bool existed = File.Exists(destination);
                string? backup = null;
                if (existed)
                {
                    backup = ResolveContainedPath(NormalizeDirectory(backupRoot), relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, true);
                }
                replacements.Add(new Replacement(destination, backup, existed));
            }
            int replaced = 0;
            for (int index = 0; index < sourceFiles.Length; index++)
            {
                Replacement replacement = replacements[index];
                Directory.CreateDirectory(Path.GetDirectoryName(replacement.Destination)!);
                string temporary = replacement.Destination + $".update-tmp-{Guid.NewGuid():N}";
                try
                {
                    File.Copy(sourceFiles[index], temporary, true);
                    File.Move(temporary, replacement.Destination, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                replaced++;
                afterReplace?.Invoke(replacement.Destination, replaced);
            }
            return replaced;
        }
        catch
        {
            foreach (Replacement replacement in replacements.AsEnumerable().Reverse())
            {
                if (replacement.Existed && replacement.Backup is not null && File.Exists(replacement.Backup))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(replacement.Destination)!);
                    File.Copy(replacement.Backup, replacement.Destination, true);
                }
                else if (!replacement.Existed && File.Exists(replacement.Destination)) File.Delete(replacement.Destination);
            }
            throw;
        }
        finally { TryDeleteDirectory(backupRoot); }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            RejectReparsePoint(directory);
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectReparsePoint(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else if (File.Exists(entry)) yield return entry;
            }
        }
    }

    private static string NormalizeDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
    private static string ResolveContainedPath(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update path escapes its permitted root.");
        return path;
    }
    private static void EnsureSeparateRoots(string source, string target)
    {
        if (source.StartsWith(target, StringComparison.OrdinalIgnoreCase) || target.StartsWith(source, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update source and target directories must not overlap.");
    }
    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Update paths cannot be reparse points.");
    }
    private static void TryDeleteDirectory(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static string? GetArg(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++) if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
        return null;
    }
    private sealed record Replacement(string Destination, string? Backup, bool Existed);
}

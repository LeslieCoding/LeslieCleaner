using System.Xml.Linq;
using Microsoft.Win32;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Collects targeted service, startup, and scheduled-task ownership evidence for an installed application.</summary>
public interface IApplicationResourceInventory
{
    /// <summary>Collects resources whose executable path is contained by the application's install directory.</summary>
    Task<IReadOnlyList<FingerprintEntry>> CollectAsync(
        InstalledApp application,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Windows inventory that reads service ImagePath, registry Run values, and Task Scheduler XML.
/// It never deletes or disables resources and does not use name-only matching.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsApplicationResourceInventory : IApplicationResourceInventory
{
    private static readonly (RegistryHive Hive, RegistryView View, string Key)[] RunLocations =
    [
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<FingerprintEntry>> CollectAsync(
        InstalledApp application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (string.IsNullOrWhiteSpace(application.InstallLocation) ||
            !Path.IsPathFullyQualified(application.InstallLocation))
        {
            return Task.FromResult<IReadOnlyList<FingerprintEntry>>([]);
        }

        string installRoot = Path.GetFullPath(application.InstallLocation);
        return Task.Run<IReadOnlyList<FingerprintEntry>>(() =>
        {
            var entries = new List<FingerprintEntry>();
            CollectServices(installRoot, entries, cancellationToken);
            CollectRunEntries(installRoot, entries, cancellationToken);
            CollectScheduledTasks(installRoot, entries, cancellationToken);
            return [.. entries.DistinctBy(
                static entry => $"{entry.Type}:{entry.Value}", StringComparer.OrdinalIgnoreCase)];
        }, cancellationToken);
    }

    /// <summary>Returns true only when the executable from a command line is inside the install directory boundary.</summary>
    public static bool CommandBelongsToApplication(string? commandLine, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(installDirectory) ||
            !Path.IsPathFullyQualified(installDirectory))
        {
            return false;
        }

        string expanded = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        string executable = ExtractExecutable(expanded);
        if (!Path.IsPathFullyQualified(executable))
        {
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory)) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(executable);
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static void CollectServices(
        string installRoot,
        ICollection<FingerprintEntry> entries,
        CancellationToken cancellationToken)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? services = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", writable: false);
            if (services is null)
            {
                return;
            }

            foreach (string name in services.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using RegistryKey? service = services.OpenSubKey(name, writable: false);
                    string? imagePath = service?.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (CommandBelongsToApplication(imagePath, installRoot))
                    {
                        entries.Add(new FingerprintEntry(
                            FingerprintResourceType.Service, name, 95,
                            "Service ImagePath is inside the application install directory"));
                    }
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                {
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
        }
    }

    private static void CollectRunEntries(
        string installRoot,
        ICollection<FingerprintEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach ((RegistryHive hive, RegistryView view, string keyPath) in RunLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? key = baseKey.OpenSubKey(keyPath, writable: false);
                if (key is null)
                {
                    continue;
                }

                foreach (string valueName in key.GetValueNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string? command = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (CommandBelongsToApplication(command, installRoot))
                    {
                        entries.Add(new FingerprintEntry(
                            FingerprintResourceType.Startup,
                            $"{hive}:{view}:{keyPath}:{valueName}", 95,
                            "Registry startup command is inside the application install directory"));
                    }
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
            }
        }
    }

    private static void CollectScheduledTasks(
        string installRoot,
        ICollection<FingerprintEntry> entries,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Tasks");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string file in EnumerateFilesSafely(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                XDocument document = XDocument.Load(stream, LoadOptions.None);
                bool belongs = document.Descendants()
                    .Where(static node => node.Name.LocalName == "Command")
                    .Any(node => CommandBelongsToApplication(node.Value, installRoot));
                if (belongs)
                {
                    entries.Add(new FingerprintEntry(
                        FingerprintResourceType.Task,
                        Path.GetRelativePath(root, file), 90,
                        "Scheduled task action is inside the application install directory"));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Xml.XmlException)
            {
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                files = Directory.EnumerateFiles(directory).ToArray();
                directories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            foreach (string child in directories)
            {
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                }
            }
        }
    }

    private static string ExtractExecutable(string commandLine)
    {
        if (commandLine.StartsWith('"'))
        {
            int end = commandLine.IndexOf('"', 1);
            return end > 1 ? commandLine[1..end] : string.Empty;
        }

        int exe = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            return commandLine[..(exe + 4)].Trim();
        }

        int separator = commandLine.IndexOf(' ');
        return separator < 0 ? commandLine : commandLine[..separator];
    }
}

using System.Security.Cryptography;
using System.Text;
using LeslieCleaner.Core.Abstractions;
using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 对扫描器实际读取的选定位置计算 SHA-256 指纹。
/// 指纹同时包含注册表原始值和引用路径的存在/修改状态，
/// 避免“注册表没变但文件已恢复或丢失”时误用快存。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsRegistryStateFingerprintProvider : IRegistryStateFingerprintProvider
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string FontsPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private const string ClassesPath = @"SOFTWARE\Classes";

    /// <inheritdoc />
    public Task<string> ComputeAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, Environment.OSVersion.VersionString);
        foreach (RegistryView view in Views())
        {
            AppendUninstall(hash, RegistryHive.LocalMachine, view, cancellationToken);
            AppendUninstall(hash, RegistryHive.CurrentUser, view, cancellationToken);
            AppendValues(hash, RegistryHive.LocalMachine, view, RunPath, commandLine: true, cancellationToken);
            AppendValues(hash, RegistryHive.CurrentUser, view, RunPath, commandLine: true, cancellationToken);
            AppendClasses(hash, RegistryHive.CurrentUser, view, cancellationToken);
            AppendClasses(hash, RegistryHive.LocalMachine, view, cancellationToken);
        }
        AppendValues(hash, RegistryHive.LocalMachine, RegistryView.Registry64, FontsPath,
            commandLine: false, cancellationToken, Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
        return Convert.ToHexString(hash.GetHashAndReset());
    }, cancellationToken);

    private static void AppendUninstall(
        IncrementalHash hash,
        RegistryHive hive,
        RegistryView view,
        CancellationToken token)
    {
        using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? uninstall = root.OpenSubKey(UninstallPath, writable: false);
        Append(hash, $"U|{hive}|{view}|{(uninstall is null ? 0 : 1)}");
        if (uninstall is null) return;
        foreach (string name in uninstall.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using RegistryKey? item = uninstall.OpenSubKey(name, writable: false);
                Append(hash, name);
                if (item is null) continue;
                foreach (string valueName in new[] { "DisplayName", "Publisher", "InstallLocation", "DisplayIcon" })
                {
                    string raw = Convert.ToString(item.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                        System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    Append(hash, valueName + "=" + raw);
                    if (valueName == "DisplayIcon")
                        AppendDependency(hash, RegistryRules.StripIconIndex(raw));
                    else if (valueName == "InstallLocation")
                        AppendDependency(hash, RegistryRules.NormalizePath(raw));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Append(hash, "INACCESSIBLE|" + name + "|" + exception.GetType().Name);
            }
        }
    }

    private static void AppendValues(
        IncrementalHash hash,
        RegistryHive hive,
        RegistryView view,
        string subPath,
        bool commandLine,
        CancellationToken token,
        string? relativeBaseDirectory = null)
    {
        try
        {
            using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? key = root.OpenSubKey(subPath, writable: false);
            Append(hash, $"V|{hive}|{view}|{subPath}|{(key is null ? 0 : 1)}");
            if (key is null) return;
            foreach (string name in key.GetValueNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                string raw = Convert.ToString(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                Append(hash, name + "=" + raw);
                string? dependency = commandLine
                    ? RegistryRules.ExtractExecutablePath(raw)
                    : RegistryRules.NormalizePath(raw);
                if (!commandLine && dependency is not null && !Path.IsPathRooted(dependency) &&
                    !string.IsNullOrWhiteSpace(relativeBaseDirectory))
                {
                    dependency = Path.Combine(relativeBaseDirectory, dependency);
                }

                if (commandLine)
                    AppendWindowsCommandDependency(hash, dependency);
                else
                    AppendDependency(hash, dependency);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Append(hash, $"INACCESSIBLE|{hive}|{view}|{subPath}|{exception.GetType().Name}");
        }
    }

    private static void AppendClasses(
        IncrementalHash hash, RegistryHive hive, RegistryView view, CancellationToken token)
    {
        try
        {
            using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? classes = root.OpenSubKey(ClassesPath, writable: false);
            Append(hash, $"CLASSES|{hive}|{view}|{(classes is null ? 0 : 1)}");
            if (classes is null) return;

            using RegistryKey mergedClasses = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
            string[] names = classes.GetSubKeyNames();
            foreach (string extension in names.Where(static value => value.Length > 1 && value[0] == '.')
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                using RegistryKey? key = classes.OpenSubKey(extension, writable: false);
                string progId = key?.GetValue(null) as string ?? string.Empty;
                using RegistryKey? progIdKey = progId.Length == 0 ? null : mergedClasses.OpenSubKey(progId, writable: false);
                Append(hash, $"E|{hive}|{view}|{extension}|{progId}|{(progIdKey is null ? 0 : 1)}");
            }

            using RegistryKey? clsid = classes.OpenSubKey("CLSID", writable: false);
            if (clsid is null) return;
            foreach (string name in clsid.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using RegistryKey? server = classes.OpenSubKey($@"CLSID\{name}\InprocServer32", writable: false);
                    if (server is null) continue;
                    string raw = server.GetValue(null) as string ?? string.Empty;
                    Append(hash, $"C|{hive}|{view}|{name}|{raw}");
                    AppendComDependency(hash, RegistryRules.NormalizePath(raw));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    Append(hash, $"INACCESSIBLE|CLSID|{hive}|{view}|{name}|{exception.GetType().Name}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Append(hash, $"INACCESSIBLE|CLASSES|{hive}|{view}|{exception.GetType().Name}");
        }
    }

    private static void AppendWindowsCommandDependency(IncrementalHash hash, string? path)
    {
        AppendDependency(hash, path);
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Contains(Path.DirectorySeparatorChar) || path.Contains(Path.AltDirectorySeparatorChar))
        {
            return;
        }

        IEnumerable<string?> directories =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            .. (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static directory => directory.Trim('"')),
        ];
        foreach (string directory in directories.Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Select(static value => value!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                AppendDependency(hash, Path.Combine(directory, path));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                Append(hash, $"X|COMMAND|{directory}|{path}|{exception.GetType().Name}");
            }
        }
    }

    private static void AppendComDependency(IncrementalHash hash, string? path)
    {
        AppendDependency(hash, path);
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return;
        }

        foreach (string directory in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.System),
                     Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                 }.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AppendDependency(hash, Path.Combine(directory, path));
        }
    }

    private static void AppendDependency(IncrementalHash hash, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Append(hash, "D|null");
            return;
        }
        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (File.Exists(expanded))
            {
                var info = new FileInfo(expanded);
                Append(hash, $"F|{expanded}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
            }
            else if (Directory.Exists(expanded))
            {
                Append(hash, $"D|{expanded}|{Directory.GetLastWriteTimeUtc(expanded).Ticks}");
            }
            else Append(hash, "M|" + expanded);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Append(hash, "X|" + path + "|" + exception.GetType().Name);
        }
    }

    private static RegistryView[] Views() => Environment.Is64BitOperatingSystem
        ? [RegistryView.Registry64, RegistryView.Registry32]
        : [RegistryView.Registry64];

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value + "\n"));
}

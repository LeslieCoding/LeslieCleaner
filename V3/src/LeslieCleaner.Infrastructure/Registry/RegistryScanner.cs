using System.Collections.Concurrent;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Win32;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// Read-only registry rule scanner. It evaluates every applicable 64-bit and 32-bit view
/// and never shares a <see cref="RegistryKey"/> instance between parallel workers.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RegistryScanner : IRegistryScanner
{
    private const string UninstallSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string RunSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string FontsSubPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private static readonly RegistryView[] RegistryViews = Environment.Is64BitOperatingSystem
        ? [RegistryView.Registry64, RegistryView.Registry32]
        : [RegistryView.Registry64];

    private readonly ILogService _logService;

    /// <summary>Initializes the scanner.</summary>
    /// <param name="logService">Structured logging service.</param>
    public RegistryScanner(ILogService logService) => _logService = logService;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryFinding>> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logService.LogEvent(LogEventKind.ScanStart, "Registry rule scan started");
        var findings = new ConcurrentBag<RegistryFinding>();

        await Task.Run(() =>
        {
            ScanUninstallEntries(findings, cancellationToken);
            progress?.Report(new ScanProgress("Uninstall entries", 1, 5, null));

            ScanFonts(findings, cancellationToken);
            progress?.Report(new ScanProgress("Font references", 2, 5, null));

            ScanRunKeys(findings, cancellationToken);
            progress?.Report(new ScanProgress("Startup paths", 3, 5, null));

            ScanFileExtensions(findings, cancellationToken);
            progress?.Report(new ScanProgress("File extensions", 4, 5, null));

            ScanComServers(findings, progress, cancellationToken);
            progress?.Report(new ScanProgress("COM/CLSID", 5, 5, null));
        }, cancellationToken).ConfigureAwait(false);

        var result = findings.ToList();
        result.Sort(static (left, right) => left.Risk.CompareTo(right.Risk));
        _logService.LogEvent(LogEventKind.ScanFinish, "Registry rule scan completed", new Dictionary<string, object?>
        {
            ["Findings"] = result.Count,
        });
        return result;
    }

    private void ScanUninstallEntries(ConcurrentBag<RegistryFinding> findings, CancellationToken cancellationToken)
    {
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumerateViews(RegistryHive.LocalMachine, "HKLM")
            .Concat(EnumerateViews(RegistryHive.CurrentUser, "HKCU")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = RegistryKey.OpenBaseKey(registryHive, registryView);
            using RegistryKey? uninstall = hive.OpenSubKey(UninstallSubPath, writable: false);
            if (uninstall is null)
            {
                continue;
            }

            foreach (string subKeyName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using RegistryKey? entry = uninstall.OpenSubKey(subKeyName, writable: false);
                    if (entry is null)
                    {
                        continue;
                    }

                    string keyPath = $@"{rootName}\{UninstallSubPath}\{subKeyName}";
                    RegistryFinding? finding = RegistryRules.EvaluateUninstallEntry(
                        keyPath,
                        entry.GetValue("DisplayName") as string,
                        entry.GetValue("InstallLocation") as string,
                        entry.GetValue("DisplayIcon") as string,
                        PathExists);
                    if (finding is not null)
                    {
                        findings.Add(finding);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    _logService.LogException(exception, "RegistryScanner.ScanUninstallEntries", new Dictionary<string, object?>
                    {
                        ["SubKey"] = subKeyName,
                        ["RegistryView"] = registryView.ToString(),
                    });
                }
            }
        }
    }

    private void ScanFonts(ConcurrentBag<RegistryFinding> findings, CancellationToken cancellationToken)
    {
        string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? fonts = hive.OpenSubKey(FontsSubPath, writable: false);
        if (fonts is null)
        {
            return;
        }

        string keyPath = $@"HKLM\{FontsSubPath}";
        foreach (string valueName in fonts.GetValueNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegistryFinding? finding = RegistryRules.EvaluateFont(
                keyPath, valueName, fonts.GetValue(valueName) as string, fontsDirectory, File.Exists);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }

    private void ScanRunKeys(ConcurrentBag<RegistryFinding> findings, CancellationToken cancellationToken)
    {
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumerateViews(RegistryHive.LocalMachine, "HKLM")
            .Concat(EnumerateViews(RegistryHive.CurrentUser, "HKCU")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey hive = RegistryKey.OpenBaseKey(registryHive, registryView);
            using RegistryKey? run = hive.OpenSubKey(RunSubPath, writable: false);
            if (run is null)
            {
                continue;
            }

            string keyPath = $@"{rootName}\{RunSubPath}";
            foreach (string valueName in run.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                RegistryFinding? finding = RegistryRules.EvaluatePathValue(
                    keyPath, valueName, run.GetValue(valueName) as string, PathExists);
                if (finding is not null)
                {
                    findings.Add(finding);
                }
            }
        }
    }

    private void ScanFileExtensions(ConcurrentBag<RegistryFinding> findings, CancellationToken cancellationToken)
    {
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumerateViews(RegistryHive.ClassesRoot, "HKCR"))
        {
            string[] subKeyNames;
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(registryHive, registryView);
                subKeyNames = root.GetSubKeyNames();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(exception, "RegistryScanner.ScanFileExtensions", new Dictionary<string, object?>
                {
                    ["RegistryView"] = registryView.ToString(),
                });
                continue;
            }

            var progIdCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Parallel.ForEach(
                subKeyNames.Where(static name => name.StartsWith('.') && name.Length > 1),
                CreateParallelOptions(cancellationToken),
                () => RegistryKey.OpenBaseKey(registryHive, registryView),
                (extension, _, root) =>
                {
                    try
                    {
                        using RegistryKey? extensionKey = root.OpenSubKey(extension, writable: false);
                        string? progId = extensionKey?.GetValue(null) as string;
                        RegistryFinding? finding = RegistryRules.EvaluateFileExtension(
                            $@"{rootName}\{extension}", progId,
                            id => progIdCache.GetOrAdd(id, key =>
                            {
                                using RegistryKey? progIdKey = root.OpenSubKey(key, writable: false);
                                return progIdKey is not null;
                            }));
                        if (finding is not null)
                        {
                            findings.Add(finding);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        _logService.LogException(exception, "RegistryScanner.ScanFileExtensions", new Dictionary<string, object?>
                        {
                            ["Extension"] = extension,
                            ["RegistryView"] = registryView.ToString(),
                        });
                    }
                    return root;
                },
                static root => root.Dispose());
        }
    }

    private void ScanComServers(
        ConcurrentBag<RegistryFinding> findings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumerateViews(RegistryHive.ClassesRoot, "HKCR"))
        {
            string[] clsidNames;
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(registryHive, registryView);
                using RegistryKey? clsid = root.OpenSubKey("CLSID", writable: false);
                if (clsid is null)
                {
                    continue;
                }

                clsidNames = clsid.GetSubKeyNames();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(exception, "RegistryScanner.ScanComServers", new Dictionary<string, object?>
                {
                    ["RegistryView"] = registryView.ToString(),
                });
                continue;
            }

            var existenceCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            long processed = 0;
            Parallel.ForEach(
                clsidNames,
                CreateParallelOptions(cancellationToken),
                () => RegistryKey.OpenBaseKey(registryHive, registryView),
                (clsidName, _, root) =>
                {
                    try
                    {
                    using RegistryKey? inproc = root.OpenSubKey($@"CLSID\{clsidName}\InprocServer32", writable: false);
                    string? serverPath = inproc?.GetValue(null) as string;
                    RegistryFinding? finding = RegistryRules.EvaluateInprocServer(
                        $@"{rootName}\CLSID\{clsidName}\InprocServer32", serverPath,
                        path => existenceCache.GetOrAdd(path, static candidate => File.Exists(candidate)));
                    if (finding is not null)
                    {
                        if (IsSystemProtectedKey(inproc))
                        {
                            _logService.LogEvent(LogEventKind.UserAction, "Skipping protected registry key", new Dictionary<string, object?>
                            {
                                ["KeyPath"] = finding.KeyPath,
                            });
                            return root;
                        }

                        findings.Add(finding);
                    }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        _logService.LogException(exception, "RegistryScanner.ScanComServers", new Dictionary<string, object?>
                        {
                            ["Clsid"] = clsidName,
                            ["RegistryView"] = registryView.ToString(),
                        });
                    }
                    finally
                    {
                        long done = Interlocked.Increment(ref processed);
                        if ((done & 0x1FF) == 0)
                        {
                            progress?.Report(new ScanProgress("COM/CLSID", done, clsidNames.Length, clsidName));
                        }
                    }
                    return root;
                },
                static root => root.Dispose());
        }
    }

    private static IEnumerable<(RegistryHive Hive, string RootName, RegistryView View)> EnumerateViews(RegistryHive registryHive, string rootName)
    {
        foreach (RegistryView registryView in RegistryViews)
        {
            yield return (registryHive, registryView == RegistryView.Registry32 ? rootName + "32" : rootName, registryView);
        }
    }

    private static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken) => new()
    {
        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8),
        CancellationToken = cancellationToken,
    };

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>Returns whether the key is protected from administrator deletion by Windows Resource Protection.</summary>
    /// <param name="key">Opened registry key.</param>
    /// <returns><see langword="true"/> when the key should not be offered for cleanup.</returns>
    public static bool IsSystemProtectedKey(RegistryKey? key)
    {
        if (key is null)
        {
            return false;
        }

        try
        {
            System.Security.AccessControl.RegistrySecurity acl = key.GetAccessControl();
            bool adminsFullControl = acl.GetAccessRules(
                    includeExplicit: true, includeInherited: true, typeof(System.Security.Principal.NTAccount))
                .Cast<System.Security.AccessControl.RegistryAccessRule>()
                .Any(rule =>
                    rule.IdentityReference.Value.Equals(@"BUILTIN\Administrators", StringComparison.OrdinalIgnoreCase) &&
                    rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow &&
                    (rule.RegistryRights & System.Security.AccessControl.RegistryRights.FullControl) ==
                        System.Security.AccessControl.RegistryRights.FullControl);
            return !adminsFullControl;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}

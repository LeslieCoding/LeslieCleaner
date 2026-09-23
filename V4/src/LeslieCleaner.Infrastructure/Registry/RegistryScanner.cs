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
    private const string ClassesSubPath = @"SOFTWARE\Classes";
    private const string TrustedInstallerSid =
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    private static readonly RegistryView[] RegistryViews = Environment.Is64BitOperatingSystem
        ? [RegistryView.Registry64, RegistryView.Registry32]
        : [RegistryView.Registry64];

    private readonly ILogService _logService;
    private readonly IRegistryScanCacheStore? _cacheStore;
    private readonly IRegistryStateFingerprintProvider? _fingerprintProvider;

    /// <summary>Initializes the scanner.</summary>
    /// <param name="logService">Structured logging service.</param>
    /// <param name="cacheStore">Optional seven-day persistent scan cache.</param>
    /// <param name="fingerprintProvider">Optional authoritative registry/dependency fingerprint provider.</param>
    public RegistryScanner(
        ILogService logService,
        IRegistryScanCacheStore? cacheStore = null,
        IRegistryStateFingerprintProvider? fingerprintProvider = null)
    {
        _logService = logService;
        _cacheStore = cacheStore;
        _fingerprintProvider = fingerprintProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryFinding>> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logService.LogEvent(LogEventKind.ScanStart, "Registry rule scan started");
        string? initialFingerprint = null;
        if (_cacheStore is not null && _fingerprintProvider is not null)
        {
            try
            {
                initialFingerprint = await _fingerprintProvider.ComputeAsync(cancellationToken).ConfigureAwait(false);
                RegistryScanCacheSnapshot? cached = await _cacheStore.TryReadAsync(
                    initialFingerprint, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                if (cached is not null && cached.Findings.All(static finding =>
                        !finding.KeyPath.StartsWith("HKCR\\", StringComparison.OrdinalIgnoreCase) &&
                        !finding.KeyPath.StartsWith("HKCR32\\", StringComparison.OrdinalIgnoreCase)))
                {
                    progress?.Report(new ScanProgress("Registry cache", 5, 5, null));
                    _logService.LogEvent(LogEventKind.ScanFinish, "Registry scan reused verified incremental cache", new Dictionary<string, object?>
                    {
                        ["Findings"] = cached.Findings.Count,
                        ["CacheCreatedUtc"] = cached.CreatedUtc,
                    });
                    return cached.Findings.OrderBy(static finding => finding.Risk).ToArray();
                }

                if (cached is not null)
                {
                    await _cacheStore.InvalidateAsync(cancellationToken).ConfigureAwait(false);
                    _logService.LogEvent(LogEventKind.UserAction,
                        "Legacy merged-HKCR registry cache invalidated; physical-hive scan required");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException)
            {
                _logService.LogException(exception, "Registry cache validation failed; continuing with fresh scan");
                initialFingerprint = null;
            }
        }

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

        var result = findings.Select(static finding => RegistryFindingAnalyzer.Analyze(
                finding,
                finding.Evidence with { RegistryView = ViewNameFromPath(finding.KeyPath) }))
            .OfType<RegistryFinding>()
            // Multiple rules can describe the same physical key/value (for example an invalid
            // extension and a broken COM association). Present one actionable row per physical
            // target; keep the highest-risk evidence so the UI cannot silently choose a weaker
            // duplicate when the batch cleaner groups targets later.
            .GroupBy(static finding => PhysicalTargetIdentity(finding), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static finding => finding.RiskScore)
                .ThenBy(static finding => finding.Issue)
                .First())
            .ToList();
        result.Sort(static (left, right) => left.Risk.CompareTo(right.Risk));
        if (initialFingerprint is not null && _cacheStore is not null && _fingerprintProvider is not null)
        {
            try
            {
                string finalFingerprint = await _fingerprintProvider.ComputeAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(initialFingerprint, finalFingerprint, StringComparison.Ordinal))
                {
                    DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
                    await _cacheStore.WriteAsync(
                        new RegistryScanCacheSnapshot(finalFingerprint, createdUtc, createdUtc.AddDays(7), result),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logService.LogEvent(LogEventKind.UserAction, "Registry changed during scan; incremental cache was not written");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException)
            {
                _logService.LogException(exception, "Registry cache write failed; scan result remains authoritative");
            }
        }

        _logService.LogEvent(LogEventKind.ScanFinish, "Registry rule scan completed", new Dictionary<string, object?>
        {
            ["Findings"] = result.Count,
        });
        return result;
    }

    private static string PhysicalTargetIdentity(RegistryFinding finding) =>
        string.Concat(finding.KeyPath.Trim().TrimEnd('\\'), '\u001F', finding.ValueName ?? "<key>");

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
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumeratePhysicalClassLocations())
        {
            string[] subKeyNames;
            HashSet<string> mergedProgIds;
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(registryHive, registryView);
                using RegistryKey? classes = root.OpenSubKey(ClassesSubPath, writable: false);
                if (classes is null)
                {
                    continue;
                }

                subKeyNames = classes.GetSubKeyNames();
                using RegistryKey mergedClasses = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, registryView);
                mergedProgIds = new HashSet<string>(mergedClasses.GetSubKeyNames(), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logService.LogException(exception, "RegistryScanner.ScanFileExtensions", new Dictionary<string, object?>
                {
                    ["RegistryHive"] = registryHive.ToString(),
                    ["RegistryView"] = registryView.ToString(),
                });
                continue;
            }

            Parallel.ForEach(
                subKeyNames.Where(static name => name.StartsWith('.') && name.Length > 1),
                CreateParallelOptions(cancellationToken),
                () => RegistryKey.OpenBaseKey(registryHive, registryView),
                (extension, _, root) =>
                {
                    try
                    {
                        using RegistryKey? extensionKey = root.OpenSubKey(
                            $@"{ClassesSubPath}\{extension}", writable: false);
                        string? progId = extensionKey?.GetValue(null) as string;
                        RegistryFinding? finding = RegistryRules.EvaluateFileExtension(
                            $@"{rootName}\{ClassesSubPath}\{extension}", progId,
                            mergedProgIds.Contains);
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
                            ["RegistryHive"] = registryHive.ToString(),
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
        foreach ((RegistryHive registryHive, string rootName, RegistryView registryView) in EnumeratePhysicalClassLocations())
        {
            string[] clsidNames;
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(registryHive, registryView);
                using RegistryKey? clsid = root.OpenSubKey($@"{ClassesSubPath}\CLSID", writable: false);
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
                    ["RegistryHive"] = registryHive.ToString(),
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
                        using RegistryKey? inproc = root.OpenSubKey(
                            $@"{ClassesSubPath}\CLSID\{clsidName}\InprocServer32", writable: false);
                        string? serverPath = inproc?.GetValue(null) as string;
                        RegistryFinding? finding = RegistryRules.EvaluateInprocServer(
                            $@"{rootName}\{ClassesSubPath}\CLSID\{clsidName}\InprocServer32", serverPath,
                            path => existenceCache.GetOrAdd(path, static candidate => File.Exists(candidate)));
                        if (finding is not null)
                        {
                            if (IsSystemProtectedKey(inproc))
                            {
                                _logService.LogEvent(LogEventKind.UserAction, "Skipping protected registry key", new Dictionary<string, object?>
                                {
                                    ["KeyPath"] = finding.KeyPath,
                                    ["RegistryHive"] = registryHive.ToString(),
                                    ["RegistryView"] = registryView.ToString(),
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
                            ["RegistryHive"] = registryHive.ToString(),
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

    private static IEnumerable<(RegistryHive Hive, string RootName, RegistryView View)> EnumeratePhysicalClassLocations()
    {
        foreach (RegistryView registryView in RegistryViews)
        {
            string suffix = registryView == RegistryView.Registry32 ? "32" : string.Empty;
            yield return (RegistryHive.CurrentUser, "HKCU" + suffix, registryView);
            yield return (RegistryHive.LocalMachine, "HKLM" + suffix, registryView);
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

    private static string ViewNameFromPath(string keyPath)
    {
        string root = keyPath.Trim().TrimStart('\\').Split('\\', 2)[0];
        return root.EndsWith("32", StringComparison.OrdinalIgnoreCase)
            ? RegistryView.Registry32.ToString()
            : RegistryView.Registry64.ToString();
    }

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
            System.Security.Principal.IdentityReference? owner = acl.GetOwner(
                typeof(System.Security.Principal.SecurityIdentifier));
            return owner?.Value.Equals(TrustedInstallerSid, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 无法读取 ACL 时不能把普通键误判为 WRP；实际写入仍由 RegistryEngine 权限检查和回读复验兜底。
            return false;
        }
    }
}

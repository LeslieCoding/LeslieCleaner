using System.Diagnostics;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Win32;

namespace LeslieCleaner.Modules.Uninstaller.Discovery;

/// <summary>
/// 应用发现引擎。覆盖圣经第一节第 3 条全部来源：
/// Win32（HKLM 64/32 + HKCU Uninstall）、MSI（ProductCode/UpgradeCode + Installer Cache 探测）、
/// MSIX/Appx（Windows.Management.Deployment.PackageManager，失败优雅降级）、Winget（winget list，
/// 不存在自动跳过）、Steam（libraryfolders.vdf + appmanifest_*.acf，含自定义库）、Epic（.item 清单）、
/// Battle.net/Adobe/Office/VS/Docker/Node/Python/Java（注册表签名重分类 + VS 实例 state.json）。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class InstalledAppDiscovery
{
    private const string UninstallSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly TimeSpan WingetTimeout = TimeSpan.FromSeconds(60);

    private readonly ILogService _logService;

    /// <summary>以日志服务创建发现引擎。</summary>
    /// <param name="logService">日志服务。</param>
    public InstalledAppDiscovery(ILogService logService) => _logService = logService;

    /// <summary>
    /// 枚举全部已安装应用（按名称排序，跨来源按名称+版本去重）。
    /// </summary>
    /// <param name="progress">进度回调，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<InstalledApp>> EnumerateAllAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logService.LogEvent(LogEventKind.ScanStart, "应用发现开始");
        var apps = new List<InstalledApp>();

        apps.AddRange(await Task.Run(() => EnumerateRegistryApps(cancellationToken), cancellationToken).ConfigureAwait(false));
        progress?.Report(new ScanProgress("注册表卸载项", 1, 5, null));

        apps.AddRange(await EnumerateAppxPackagesAsync(cancellationToken).ConfigureAwait(false));
        progress?.Report(new ScanProgress("MSIX/Appx", 2, 5, null));

        apps.AddRange(await EnumerateWingetAppsAsync(cancellationToken).ConfigureAwait(false));
        progress?.Report(new ScanProgress("Winget", 3, 5, null));

        apps.AddRange(await Task.Run(() => EnumerateSteamApps(cancellationToken), cancellationToken).ConfigureAwait(false));
        apps.AddRange(await Task.Run(() => EnumerateEpicApps(cancellationToken), cancellationToken).ConfigureAwait(false));
        progress?.Report(new ScanProgress("游戏平台", 4, 5, null));

        apps.AddRange(await Task.Run(() => EnumerateVisualStudioInstances(cancellationToken), cancellationToken).ConfigureAwait(false));
        progress?.Report(new ScanProgress("开发环境", 5, 5, null));

        var classified = apps
            .Select(AppParsers.ClassifyBySignature)
            .GroupBy(static a => (a.Name, a.Version), static (_, group) => group.First())
            .OrderBy(static a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logService.LogEvent(LogEventKind.ScanFinish, "应用发现完成",
            new Dictionary<string, object?> { ["Apps"] = classified.Count });
        return classified;
    }

    /// <summary>
    /// 仅从注册表 Uninstall 键枚举（Stage 1 残留定位复用，&lt;100ms）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public IReadOnlyList<InstalledApp> EnumerateRegistryApps(CancellationToken cancellationToken = default)
    {
        var apps = new List<InstalledApp>();
        (RegistryHive Hive, RegistryView View, string Prefix)[] scopes =
        [
            (RegistryHive.LocalMachine, RegistryView.Registry64, "hklm64"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "hklm32"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, "hkcu"),
        ];

        foreach ((RegistryHive hive, RegistryView view, string prefix) in scopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? uninstall = baseKey.OpenSubKey(UninstallSubPath, writable: false);
            if (uninstall is null)
            {
                continue;
            }

            foreach (string keyName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using RegistryKey? entry = uninstall.OpenSubKey(keyName, writable: false);
                    if (entry is null)
                    {
                        continue;
                    }

                    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (string valueName in entry.GetValueNames())
                    {
                        values[valueName] = entry.GetValue(valueName);
                    }

                    InstalledApp? app = AppParsers.ParseUninstallValues(keyName, values, prefix);
                    if (app is not null)
                    {
                        apps.Add(app);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    _logService.LogException(ex, "InstalledAppDiscovery.EnumerateRegistryApps",
                        new Dictionary<string, object?> { ["Key"] = keyName });
                }
            }
        }

        return apps;
    }

    /// <summary>
    /// 探测 Windows Installer Cache 中指定 ProductCode 的缓存包是否存在（只读）。
    /// </summary>
    /// <param name="productCode">MSI ProductCode（含花括号）。</param>
    public static bool InstallerCacheHasPackage(string productCode)
    {
        string installerCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer");
        try
        {
            return Directory.Exists(installerCache) &&
                Directory.EnumerateFiles(installerCache, "*.msi")
                    .Any(f => f.Contains(productCode.Trim('{', '}'), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<InstalledApp>> EnumerateAppxPackagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run<IReadOnlyList<InstalledApp>>(() =>
            {
                var manager = new Windows.Management.Deployment.PackageManager();
                var apps = new List<InstalledApp>();
                foreach (Windows.ApplicationModel.Package package in manager.FindPackages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        Windows.ApplicationModel.PackageId id = package.Id;
                        string name = id.Name;
                        string? publisher = id.Publisher;
                        bool isStore = !string.IsNullOrEmpty(package.SignatureKind.ToString()) &&
                            package.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store;
                        apps.Add(new InstalledApp(
                            $"appx:{id.FullName}",
                            name,
                            $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}",
                            publisher,
                            0,
                            DateOnly.FromDateTime(package.InstalledDate.DateTime),
                            package.InstalledLocation?.Path,
                            null,
                            isStore ? AppSourceType.Store : AppSourceType.Appx,
                            null, null));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or FileNotFoundException)
                    {
                        _logService.LogException(ex, "InstalledAppDiscovery.AppxPackage");
                    }
                }

                return apps;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException
            or UnauthorizedAccessException or System.IO.FileNotFoundException or TypeLoadException)
        {
            // WinRT 投影不可用/权限不足：优雅降级，Appx 来源留空
            _logService.LogException(ex, "InstalledAppDiscovery.EnumerateAppxPackagesAsync");
            return [];
        }
    }

    private async Task<IReadOnlyList<InstalledApp>> EnumerateWingetAppsAsync(CancellationToken cancellationToken)
    {
        string? winget = FindOnPath("winget.exe");
        if (winget is null)
        {
            return [];
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = winget,
                Arguments = "list --disable-interactivity --accept-source-agreements",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // winget 输出跟随系统 ANSI 代码页（中文 Windows 为 GBK）
                StandardOutputEncoding = System.Text.Encoding.Default,
            };
            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(WingetTimeout);
            try
            {
                string output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                return AppParsers.ParseWingetList(output);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                _logService.LogEvent(LogEventKind.Exception, "winget list 超时熔断",
                    new Dictionary<string, object?> { ["TimeoutSeconds"] = WingetTimeout.TotalSeconds });
                return [];
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _logService.LogException(ex, "InstalledAppDiscovery.EnumerateWingetAppsAsync");
            return [];
        }
    }

    private IReadOnlyList<InstalledApp> EnumerateSteamApps(CancellationToken cancellationToken)
    {
        string? steamRoot = GetSteamRoot();
        if (steamRoot is null)
        {
            return [];
        }

        var apps = new List<InstalledApp>();
        string libraryFoldersVdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        IReadOnlyList<string> libraries = File.Exists(libraryFoldersVdf)
            ? AppParsers.ParseLibraryFolders(File.ReadAllText(libraryFoldersVdf), steamRoot)
            : [Path.Combine(steamRoot, "steamapps")];

        foreach (string library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(library))
            {
                continue;
            }

            foreach (string manifest in Directory.EnumerateFiles(library, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    InstalledApp? app = AppParsers.ParseSteamAppManifest(File.ReadAllText(manifest), library);
                    if (app is not null)
                    {
                        apps.Add(app);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logService.LogException(ex, "InstalledAppDiscovery.EnumerateSteamApps",
                        new Dictionary<string, object?> { ["Manifest"] = manifest });
                }
            }
        }

        return apps;
    }

    private IReadOnlyList<InstalledApp> EnumerateEpicApps(CancellationToken cancellationToken)
    {
        string manifestsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");
        if (!Directory.Exists(manifestsDirectory))
        {
            return [];
        }

        var apps = new List<InstalledApp>();
        foreach (string manifest in Directory.EnumerateFiles(manifestsDirectory, "*.item"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                InstalledApp? app = AppParsers.ParseEpicManifest(File.ReadAllText(manifest), manifest);
                if (app is not null)
                {
                    apps.Add(app);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "InstalledAppDiscovery.EnumerateEpicApps",
                    new Dictionary<string, object?> { ["Manifest"] = manifest });
            }
        }

        return apps;
    }

    private IReadOnlyList<InstalledApp> EnumerateVisualStudioInstances(CancellationToken cancellationToken)
    {
        string instancesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\VisualStudio\Packages\_Instances");
        if (!Directory.Exists(instancesDirectory))
        {
            return [];
        }

        var apps = new List<InstalledApp>();
        foreach (string instanceDirectory in Directory.EnumerateDirectories(instancesDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string stateFile = Path.Combine(instanceDirectory, "state.json");
            if (!File.Exists(stateFile))
            {
                continue;
            }

            try
            {
                InstalledApp? app = AppParsers.ParseVisualStudioInstanceState(
                    File.ReadAllText(stateFile), Path.GetFileName(instanceDirectory));
                if (app is not null)
                {
                    apps.Add(app);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "InstalledAppDiscovery.EnumerateVisualStudioInstances",
                    new Dictionary<string, object?> { ["State"] = stateFile });
            }
        }

        return apps;
    }

    private string? GetSteamRoot()
    {
        try
        {
            using RegistryKey? steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", writable: false);
            string? steamPath = steam?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath))
            {
                return steamPath;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logService.LogException(ex, "InstalledAppDiscovery.GetSteamRoot");
        }

        string? programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string candidate = Path.Combine(programFilesX86 ?? string.Empty, "Steam");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static string? FindOnPath(string fileName)
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

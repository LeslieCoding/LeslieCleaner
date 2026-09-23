namespace LeslieCleaner.UI.Services;

/// <summary>模块程序集与类型名常量（懒激活寻址，避免调用点硬编码字符串散落）。</summary>
public static class ModuleRefs
{
    /// <summary>Cleaner 模块程序集。</summary>
    public const string CleanerAssembly = "LeslieCleaner.Modules.Cleaner.dll";

    /// <summary>Browser 模块程序集。</summary>
    public const string BrowserAssembly = "LeslieCleaner.Modules.Browser.dll";

    /// <summary>Startup 模块程序集。</summary>
    public const string StartupAssembly = "LeslieCleaner.Modules.Startup.dll";

    /// <summary>Privacy 模块程序集。</summary>
    public const string PrivacyAssembly = "LeslieCleaner.Modules.Privacy.dll";

    /// <summary>Shredder 模块程序集。</summary>
    public const string ShredderAssembly = "LeslieCleaner.Modules.Shredder.dll";

    /// <summary>Uninstaller 模块程序集。</summary>
    public const string UninstallerAssembly = "LeslieCleaner.Modules.Uninstaller.dll";

    /// <summary>AI 模块程序集。</summary>
    public const string AiAssembly = "LeslieCleaner.Modules.AI.dll";

    /// <summary>SmartScanEngine 类型名。</summary>
    public const string SmartScanEngineType = "LeslieCleaner.Modules.Cleaner.SmartScanEngine";

    /// <summary>SystemScanEnvironment 类型名。</summary>
    public const string SystemScanEnvironmentType = "LeslieCleaner.Modules.Cleaner.Scanning.SystemScanEnvironment";

    /// <summary>DuplicateFinderEngine 类型名。</summary>
    public const string DuplicateFinderEngineType = "LeslieCleaner.Modules.Cleaner.Duplicates.DuplicateFinderEngine";

    /// <summary>LargeFileAnalyzerEngine 类型名。</summary>
    public const string LargeFileAnalyzerEngineType = "LeslieCleaner.Modules.Cleaner.Analytics.LargeFileAnalyzerEngine";

    /// <summary>StartupManagerEngine 类型名。</summary>
    public const string StartupManagerEngineType = "LeslieCleaner.Modules.Startup.StartupManagerEngine";

    /// <summary>PrivacyGuardEngine 类型名。</summary>
    public const string PrivacyGuardEngineType = "LeslieCleaner.Modules.Privacy.PrivacyGuardEngine";

    /// <summary>ShredderEngine 类型名。</summary>
    public const string ShredderEngineType = "LeslieCleaner.Modules.Shredder.ShredderEngine";

    /// <summary>InstalledAppDiscovery 类型名。</summary>
    public const string InstalledAppDiscoveryType = "LeslieCleaner.Modules.Uninstaller.Discovery.InstalledAppDiscovery";

    /// <summary>UninstallExecutor 类型名。</summary>
    public const string UninstallExecutorType = "LeslieCleaner.Modules.Uninstaller.Execution.UninstallExecutor";

    /// <summary>ResidualScannerEngine 类型名。</summary>
    public const string ResidualScannerEngineType = "LeslieCleaner.Modules.Uninstaller.Residual.ResidualScannerEngine";

    /// <summary>MaintenancePlanner 类型名。</summary>
    public const string MaintenancePlannerType = "LeslieCleaner.Modules.AI.MaintenancePlanner";
}

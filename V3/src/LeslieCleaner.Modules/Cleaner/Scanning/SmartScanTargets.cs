namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>
/// Smart Scan 十四类扫描目标定义（圣经第一节第 1 条）。
/// 路径不存在自动跳过；红线：仅覆盖系统自动生成的垃圾，不含用户文档/配置。
/// </summary>
public static class SmartScanTargets
{
    /// <summary>注册表清理目标标识（由 IRegistryScanner 供数，不走目录枚举）。</summary>
    public const string RegistryCategoryId = "Registry";

    /// <summary>Windows Shell 回收站类别标识。</summary>
    public const string RecycleBinCategoryId = "RecycleBin";

    /// <summary>全部目标定义。</summary>
    public static IReadOnlyList<ScanTargetDefinition> All { get; } =
    [
        new("TempFiles", "临时文件", ScanSafetyLevel.Default,
            ["Temp:"], []),

        new("WindowsUpdateCache", "Windows 更新缓存", ScanSafetyLevel.Default,
            [@"Windows:SoftwareDistribution\Download"], []),

        new("DeliveryOptimizationCache", "Delivery Optimization 缓存", ScanSafetyLevel.Default,
            [@"Windows:SoftwareDistribution\DeliveryOptimization",
             @"ProgramData:Microsoft\Windows\DeliveryOptimization\Cache"], []),

        new("ThumbnailCache", "缩略图缓存", ScanSafetyLevel.Default,
            [@"LocalAppData:Microsoft\Windows\Explorer"],
            ["thumbcache_*.db", "iconcache_*.db"]),

        new("LogFiles", "日志文件", ScanSafetyLevel.Default,
            [@"Windows:Logs"], []),

        new("CrashDumps", "崩溃转储", ScanSafetyLevel.Default,
            [@"LocalAppData:CrashDumps",
             @"ProgramData:Microsoft\Windows\WER\ReportQueue",
             @"ProgramData:Microsoft\Windows\WER\ReportArchive"], []),

        new("Prefetch", "Prefetch", ScanSafetyLevel.Optional,
            [@"Windows:Prefetch"], ["*.pf"]),

        new(RecycleBinCategoryId, "回收站", ScanSafetyLevel.Default,
            [@"FixedDrives:$Recycle.Bin"], []),

        new(RegistryCategoryId, "注册表清理", ScanSafetyLevel.Default,
            [], []),

        new("BrowserCache", "浏览器缓存", ScanSafetyLevel.Default,
            [@"LocalAppData:Google\Chrome\User Data\*\Cache",
             @"LocalAppData:Google\Chrome\User Data\*\Code Cache",
             @"LocalAppData:Microsoft\Edge\User Data\*\Cache",
             @"LocalAppData:Microsoft\Edge\User Data\*\Code Cache",
             @"LocalAppData:Mozilla\Firefox\Profiles\*\cache2"], []),

        new("GpuShaderCache", "GPU Shader Cache", ScanSafetyLevel.Default,
            [@"LocalAppData:NVIDIA\DXCache",
             @"LocalAppData:NVIDIA\GLCache",
             @"ProgramData:NVIDIA Corporation\NV_Cache",
             @"LocalAppData:AMD\DxCache",
             @"LocalAppData:AMD\GLCache",
             @"LocalAppData:Intel\ShaderCache"], []),

        new("FontCache", "Font Cache", ScanSafetyLevel.Optional,
            [@"Windows:ServiceProfiles\LocalService\AppData\Local\FontCache"], []),

        new("DirectXShaderCache", "DirectX Shader Cache", ScanSafetyLevel.Default,
            [@"LocalAppData:D3DSCache"], []),

        new("AiToolsCache", "AI 软件缓存", ScanSafetyLevel.Default,
            [@"RoamingAppData:Cursor\Cache",
             @"RoamingAppData:Cursor\Code Cache",
             @"RoamingAppData:Cursor\CachedData",
             @"RoamingAppData:Code\Cache",
             @"RoamingAppData:Code\Code Cache",
             @"RoamingAppData:Code\CachedData",
             @"RoamingAppData:Code - Insiders\Cache",
             @"RoamingAppData:Code - Insiders\Code Cache",
             @"LocalAppData:JetBrains\*\caches",
             @"RoamingAppData:ChatGPT\Cache",
             @"RoamingAppData:ChatGPT\Code Cache"], []),
    ];
}

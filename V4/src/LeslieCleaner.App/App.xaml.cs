using System.Diagnostics;
using System.Windows;
using LeslieCleaner.Core;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure;
using LeslieCleaner.Infrastructure.Logging;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.Theming;
using LeslieCleaner.UI.ViewModels;
using LeslieCleaner.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO;

namespace LeslieCleaner.App;

/// <summary>
/// WPF 应用程序宿主：DI 装配（核心基础设施 + 接口键懒工厂 + 模块懒激活器）、
/// 全局异常处理、主题初始化、Mica、冷启动测量。
/// 命令行钩子（截图验证用，真实行为非假数据）：
/// <c>--page &lt;导航标题&gt;</c> 启动后直达页面；<c>--theme light|dark</c> 强制主题；
/// <c>--autoscan</c> 启动后自动开始 Smart Scan。
/// </summary>
public partial class Program : Application
{
    private static readonly long ProcessStartTimestamp = Stopwatch.GetTimestamp();

    private ServiceProvider? _serviceProvider;
    private ILogService? _logService;

    /// <summary>启动参数：直达页面标题。</summary>
    public static string? StartupPage { get; private set; }

    /// <summary>启动参数：启动后自动 Smart Scan。</summary>
    public static bool AutoScan { get; private set; }

    /// <summary>启动参数：主题覆盖（light/dark/auto）。</summary>
    public static string? StartupThemeOverride { get; private set; }

    /// <summary>启动参数：提权重启后需要恢复定位的启动项稳定标识。</summary>
    public static string? StartupItemId { get; private set; }

    /// <summary>启动参数：注册表权限失败目标的一次性恢复令牌。</summary>
    public static string? RegistryResumeToken { get; private set; }

    private static bool ProvisionIntegrityBaseline { get; set; }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        ParseArguments(e.Args);
        base.OnStartup(e);

        _serviceProvider = BuildServiceProvider();
        _logService = _serviceProvider.GetRequiredService<ILogService>();
        GlobalExceptionHandler.Attach(_logService);

        if (ProvisionIntegrityBaseline)
        {
            Shutdown(ProvisionTrustedIntegrityBaseline());
            return;
        }

        SecurityAssessment securityAssessment = _serviceProvider.GetRequiredService<ISecurityManager>()
            .AssessAsync().GetAwaiter().GetResult();
        if (!securityAssessment.ApplicationIntegrityValid || !securityAssessment.DatabaseIntegrityValid ||
            !securityAssessment.AuditIntegrityValid)
        {
            _logService.LogEvent(LogEventKind.Exception, "启动安全检查失败", new Dictionary<string, object?>
            {
                ["ApplicationIntegrityValid"] = securityAssessment.ApplicationIntegrityValid,
                ["DatabaseIntegrityValid"] = securityAssessment.DatabaseIntegrityValid,
                ["AuditIntegrityValid"] = securityAssessment.AuditIntegrityValid,
                ["RejectedPlugins"] = securityAssessment.RejectedPlugins,
            });
            MessageBox.Show(
                "LeslieCleaner 检测到完整性或审计风险。请先在平台中心查看安全状态，确认前不要执行清理、注册表删除或文件粉碎。",
                "安全检查警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 事件流接入 Notification Center
        var eventTap = _serviceProvider.GetRequiredService<AppEventTap>();
        _serviceProvider.GetRequiredService<NotificationService>().Attach(eventTap);

        _logService.LogEvent(LogEventKind.UserAction, "LeslieCleaner 启动",
            new Dictionary<string, object?> { ["Args"] = string.Join(' ', e.Args) });

        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        themeService.Initialize();

        var mainWindow = new MainWindow(_serviceProvider);
        mainWindow.ContentRendered += OnFirstContentRendered;
        Current.MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.ApplyStartupHooks();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _logService?.LogEvent(LogEventKind.UserAction, "LeslieCleaner 退出");
        if (_serviceProvider is not null)
        {
            _serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        Serilog.Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnFirstContentRendered(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
        {
            window.ContentRendered -= OnFirstContentRendered;
            TimeSpan coldStart = Stopwatch.GetElapsedTime(ProcessStartTimestamp);
            _logService?.LogEvent(LogEventKind.PerformanceMetrics, "冷启动完成", new Dictionary<string, object?>
            {
                ["ColdStartMs"] = coldStart.TotalMilliseconds,
                ["TargetMs"] = 1500,
            });
        }
    }

    private static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--page" && i + 1 < args.Length)
            {
                StartupPage = args[i + 1];
            }
            else if (args[i] == "--theme" && i + 1 < args.Length)
            {
                StartupThemeOverride = args[i + 1];
            }
            else if (args[i] == StartupResumeTarget.ArgumentName && i + 1 < args.Length &&
                     StartupResumeTarget.TryNormalize(args[i + 1], out string? itemId))
            {
                StartupItemId = itemId;
            }
            else if (args[i] == "--registry-resume" && i + 1 < args.Length &&
                     Guid.TryParseExact(args[i + 1], "N", out _))
            {
                RegistryResumeToken = args[i + 1];
            }
            else if (args[i] == "--autoscan")
            {
                AutoScan = true;
            }
            else if (args[i] == "--provision-integrity-baseline")
            {
                ProvisionIntegrityBaseline = true;
            }
        }
    }

    private int ProvisionTrustedIntegrityBaseline()
    {
        const string authorizationReason =
            "Installer explicitly authorized the verified LeslieCleaner release payload.";
        try
        {
            string applicationRoot = Path.GetFullPath(AppContext.BaseDirectory);
            ServiceProvider provider = _serviceProvider ??
                throw new InvalidOperationException("Application services are not initialized.");
            var pathService = provider.GetRequiredService<IPathService>();
            var repairService = provider.GetRequiredService<IIntegrityRepairService>();
            string backupRoot = pathService.EnsureDirectory(
                Path.Combine(pathService.BackupDirectory, "SecurityIntegrity"));
            IReadOnlyList<TrustedIntegrityTarget> targets = IntegrityTargetPlanner.Build(applicationRoot);
            var request = new TrustedBaselineRequest(
                applicationRoot,
                backupRoot,
                targets,
                ExplicitlyAuthorized: true,
                AuthorizationReason: authorizationReason,
                ReplaceExisting: true);

            TrustedBaselineResult result = repairService.EstablishTrustedBaselineAsync(request)
                .GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                _logService!.LogEvent(LogEventKind.Exception, "Installer integrity provisioning failed",
                    new Dictionary<string, object?>
                    {
                        ["ApplicationRoot"] = applicationRoot,
                        ["Message"] = result.Message,
                    });
                return 21;
            }

            StartupIntegrityReport verification = repairService.VerifyStartupAsync().GetAwaiter().GetResult();
            bool verified = verification.ManifestTrusted && verification.IsValid;
            _logService!.LogEvent(
                verified ? LogEventKind.UserAction : LogEventKind.Exception,
                verified
                    ? "Installer integrity baseline provisioned and verified"
                    : "Installer integrity baseline failed post-provision verification",
                new Dictionary<string, object?>
                {
                    ["ApplicationRoot"] = applicationRoot,
                    ["ProtectedFileCount"] = result.ProtectedFileCount,
                    ["ManifestTrusted"] = verification.ManifestTrusted,
                    ["HighRiskFailureCount"] = verification.HighRiskFailureCount,
                    ["LowRiskFailureCount"] = verification.LowRiskFailureCount,
                });
            return verified ? 0 : 22;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             System.Security.Cryptography.CryptographicException or
                                             ArgumentException or InvalidOperationException)
        {
            _logService?.LogException(exception, "Program.ProvisionTrustedIntegrityBaseline");
            return 23;
        }
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLeslieCleanerCore();
        services.AddLeslieCleanerInfrastructure();
        services.AddLeslieCleanerAiModule();
        RegisterLazyInterfaceServices(services);

        // UI 层服务与全部页面视图模型（引擎经 ILazyModuleActivator 延迟加载）
        // ILogService 包装为事件分接头：History/Notification Center 订阅既有事件流
        services.AddSingleton<AppEventTap>(static provider => new AppEventTap(
            new SerilogLogService(provider.GetRequiredService<ILoggingBootstrapper>().Bootstrap())));
        services.AddSingleton<ILogService>(static provider => provider.GetRequiredService<AppEventTap>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<RegistryElevationResumeStore>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<SystemContextSnapshotProvider>();
        services.AddSingleton(static provider =>
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeslieCleaner");
            return new NotificationService(Path.Combine(baseDir, "Notifications"));
        });
        services.AddSingleton(static provider => new SearchService(
            () => provider.GetRequiredService<MainViewModel>().NavigationItems
                .Select(i => (i.Title, i.Icon)).ToList(),
            () => UninstallerViewModel.CachedAppsSnapshot));
        services.AddSingleton<ILazyModuleActivator, LazyModuleActivator>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<INavigationService>(static provider =>
            provider.GetRequiredService<MainViewModel>());
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SmartScanViewModel>();
        services.AddTransient<RegistryViewModel>();
        services.AddTransient<UninstallerViewModel>();
        services.AddTransient<StartupViewModel>();
        services.AddTransient<PrivacyViewModel>();
        services.AddTransient<ShredderViewModel>();
        services.AddTransient<DuplicatesViewModel>();
        services.AddTransient<LargeFilesViewModel>();
        services.AddTransient<ToolboxViewModel>();
        services.AddTransient<PlatformCenterViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    /// <summary>
    /// 以 Core 接口为键的懒工厂注册：注册路径只引用 Core 类型，
    /// 实现类型按程序集+类型名在首次解析时加载（模块程序集首次使用时才入内存）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    private static void RegisterLazyInterfaceServices(IServiceCollection services)
    {
        services.TryAddSingleton<IDatabase>(static provider =>
        {
            var pathService = provider.GetRequiredService<IPathService>();
            string databaseFile = Path.Combine(
                pathService.EnsureDirectory(pathService.DataDirectory), "LeslieCleaner.db");
            return CreateLazy<IDatabase>(provider, "LeslieCleaner.Data.dll",
                "LeslieCleaner.Data.SqliteDatabase", databaseFile);
        });

        services.TryAddSingleton<ICleanEngine>(static provider => CreateLazy<ICleanEngine>(provider,
            "LeslieCleaner.Modules.Cleaner.dll", "LeslieCleaner.Modules.Cleaner.CleanEngine"));

        services.TryAddSingleton<IBrowserPrivacyCleaner>(static provider => CreateLazy<IBrowserPrivacyCleaner>(provider,
            "LeslieCleaner.Modules.Browser.dll", "LeslieCleaner.Modules.Browser.BrowserCleanerEngine"));

        services.TryAddSingleton<IResidualScanner>(static provider => CreateLazy<IResidualScanner>(provider,
            "LeslieCleaner.Modules.Uninstaller.dll", "LeslieCleaner.Modules.Uninstaller.Residual.ResidualScannerEngine"));
    }

    /// <summary>延迟加载程序集并构造类型（构造函数依赖由容器注入）。</summary>
    /// <typeparam name="T">返回类型。</typeparam>
    /// <param name="provider">服务容器。</param>
    /// <param name="assemblyFile">程序集文件名。</param>
    /// <param name="typeName">全名类型名。</param>
    /// <param name="explicitArgs">显式构造参数。</param>
    public static T CreateLazy<T>(IServiceProvider provider, string assemblyFile, string typeName,
        params object[] explicitArgs)
    {
        var assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, assemblyFile));
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        return (T)ActivatorUtilities.CreateInstance(provider, type, explicitArgs);
    }
}

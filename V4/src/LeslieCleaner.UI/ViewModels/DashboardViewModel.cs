using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Modules.Cleaner.Scanning;
using LeslieCleaner.Modules.Privacy;
using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.UI.Converters;
using LeslieCleaner.UI.Services;
using System.Collections.ObjectModel;

namespace LeslieCleaner.UI.ViewModels;

/// <summary>首页四卡片之一。</summary>
public sealed partial class DashboardCard : ObservableObject
{
    /// <summary>标题。</summary>
    public string Title { get; }

    /// <summary>副标题。</summary>
    public string Detail { get; }

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _stateBrushKey;

    /// <summary>以标题、初始值、副标题与状态画刷键创建卡片。</summary>
    public DashboardCard(string title, string value, string detail, string stateBrushKey)
    {
        Title = title;
        Detail = detail;
        _value = value;
        _stateBrushKey = stateBrushKey;
    }
}

/// <summary>
/// 首页视图模型。健康评分圆环（AI Optimizer 真实评分）、上次扫描时间、
/// 四卡片（存储/隐私/启动项/系统性能）。指标采集全部在后台线程，UI 线程零 IO。
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IPageActivatable
{
    private readonly ILazyModuleActivator _activator;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly IFavoriteService _favoriteService;
    private readonly IRecentService _recentService;

    /// <summary>缓存的仪表盘指标配置键（上次评估结果，启动时零采集直接展示）。</summary>
    public const string CachedMetricsConfigKey = "ui:dashboardMetrics";

    private SmartScanEngine? _smartScanEngine;
    private StartupManagerEngine? _startupManagerEngine;
    private PrivacyGuardEngine? _privacyGuardEngine;
    private IRegistryScanner? _registryScanner;

    private SmartScanEngine SmartScanEngine => _smartScanEngine ??=
        _activator.Create<SmartScanEngine>(ModuleRefs.CleanerAssembly, ModuleRefs.SmartScanEngineType,
            _activator.Create<IScanEnvironment>(ModuleRefs.CleanerAssembly, ModuleRefs.SystemScanEnvironmentType));

    private IRegistryScanner RegistryScanner => _registryScanner ??= _activator.Resolve<IRegistryScanner>();

    private StartupManagerEngine StartupManagerEngine => _startupManagerEngine ??=
        _activator.Create<StartupManagerEngine>(ModuleRefs.StartupAssembly, ModuleRefs.StartupManagerEngineType);

    private PrivacyGuardEngine PrivacyGuardEngine => _privacyGuardEngine ??=
        _activator.Create<PrivacyGuardEngine>(ModuleRefs.PrivacyAssembly, ModuleRefs.PrivacyGuardEngineType);

    [ObservableProperty]
    private int _healthScore;

    [ObservableProperty]
    private string _scoreGrade = "评估中…";

    [ObservableProperty]
    private string _lastScanText = "尚未扫描";

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>四卡片。</summary>
    public DashboardCard StorageCard { get; } = new("存储空间", "…", "系统盘", "SuccessBrush");

    /// <summary>隐私卡片。</summary>
    public DashboardCard PrivacyCard { get; } = new("隐私风险", "…", "检测中", "SuccessBrush");

    /// <summary>启动项卡片。</summary>
    public DashboardCard StartupCard { get; } = new("启动项", "…", "检测中", "SuccessBrush");

    /// <summary>性能卡片。</summary>
    public DashboardCard PerformanceCard { get; } = new("系统性能", "…", "检测中", "SuccessBrush");

    /// <summary>收藏列表，随 <see cref="IFavoriteService"/> 初始化。</summary>
    public ObservableCollection<FavoriteItem> Favorites { get; }

    /// <summary>最近使用列表，随 <see cref="IRecentService"/> 初始化。</summary>
    public ObservableCollection<RecentItem> RecentItems { get; }

    /// <summary>以懒激活器、导航服务、配置服务、日志服务与收藏/最近服务创建首页视图模型。</summary>
    public DashboardViewModel(
        ILazyModuleActivator activator,
        INavigationService navigationService,
        IConfigService configService,
        ILogService logService,
        IFavoriteService favoriteService,
        IRecentService recentService)
    {
        _activator = activator;
        _configService = configService;
        _navigationService = navigationService;
        _logService = logService;
        _favoriteService = favoriteService;
        _recentService = recentService;

        Favorites = new ObservableCollection<FavoriteItem>(favoriteService.GetFavorites());
        RecentItems = new ObservableCollection<RecentItem>(recentService.GetRecent(5));
    }

    private readonly INavigationService _navigationService;

    /// <summary>快捷操作：跳转到对应页面。</summary>
    /// <param name="pageTitle">目标页面标题。</param>
    [RelayCommand]
    private void QuickAction(string? pageTitle)
    {
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            _navigationService.NavigateTo(pageTitle);
        }
    }

    /// <summary>后台采集真实指标并刷新评分与卡片。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            SystemMetrics metrics = await Task.Run(async () =>
            {
                ScanResult scan = await SmartScanEngine.ScanAsync(new ScanOptions { Targets = ["*"] });
                IReadOnlyList<RegistryFinding> findings = await RegistryScanner.ScanAsync();
                IReadOnlyList<StartupItem> startupItems = await StartupManagerEngine.EnumerateAsync();
                IReadOnlyList<PrivacyItem> privacyItems = await PrivacyGuardEngine.ScanAsync();

                var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(
                    Environment.SpecialFolder.System))!);
                double freePercent = systemDrive.AvailableFreeSpace * 100.0 / systemDrive.TotalSize;
                int highImpact = startupItems.Count(static i => i is { Enabled: true, Impact: StartupImpact.High });
                return new SystemMetrics(
                    scan.TotalSizeBytes, highImpact, findings.Count, freePercent,
                    privacyItems.Count,
                    scan.Items.Count > 0 ? scan.Items.Max(static i => i.SizeBytes) : 0);
            }).ConfigureAwait(false);

            AiAnalysisReport report = HealthScoreEngine.Analyze(metrics);
            ApplyMetrics(metrics, report.Score, DateTimeOffset.Now);

            // 缓存本次评估：下次启动零采集直接展示（延迟采集策略）
            var cache = new DashboardMetricsCache(
                metrics.JunkBytes, metrics.HighImpactStartupCount, metrics.RegistryIssueCount,
                metrics.DiskFreePercent, metrics.PrivacyItemCount, metrics.TopLargeFileBytes,
                DateTimeOffset.UtcNow);
            await _configService.SetValueAsync(CachedMetricsConfigKey, cache).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "DashboardViewModel.RefreshAsync");
            ScoreGrade = "评估失败（详见日志）";
        }
        finally
        {
            IsBusy = false;
            // 指标采集产生大量瞬态对象（数千扫描项），主动回收避免空闲内存虚高
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
        }
    }

    /// <summary>
    /// 页面激活：仅展示上次缓存的评估结果（不触发采集）。
    /// 全量指标采集只在用户显式点击"刷新评估"时执行——启动期零扫描、零引擎实例化。
    /// </summary>
    public Task OnActivatedAsync()
    {
        _recentService.Record("Dashboard", "home");

        DashboardMetricsCache? cache = _configService.GetValue(CachedMetricsConfigKey, (DashboardMetricsCache?)null);
        if (cache is not null)
        {
            var metrics = new SystemMetrics(
                cache.JunkBytes, cache.HighImpactStartupCount, cache.RegistryIssueCount,
                cache.DiskFreePercent, cache.PrivacyItemCount, cache.TopLargeFileBytes);
            ApplyMetrics(metrics, HealthScoreEngine.ComputeScore(metrics), cache.EvaluatedUtc.LocalDateTime);
        }
        else
        {
            ScoreGrade = "点击刷新评估";
        }

        return Task.CompletedTask;
    }

    private void ApplyMetrics(SystemMetrics metrics, int score, DateTimeOffset evaluatedLocal)
    {
        HealthScore = score;
        ScoreGrade = score >= 90 ? "优秀" : score >= 75 ? "良好" : score >= 60 ? "一般" : "较差";
        LastScanText = $"上次扫描：{evaluatedLocal:yyyy-MM-dd HH:mm}";

        StorageCard.Value = $"{metrics.DiskFreePercent:F0}%";
        StorageCard.StateBrushKey = metrics.DiskFreePercent >= 20 ? "SuccessBrush" : "DangerBrush";
        PerformanceCard.Value = score >= 75 ? "优秀" : "待优化";
        PerformanceCard.StateBrushKey = score >= 75 ? "SuccessBrush" : "WarningBrush";
        StartupCard.Value = $"{metrics.HighImpactStartupCount}";
        StartupCard.StateBrushKey = metrics.HighImpactStartupCount == 0 ? "SuccessBrush" : "WarningBrush";
        PrivacyCard.Value = metrics.PrivacyItemCount == 0 ? "低风险" : $"{metrics.PrivacyItemCount} 项";
        PrivacyCard.StateBrushKey = metrics.PrivacyItemCount == 0 ? "SuccessBrush" : "WarningBrush";
    }

    /// <summary>仪表盘指标缓存（配置持久化模型）。</summary>
    public sealed record DashboardMetricsCache(
        long JunkBytes,
        int HighImpactStartupCount,
        int RegistryIssueCount,
        double DiskFreePercent,
        int PrivacyItemCount,
        long TopLargeFileBytes,
        DateTimeOffset EvaluatedUtc);
}

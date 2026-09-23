using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.AI;

public sealed class AiOptimizerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly MaintenancePlanner _planner;

    public AiOptimizerTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _planner = new MaintenancePlanner(_config, _workspace.Log);
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public void Score_PerfectSystem_Is100()
    {
        var metrics = new SystemMetrics(0, 0, 0, 60, 0, 0);

        Assert.Equal(100, HealthScoreEngine.ComputeScore(metrics));
    }

    [Fact]
    public void Score_HeavyPenalties()
    {
        // 垃圾100GB→-25(封顶)；10高影响→-20(封顶)；500注册表→-15(封顶)；剩余2%→-18；1000隐私→-15(封顶)
        var metrics = new SystemMetrics(
            JunkBytes: 100L * 1024 * 1024 * 1024, HighImpactStartupCount: 10,
            RegistryIssueCount: 500, DiskFreePercent: 2, PrivacyItemCount: 1000,
            TopLargeFileBytes: 0);

        Assert.Equal(7, HealthScoreEngine.ComputeScore(metrics));
    }

    [Fact]
    public void Score_ExtremePenalties_FloorsAt0()
    {
        var metrics = new SystemMetrics(
            JunkBytes: 100L * 1024 * 1024 * 1024, HighImpactStartupCount: 10,
            RegistryIssueCount: 500, DiskFreePercent: 0, PrivacyItemCount: 1000,
            TopLargeFileBytes: 0);

        Assert.Equal(5, HealthScoreEngine.ComputeScore(metrics)); // 磁盘 0% → -20(未封顶 25)
    }

    [Fact]
    public void Score_PartialPenalties()
    {
        // 垃圾 2GB → -10；3 高影响 → -12；50 注册表 → -5；剩余 15% → -5；40 隐私 → -2；合计 66
        var metrics = new SystemMetrics(
            2L * 1024 * 1024 * 1024, 3, 50, 15, 40, 0);

        Assert.Equal(66, HealthScoreEngine.ComputeScore(metrics));
    }

    [Fact]
    public void Suggestions_CiteRealNumbers()
    {
        var metrics = new SystemMetrics(
            5L * 1024 * 1024 * 1024, 4, 0, 12, 30, 6L * 1024 * 1024 * 1024);
        AiAnalysisReport report = HealthScoreEngine.Analyze(metrics);

        Assert.Contains("4", report.StartupSuggestions[0]);
        Assert.Contains("30", report.PrivacySuggestions[0]);
        Assert.Contains(report.StorageSuggestions, s => s.Contains("5.0 GB"));
        Assert.Contains(report.StorageSuggestions, s => s.Contains("12.0%"));
    }

    [Fact]
    public void Suggestions_HealthySystem_PositiveText()
    {
        var metrics = new SystemMetrics(0, 0, 0, 55, 0, 0);
        AiAnalysisReport report = HealthScoreEngine.Analyze(metrics);

        Assert.Equal(100, report.Score);
        Assert.Contains("良好", report.StartupSuggestions[0]);
        Assert.Contains("干净", report.PrivacySuggestions[0]);
        Assert.Contains("健康", report.StorageSuggestions[0]);
    }

    [Fact]
    public void IsPlanDue_Logic()
    {
        var plan = new MaintenancePlan("每日维护", 1, ["lesliecleaner.cleaner"], Enabled: true, null);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.True(MaintenancePlanner.IsPlanDue(plan, now));
        Assert.True(MaintenancePlanner.IsPlanDue(plan with { LastRunUtc = now.AddDays(-2) }, now));
        Assert.False(MaintenancePlanner.IsPlanDue(plan with { LastRunUtc = now.AddHours(-2) }, now));
        Assert.False(MaintenancePlanner.IsPlanDue(plan with { Enabled = false }, now));
    }

    [Fact]
    public async Task Planner_SaveGetMarkExecuted()
    {
        var plans = new List<MaintenancePlan>
        {
            new("每周清理", 7, ["lesliecleaner.cleaner"], true, null),
        };
        await _planner.SavePlansAsync(plans);

        Assert.Single(_planner.GetDuePlans(DateTimeOffset.UtcNow));

        await _planner.MarkExecutedAsync("每周清理", DateTimeOffset.UtcNow);

        Assert.Empty(_planner.GetDuePlans(DateTimeOffset.UtcNow));
        Assert.NotNull(_planner.GetPlans()[0].LastRunUtc);
    }
}

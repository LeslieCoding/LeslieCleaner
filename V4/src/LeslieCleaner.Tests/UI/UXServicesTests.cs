using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Tests.Support;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.ViewModels;

namespace LeslieCleaner.Tests.UI;

/// <summary>
/// UX 服务测试（精简范围）：通知中心未读计数与事件流喂数、全局搜索提供者命中。
/// </summary>
public sealed class UXServicesTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Notification_UnreadCount_AddAndMarkAllRead()
    {
        var notifications = new NotificationService(Path.Combine(_workspace.RootDirectory, "Notify"));

        notifications.Add(new AppNotification { Title = "扫描完成", Body = "Smart Scan 0.47s", Kind = "Scan" });
        notifications.Add(new AppNotification { Title = "错误", Body = "x", Kind = "Error" });

        Assert.Equal(2, notifications.UnreadCount);
        Assert.Equal("错误", notifications.Notifications[0].Title); // 最新在前

        notifications.MarkAllRead();

        Assert.Equal(0, notifications.UnreadCount);
        Assert.All(notifications.Notifications, n => Assert.True(n.IsRead));
    }

    [Fact]
    public void EventTap_NotificationAutoFeed_KindsMapped()
    {
        var tap = new AppEventTap(_workspace.Log);
        var notifications = new NotificationService(Path.Combine(_workspace.RootDirectory, "Notify"));
        notifications.Attach(tap);

        tap.LogEvent(LogEventKind.ScanFinish, "Smart Scan 完成");
        tap.LogEvent(LogEventKind.DeleteFinish, "清理完成");
        tap.LogEvent(LogEventKind.RestoreFinish, "回滚完成");
        tap.LogEvent(LogEventKind.Exception, "某个错误");

        Assert.Equal(4, notifications.Notifications.Count);
        Assert.Equal(4, notifications.UnreadCount);
        Assert.Equal("错误", notifications.Notifications[0].Title);
        Assert.Equal("恢复完成", notifications.Notifications[1].Title);
        Assert.Equal("清理完成", notifications.Notifications[2].Title);
        Assert.Equal("扫描完成", notifications.Notifications[3].Title);
    }

    [Fact]
    public void Search_KeywordHitsModuleAndSettings()
    {
        var installed = new InstalledApp(
            "win32:adobe-reader", "Adobe Reader", "24.1", "Adobe Inc.", 0, null,
            @"C:\Program Files\Adobe\Reader", null, AppSourceType.Win32, null, null);
        var search = new SearchService(
            () => [("首页", "home"), ("Smart Scan", "zap"), ("启动项管理", "rocket")],
            () => [installed]);

        IReadOnlyList<SearchResultItem> moduleHits = search.Search("启动");
        Assert.Contains(moduleHits, r => r.Kind == "功能" && r.Payload == "启动项管理");

        IReadOnlyList<SearchResultItem> settingHits = search.Search("主题");
        Assert.Contains(settingHits, r => r.Kind == "设置");

        IReadOnlyList<SearchResultItem> adviceHits = search.Search("建议");
        Assert.Contains(adviceHits, r => r.Kind == "建议");

        Assert.Contains(search.Search("Adobe-Reader"), r => r.Kind == "软件" && r.Title == "Adobe Reader");
        Assert.Contains(search.Search("Adobe Inc"), r => r.Kind == "软件" && r.Title == "Adobe Reader");
        Assert.Contains(search.Search("program reader"), r => r.Kind == "软件" && r.Title == "Adobe Reader");

        Assert.Empty(search.Search("   "));
        Assert.Empty(search.Search("Adobe", maxPerKind: 0));
        Assert.Empty(search.Search("不存在的页面xyz"));
    }

    [Fact]
    public void RegistryFindingRow_DefaultSelectionHonorsRepairPolicy()
    {
        var safe = new RegistryFinding(
            @"HKCU\Software\LeslieCleaner.Tests", null, RegistryIssueKind.InvalidUninstallEntry,
            RegistryRiskLevel.Safe, "safe", @"C:\missing.exe");
        var critical = new RegistryFinding(
            @"HKLM\SYSTEM\LeslieCleaner.Tests", null, RegistryIssueKind.InvalidPathValue,
            RegistryRiskLevel.Critical, "protected", @"C:\missing.exe")
        {
            IsSystemProtected = true,
        };

        var safeRow = new RegistryFindingRow(safe);
        var criticalRow = new RegistryFindingRow(critical);

        Assert.True(safeRow.IsChecked);
        Assert.False(criticalRow.IsChecked);
        criticalRow.IsChecked = true;
        Assert.False(criticalRow.IsChecked);
    }
}

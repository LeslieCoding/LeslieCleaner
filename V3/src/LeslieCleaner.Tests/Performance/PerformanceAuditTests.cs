using LeslieCleaner.Core;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure;
using LeslieCleaner.UI.Controls;
using LeslieCleaner.UI.Services;
using LeslieCleaner.UI.ViewModels;
using LeslieCleaner.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.Tests.Performance;

/// <summary>
/// 性能/泄漏回归测试：ViewModel 离开后可 GC、进度节流、图标缓存去重。
/// </summary>
public sealed class PerformanceAuditTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLeslieCleanerCore();
        services.AddLeslieCleanerInfrastructure();
        services.AddLeslieCleanerData();
        services.AddSingleton<ILazyModuleActivator, LazyModuleActivator>();
        services.AddSingleton<DialogService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ViewModel_AfterNavigationAway_IsGarbageCollectible()
    {
        WeakReference reference = CreateAndAbandonViewModel();

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        Assert.False(reference.IsAlive, "离开导航后 ViewModel 应可被 GC 回收（页缓存已移除）");
    }

    private static WeakReference CreateAndAbandonViewModel()
    {
        var services = new ServiceCollection();
        services.AddLeslieCleanerCore();
        services.AddLeslieCleanerInfrastructure();
        services.AddLeslieCleanerData();
        services.AddSingleton<ILazyModuleActivator, LazyModuleActivator>();
        services.AddSingleton<DialogService>();
        using ServiceProvider provider = services.BuildServiceProvider();

        var activator = provider.GetRequiredService<ILazyModuleActivator>();
        var cleanEngine = Program_CreateCleanEngine(provider);
        var vm = new SmartScanViewModel(
            activator,
            cleanEngine,
            provider.GetRequiredService<DialogService>(),
            provider.GetRequiredService<ILogService>());
        WeakReference reference = new(vm);
        Assert.NotNull(vm);
        return reference;
    }

    private static ICleanEngine Program_CreateCleanEngine(IServiceProvider provider) =>
        AppFactory.CreateCleanEngine(provider);

    [Fact]
    public void ThrottledProgress_CoalescesHighFrequencyReports()
    {
        var received = new List<int>();
        var throttled = new ThrottledProgress<int>(new SyncProgress<int>(received.Add),
            TimeSpan.FromMilliseconds(100));

        for (int i = 0; i < 200; i++)
        {
            throttled.Report(i);
        }

        Assert.True(received.Count < 20, $"200 次高频报告应被合并（实际透传 {received.Count} 次）");
        Assert.Equal(0, received[0]);

        throttled.Flush();
        Assert.Equal(199, received[^1]);
    }

    /// <summary>同步执行的 IProgress（Progress&lt;T&gt; 会投递到捕获的同步上下文，测试断言前可能未落地）。</summary>
    private sealed class SyncProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }

    [Fact]
    public void IconCache_SamePath_ReturnsSameFrozenInstance()
    {
        string exe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        System.Windows.Media.ImageSource? first = StartupIconService.GetIcon(exe);
        System.Windows.Media.ImageSource? second = StartupIconService.GetIcon(exe);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.True(first!.IsFrozen);
    }

    [Fact]
    public void IconCache_MissingFile_ReturnsNull()
    {
        Assert.Null(StartupIconService.GetIcon(
            Path.Combine(_workspace.RootDirectory, "ghost.exe")));
    }
}

/// <summary>测试辅助：按 App 懒工厂同款构造 ICleanEngine。</summary>
public static class AppFactory
{
    /// <summary>创建 CleanEngine。</summary>
    /// <param name="provider">服务容器。</param>
    public static ICleanEngine CreateCleanEngine(IServiceProvider provider) =>
        (ICleanEngine)ActivatorUtilities.CreateInstance(
            provider,
            System.Reflection.Assembly.LoadFrom(
                Path.Combine(AppContext.BaseDirectory, "LeslieCleaner.Modules.Cleaner.dll"))
                .GetType("LeslieCleaner.Modules.Cleaner.CleanEngine", throwOnError: true)!);
}

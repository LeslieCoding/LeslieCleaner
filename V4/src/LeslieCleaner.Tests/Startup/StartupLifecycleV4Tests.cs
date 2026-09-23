using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Modules.Startup.Classification;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Startup;

public sealed class StartupLifecycleV4Tests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Scan_UsesSevenDayCache_ForceRefreshAndExpiryAreExplicit()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero));
        var operations = new FakeOperations([CreateItem("cache")]);
        var manager = CreateManager(operations, new FakeTaskController(), clock);

        StartupScanSnapshot first = await manager.ScanAsync();
        StartupScanSnapshot cached = await manager.ScanAsync();
        StartupScanSnapshot forced = await manager.ScanAsync(new StartupScanRequest(true));
        clock.Advance(TimeSpan.FromDays(8));
        StartupScanSnapshot expired = await manager.ScanAsync();

        Assert.False(first.FromCache);
        Assert.True(cached.FromCache);
        Assert.False(forced.FromCache);
        Assert.False(expired.FromCache);
        Assert.Equal(3, operations.ScanCount);
    }

    [Fact]
    public async Task Delay_Success_IsTransactionalIdempotentAndRestorable()
    {
        string executable = _workspace.CreateFile("Apps/tool.exe");
        StartupItem item = CreateItem("delay", $"\"{executable}\" --quiet");
        var operations = new FakeOperations([item]);
        var tasks = new FakeTaskController();
        var manager = CreateManager(operations, tasks);

        DelayedStartupResult result = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds60, true));
        DelayedStartupResult duplicate = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds60, true));
        DelayedStartupResult restored = await manager.RestoreDelayedStartAsync(item.Id);

        Assert.True(result.Success);
        Assert.True(duplicate.AlreadyConfigured);
        Assert.Equal(1, operations.DisableCount);
        Assert.True(restored.Success);
        Assert.True(restored.OriginalEntryRestored);
        Assert.True(operations.Enabled);
        Assert.Empty(await manager.GetDelayedStartsAsync());
    }

    [Fact]
    public async Task Delay_CreateFailure_AutomaticallyRestoresOriginalEntry()
    {
        string executable = _workspace.CreateFile("Apps/failing.exe");
        StartupItem item = CreateItem("failure", $"\"{executable}\"");
        var operations = new FakeOperations([item]);
        var tasks = new FakeTaskController { CreateException = new IOException("scheduler failure") };
        var manager = CreateManager(operations, tasks);

        DelayedStartupResult result = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds30, true));

        Assert.False(result.Success);
        Assert.True(result.OriginalEntryRestored);
        Assert.True(operations.Enabled);
        Assert.Equal(1, operations.EnableCount);
        Assert.Empty(await manager.GetDelayedStartsAsync());
    }

    [Fact]
    public async Task Delay_Timeout_RestoresThenPropagatesCancellation()
    {
        string executable = _workspace.CreateFile("Apps/slow.exe");
        StartupItem item = CreateItem("timeout", $"\"{executable}\"");
        var operations = new FakeOperations([item]);
        var tasks = new FakeTaskController { WaitUntilCancelled = true };
        var manager = CreateManager(operations, tasks);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds30, true,
                TimeSpan.FromMilliseconds(30))));

        Assert.True(operations.Enabled);
        Assert.Equal(1, operations.EnableCount);
    }

    [Theory]
    [InlineData(StartupSourceKind.MicrosoftWindows, DisableProtection.ConfirmSystemCritical)]
    [InlineData(StartupSourceKind.HardwareDriver, DisableProtection.ConfirmHardware)]
    public async Task Delay_ProtectedCategories_AreBlockedWithoutMutation(
        StartupSourceKind category,
        DisableProtection protection)
    {
        StartupItem item = CreateItem("protected", @"C:\Windows\System32\safe.exe");
        var operations = new FakeOperations([item]);
        var manager = CreateManager(operations, new FakeTaskController());
        StartupItemDetail detail = CreateDetail(item, category, protection);

        DelayedStartupResult result = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(detail, StartupDelayOption.Seconds60, true));

        Assert.False(result.Success);
        Assert.Contains(category == StartupSourceKind.MicrosoftWindows ? "UserApplication" : "UserApplication",
            result.Error);
        Assert.Equal(0, operations.DisableCount);
    }

    [Fact]
    public async Task Delay_RequiresExplicitConfirmationAndRejectsControlCharacterInjection()
    {
        string executable = _workspace.CreateFile("Apps/safe.exe");
        StartupItem item = CreateItem("confirm", $"\"{executable}\" --ok");
        var operations = new FakeOperations([item]);
        var manager = CreateManager(operations, new FakeTaskController());

        DelayedStartupResult unconfirmed = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds60, false));
        StartupItem injected = item with { Id = "inject", Command = $"\"{executable}\" --ok\r\n/c calc" };
        DelayedStartupResult unsafeCommand = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(injected), StartupDelayOption.Seconds60, true));

        Assert.False(unconfirmed.Success);
        Assert.Contains("显式确认", unconfirmed.Error);
        Assert.False(unsafeCommand.Success);
        Assert.Contains("控制字符", unsafeCommand.Error);
        Assert.Equal(0, operations.DisableCount);
    }

    [Fact]
    public async Task Cache_IsInvalidatedAfterDelayedStartupMutation()
    {
        string executable = _workspace.CreateFile("Apps/cache-change.exe");
        StartupItem item = CreateItem("cache-change", $"\"{executable}\"");
        var operations = new FakeOperations([item]);
        var manager = CreateManager(operations, new FakeTaskController());
        _ = await manager.ScanAsync();
        _ = await manager.ScanAsync();

        _ = await manager.ConfigureDelayedStartAsync(
            new DelayedStartupRequest(CreateDetail(item), StartupDelayOption.Seconds60, true));
        StartupScanSnapshot afterChange = await manager.ScanAsync();

        Assert.False(afterChange.FromCache);
        Assert.Equal(2, operations.ScanCount);
    }

    [Fact]
    public async Task TrendAndExport_NeverPresentUnavailableDataAsMeasured()
    {
        var operations = new FakeOperations([CreateItem("report")]);
        var manager = CreateManager(operations, new FakeTaskController());
        StartupTrend empty = await manager.GetTrendAsync();
        await manager.ScanAsync();
        await manager.RecordBootObservationAsync(new StartupBootObservation(
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(42), StartupDataQuality.Estimated, "rule model"));
        StartupTrend estimated = await manager.GetTrendAsync();
        string destination = Path.Combine(_workspace.RootDirectory, "startup-report.json");
        string exported = await manager.ExportReportAsync(destination);
        string json = await File.ReadAllTextAsync(exported);

        Assert.Equal(StartupDataQuality.Unavailable, empty.Quality);
        Assert.Contains("未采集或伪造", empty.Explanation);
        Assert.Equal(StartupDataQuality.Estimated, estimated.Quality);
        Assert.Contains("不能视为真实", estimated.Explanation);
        Assert.Contains("Estimated", json);
        Assert.True(File.Exists(destination));
    }

    private StartupLifecycleManager CreateManager(
        FakeOperations operations,
        FakeTaskController taskController,
        FakeClock? clock = null) =>
        new(operations, taskController, _workspace.PathService, _workspace.Log,
            new EnabledStateProbe(() => operations.Enabled),
            clock ?? new FakeClock(DateTimeOffset.UtcNow));

    private static StartupItem CreateItem(string id, string command = @"C:\Apps\tool.exe") =>
        new($"run:HKCU\\Software\\Run:{id}", id, StartupItemKind.RegistryRun,
            @"HKCU\Software\Run", command, "Vendor", true, StartupImpact.High, "test");

    private static StartupItemDetail CreateDetail(
        StartupItem item,
        StartupSourceKind kind = StartupSourceKind.UserApplication,
        DisableProtection protection = DisableProtection.Allowed) =>
        new(item, StartupManagerEngine.ResolveExecutablePath(item), 100,
            new SignatureInfo(true, "Vendor"),
            new StartupClassification(kind, [$"classified as {kind}"], 0.9),
            new StartupImpactEstimate(4, StartupImpactLevel.High, 10, 20, 30, 4),
            "test", null, null, null, protection);

    private sealed class FakeOperations(IReadOnlyList<StartupItem> items) : IStartupEntryOperations
    {
        public int ScanCount { get; private set; }
        public int DisableCount { get; private set; }
        public int EnableCount { get; private set; }
        public bool Enabled { get; set; } = true;
        public StartupChangeResult? DisableResult { get; set; }

        public Task<IReadOnlyList<StartupItem>> EnumerateAsync(
            IProgress<LeslieCleaner.Core.Models.ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanCount++;
            return Task.FromResult(items);
        }

        public Task<StartupChangeResult> DisableAsync(StartupItem item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisableCount++;
            StartupChangeResult result = DisableResult ?? new StartupChangeResult(item.Id, true, null);
            if (result.Success) Enabled = false;
            return Task.FromResult(result);
        }

        public Task<StartupChangeResult> EnableAsync(string itemId,
            CancellationToken cancellationToken = default)
        {
            EnableCount++;
            Enabled = true;
            return Task.FromResult(new StartupChangeResult(itemId, true, null));
        }
    }

    private sealed class EnabledStateProbe(Func<bool> read) : IStartupSystemStateProbe
    {
        public Task<bool> IsEnabledAsync(StartupItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(read());
    }

    private sealed class FakeTaskController : IDelayedStartupTaskController
    {
        private readonly HashSet<string> _tasks = [];
        public Exception? CreateException { get; set; }
        public bool WaitUntilCancelled { get; set; }

        public async Task CreateAsync(DelayedStartupTaskSpec task, CancellationToken cancellationToken)
        {
            if (WaitUntilCancelled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (CreateException is not null) throw CreateException;
            _tasks.Add(task.TaskName);
        }

        public Task DeleteAsync(string taskName, CancellationToken cancellationToken)
        {
            _tasks.Remove(taskName);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string taskName, CancellationToken cancellationToken) =>
            Task.FromResult(_tasks.Contains(taskName));
    }

    private sealed class FakeClock(DateTimeOffset now) : IStartupClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
        public void Advance(TimeSpan timeSpan) => UtcNow += timeSpan;
    }
}
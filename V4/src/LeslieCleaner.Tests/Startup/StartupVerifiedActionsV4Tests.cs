using LeslieCleaner.Modules.Startup;
using LeslieCleaner.Modules.Startup.Classification;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Startup;

public sealed class StartupVerifiedActionsV4Tests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Disable_ReturnsSuccessOnlyAfterSystemStateReread()
    {
        var operations = new FakeOperations();
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log);

        StartupChangeResult result = await manager.ExecuteAsync(
            Detail(Item("verified")), StartupActionKind.Disable, true);

        Assert.True(result.Success);
        Assert.True(result.Verified);
        Assert.False(operations.Enabled);
        Assert.Equal(1, operations.DisableCount);
    }

    [Fact]
    public async Task Disable_LyingExecutor_IsRejectedAndRolledBack()
    {
        var operations = new FakeOperations { LeaveEnabledAfterDisable = true };
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log);

        StartupChangeResult result = await manager.ExecuteAsync(
            Detail(Item("lying")), StartupActionKind.Disable, true);

        Assert.False(result.Success);
        Assert.False(result.Verified);
        Assert.Contains("状态复查", result.Error);
        Assert.Equal(1, operations.EnableCount);
    }

    [Fact]
    public async Task Unauthorized_IsStructuredAsRequiresElevation()
    {
        var operations = new FakeOperations
        {
            DisableResult = new StartupChangeResult("permission", false, "Access is denied"),
        };
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => true), _workspace.Log);

        StartupChangeResult result = await manager.ExecuteAsync(
            Detail(Item("permission")), StartupActionKind.Disable, true);

        Assert.False(result.Success);
        Assert.True(result.RequiresElevation);
        Assert.False(result.Verified);
        Assert.Contains("管理员权限", result.Error);
    }

    [Fact]
    public async Task ElevationRetry_RequiresSecondConfirmationAndStillVerifies()
    {
        var operations = new FakeOperations();
        var broker = new FakeElevationBroker(() => operations.Enabled = false);
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log, broker);
        StartupItemDetail detail = Detail(Item("elevated"));

        StartupChangeResult denied = await manager.RetryWithElevationAsync(
            detail, StartupActionKind.Disable, false);
        int callsAfterDenied = broker.CallCount;
        StartupChangeResult completed = await manager.RetryWithElevationAsync(
            detail, StartupActionKind.Disable, true);

        Assert.False(denied.Success);
        Assert.Equal(0, callsAfterDenied);
        Assert.True(completed.Success);
        Assert.True(completed.Verified);
        Assert.Equal(1, broker.CallCount);
        Assert.True(broker.LastRequest!.ExpiresUtc > DateTimeOffset.UtcNow);
        Assert.True(broker.LastRequest.ExpiresUtc <= DateTimeOffset.UtcNow.AddMinutes(3));
    }

    [Fact]
    public async Task ProtectedMicrosoftItem_IsPreciselyBlockedAndNeverElevated()
    {
        var operations = new FakeOperations();
        var broker = new FakeElevationBroker(() => operations.Enabled = false);
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log, broker);
        StartupItemDetail protectedItem = Detail(Item("defender"),
            StartupSourceKind.MicrosoftWindows, DisableProtection.ConfirmSystemCritical,
            ["数字签名者为 Microsoft Windows 且签名有效"]);

        StartupChangeResult result = await manager.RetryWithElevationAsync(
            protectedItem, StartupActionKind.Disable, true);

        Assert.False(result.Success);
        Assert.False(result.RequiresElevation);
        Assert.NotNull(result.BlockedReason);
        Assert.Contains("Microsoft/Windows", result.BlockedReason);
        Assert.Contains("数字签名者", result.BlockedReason);
        Assert.Contains("提权也不会绕过", result.Error);
        Assert.Equal(0, operations.DisableCount);
        Assert.Equal(0, broker.CallCount);
    }

    [Fact]
    public async Task ProtectedDriverItem_HasDriverSpecificReason()
    {
        var operations = new FakeOperations();
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log);
        StartupItem driver = Item("driver") with { Kind = StartupItemKind.Driver };

        StartupChangeResult result = await manager.ExecuteAsync(
            Detail(driver, StartupSourceKind.HardwareDriver, DisableProtection.ConfirmHardware,
                ["命中驱动/OEM 厂商名单：intel"]), StartupActionKind.Disable, true);

        Assert.False(result.Success);
        Assert.Contains("驱动启动项受保护", result.BlockedReason);
        Assert.Equal(0, operations.DisableCount);
    }

    [Fact]
    public async Task Delete_IsReversibleForRegistryButBlockedForService()
    {
        var operations = new FakeOperations();
        var manager = new StartupVerifiedActionManager(operations,
            new StateProbe(() => operations.Enabled), _workspace.Log);

        StartupChangeResult registry = await manager.ExecuteAsync(
            Detail(Item("delete")), StartupActionKind.Delete, true);
        operations.Enabled = true;
        StartupItem service = Item("service") with { Kind = StartupItemKind.Service };
        StartupChangeResult serviceResult = await manager.ExecuteAsync(
            Detail(service), StartupActionKind.Delete, true);

        Assert.True(registry.Success);
        Assert.True(registry.Verified);
        Assert.False(serviceResult.Success);
        Assert.Contains("缺少可完整重建", serviceResult.BlockedReason);
    }

    private static StartupItem Item(string id) =>
        new($"run:HKCU\\Software\\Run:{id}", id, StartupItemKind.RegistryRun,
            @"HKCU\Software\Run", @"C:\Apps\tool.exe", "Vendor", true,
            StartupImpact.Medium, "test");

    private static StartupItemDetail Detail(
        StartupItem item,
        StartupSourceKind kind = StartupSourceKind.UserApplication,
        DisableProtection protection = DisableProtection.Allowed,
        IReadOnlyList<string>? reasons = null) =>
        new(item, StartupManagerEngine.ResolveExecutablePath(item), 1,
            new SignatureInfo(true, "Vendor"),
            new StartupClassification(kind, reasons ?? [$"classified as {kind}"], 0.9),
            new StartupImpactEstimate(3, StartupImpactLevel.Medium, 1, 1, 1, 1),
            "test", null, null, null, protection);

    private sealed class FakeOperations : IStartupEntryOperations
    {
        public bool Enabled { get; set; } = true;
        public bool LeaveEnabledAfterDisable { get; set; }
        public int DisableCount { get; private set; }
        public int EnableCount { get; private set; }
        public StartupChangeResult? DisableResult { get; set; }

        public Task<IReadOnlyList<StartupItem>> EnumerateAsync(
            IProgress<LeslieCleaner.Core.Models.ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StartupItem>>([]);

        public Task<StartupChangeResult> DisableAsync(StartupItem item,
            CancellationToken cancellationToken = default)
        {
            DisableCount++;
            StartupChangeResult result = DisableResult ?? new StartupChangeResult(item.Id, true, null);
            if (result.Success && !LeaveEnabledAfterDisable) Enabled = false;
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

    private sealed class StateProbe(Func<bool> readState) : IStartupSystemStateProbe
    {
        public Task<bool> IsEnabledAsync(StartupItem item,
            CancellationToken cancellationToken = default) => Task.FromResult(readState());
    }

    private sealed class FakeElevationBroker(Action elevate) : IStartupElevationBroker
    {
        public int CallCount { get; private set; }
        public StartupElevationRequest? LastRequest { get; private set; }

        public Task<StartupChangeResult> ExecuteAsync(StartupElevationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            elevate();
            return Task.FromResult(new StartupChangeResult(request.Item.Id, true, null));
        }
    }
}
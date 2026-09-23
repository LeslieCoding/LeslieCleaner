using LeslieCleaner.Tests.Support;
using LeslieCleaner.UI.Services;
using System.Text.Json.Nodes;

namespace LeslieCleaner.Tests.UI;

/// <summary>注册表提权恢复清单只能短时、按随机令牌消费一次。</summary>
public sealed class RegistryElevationResumeStoreTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task SaveAndConsume_RoundTripsTypedTargetsOnlyOnce()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        RegistryResumeTarget[] targets =
        [
            new(@"HKLM\Software\Vendor|Key", null),
            new(@"HKLM\Software\Vendor", "Value|Name"),
        ];

        string token = await store.SaveAsync(targets);
        IReadOnlyList<RegistryResumeTarget> restored = await store.ConsumeAsync(token);

        Assert.Equal(targets, restored);
        Assert.Empty(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Consume_WrongTokenConsumesNothingAndDeletesState()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        string token = await store.SaveAsync([new(@"HKLM\Software\Vendor", null)]);

        Assert.Empty(await store.ConsumeAsync(Guid.NewGuid().ToString("N")));
        Assert.Empty(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Consume_ExpiredOrFutureSnapshotIsRejectedAndDeleted()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        string token = await store.SaveAsync([new(@"HKLM\Software\Vendor", "Value")]);
        string path = ResumeFilePath();
        JsonNode snapshot = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        snapshot["createdUtc"] = DateTimeOffset.UtcNow.AddMinutes(-11);
        await File.WriteAllTextAsync(path, snapshot.ToJsonString());

        Assert.Empty(await store.ConsumeAsync(token));
        Assert.False(File.Exists(path));

        token = await store.SaveAsync([new(@"HKLM\Software\Vendor", "Value")]);
        snapshot = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        snapshot["createdUtc"] = DateTimeOffset.UtcNow.AddMinutes(1);
        await File.WriteAllTextAsync(path, snapshot.ToJsonString());

        Assert.Empty(await store.ConsumeAsync(token));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Consume_MalformedStateIsRejectedAndDeleted()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        string token = await store.SaveAsync([new(@"HKLM\Software\Vendor", null)]);
        string path = ResumeFilePath();
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        Assert.Empty(await store.ConsumeAsync(token));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Consume_ConcurrentCallersCanRestoreAtMostOnce()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        string token = await store.SaveAsync([new(@"HKLM\Software\Vendor", null)]);

        IReadOnlyList<RegistryResumeTarget>[] results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => store.ConsumeAsync(token)));

        Assert.Single(results, static result => result.Count == 1);
        Assert.Equal(7, results.Count(static result => result.Count == 0));
    }

    [Fact]
    public async Task Save_DeduplicatesTypedTargetsAndCapsUntrustedState()
    {
        var store = new RegistryElevationResumeStore(_workspace.PathService);
        RegistryResumeTarget[] targets = Enumerable.Range(0, 250)
            .Select(index => new RegistryResumeTarget($@"HKLM\Software\Vendor\{index}", null))
            .Append(new RegistryResumeTarget(@"HKLM\Software\Vendor\0", null))
            .ToArray();

        string token = await store.SaveAsync(targets);
        IReadOnlyList<RegistryResumeTarget> restored = await store.ConsumeAsync(token);

        Assert.Equal(200, restored.Count);
        Assert.Equal(200, restored.Distinct().Count());
    }

    private string ResumeFilePath() => Path.Combine(
        _workspace.PathService.ConfigDirectory, "registry-elevation-resume.json");

    public void Dispose() => _workspace.Dispose();
}

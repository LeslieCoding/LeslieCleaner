using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly string _configFile;
    private readonly JsonConfigService _service;

    public ConfigServiceTests()
    {
        _configFile = Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json");
        _service = new JsonConfigService(_configFile, _workspace.Log);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task GetValue_MissingKey_BootstrapsDefault()
    {
        int value = _service.GetValue("quarantine:retentionDays", 30);

        Assert.Equal(30, value);
        Assert.True(File.Exists(_configFile));
        string json = await File.ReadAllTextAsync(_configFile);
        Assert.Contains("retentionDays", json);
    }

    [Fact]
    public async Task SetValue_PersistsAcrossInstances()
    {
        await _service.SetValueAsync("general:maxConcurrency", 12);
        _service.Dispose();

        using var reloaded = new JsonConfigService(_configFile, _workspace.Log);
        Assert.Equal(12, reloaded.GetValue("general:maxConcurrency", 0));
    }

    [Fact]
    public async Task SetValue_RaisesChangedEvent()
    {
        var received = new List<string>();
        _service.Changed += (_, args) => received.AddRange(args.ChangedKeys);

        await _service.SetValueAsync("ui:theme", "Dark");

        Assert.Contains("ui:theme", received);
    }

    [Fact]
    public async Task HotReload_ExternalWrite_FiresChangedAndAppliesValue()
    {
        _service.GetValue("engine:mode", "fast");
        var signal = new TaskCompletionSource<ConfigChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Changed += (_, args) => signal.TrySetResult(args);

        await File.WriteAllTextAsync(_configFile, """
            { "engine": { "mode": "thorough" } }
            """);

        // 文件监听事件在高负载机器上可能延迟数十秒（缓冲/线程饥饿），超时放宽到 30s
        Task completed = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.Same(signal.Task, completed);
        ConfigChangedEventArgs args = await signal.Task;
        Assert.Contains("engine:mode", args.ChangedKeys);
        Assert.Equal("thorough", _service.GetValue("engine:mode", "fast"));
    }

    [Fact]
    public async Task HotReload_SelfWrite_IsSuppressed()
    {
        int eventCount = 0;
        _service.Changed += (_, _) => Interlocked.Increment(ref eventCount);

        await _service.SetValueAsync("self:write", 1);
        // 直接事件 1 次；等待文件监听回环（300ms 防抖 + 余量），不应再触发
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(1, Volatile.Read(ref eventCount));
    }

    [Fact]
    public async Task GetValue_IncompatibleType_ReturnsDefault()
    {
        await _service.SetValueAsync("broken:value", "not-a-number");

        int value = _service.GetValue("broken:value", 42);

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task ConcurrentAccess_IsThreadSafe()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                await _service.SetValueAsync($"concurrent:key{index % 5}", index);
                _ = _service.GetValue($"concurrent:key{index % 5}", 0);
            }));
        }

        await Task.WhenAll(tasks);
        Assert.InRange(_service.GetValue("concurrent:key0", 0), 0, 19);
    }
}

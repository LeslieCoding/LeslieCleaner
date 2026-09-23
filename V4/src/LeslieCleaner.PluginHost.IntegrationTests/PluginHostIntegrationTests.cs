using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.PluginSdk;
using LeslieCleaner.SamplePlugin;

namespace LeslieCleaner.PluginHost.IntegrationTests;

public sealed class PluginHostIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LeslieCleaner.PluginHost.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Echo_UsesDedicatedProcess()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("echo.plugin");
        PluginRuntimeResult result = await runtime.ExecuteAsync("echo.plugin", new PluginRuntimeRequest("echo", "{\"value\":42}"));
        Assert.True(result.Success, result.Message);
        Assert.Equal(42, JsonDocument.Parse(result.PayloadJson!).RootElement.GetProperty("value").GetInt32());
        Assert.NotEqual(Environment.ProcessId, result.Telemetry!.ProcessId);
        Assert.True(result.Telemetry.ThreadCount > 0);
    }

    [Fact]
    public async Task Crash_IsIsolatedAndRuntimeRecovers()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("crash.plugin");
        PluginRuntimeResult crashed = await runtime.ExecuteAsync("crash.plugin", new PluginRuntimeRequest("crash"));
        PluginRuntimeResult recovered = await runtime.ExecuteAsync("crash.plugin", new PluginRuntimeRequest("echo", "{\"alive\":true}"));
        Assert.False(crashed.Success);
        Assert.Equal("HOST_CRASHED", crashed.ErrorCode);
        Assert.True(recovered.Success, recovered.Message);
        Assert.True((await runtime.GetTelemetryAsync("crash.plugin"))!.CrashCount >= 1);
    }

    [Fact]
    public async Task Timeout_KillsHost()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("timeout.plugin");
        PluginRuntimeResult result = await runtime.ExecuteAsync("timeout.plugin",
            new PluginRuntimeRequest("hang", Timeout: TimeSpan.FromMilliseconds(350)));
        PluginRuntimeTelemetry telemetry = (await runtime.GetTelemetryAsync("timeout.plugin"))!;
        Assert.False(result.Success);
        Assert.Equal("EXECUTION_TIMEOUT", result.ErrorCode);
        Assert.False(telemetry.IsRunning);
    }

    [Fact]
    public async Task DefaultZeroPermissionToken_DeniesDelete()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("permission.plugin");
        PluginRuntimeResult result = await runtime.ExecuteAsync("permission.plugin", new PluginRuntimeRequest("request-file-delete"));
        Assert.False(result.Success);
        Assert.Equal("PERMISSION_DENIED", result.ErrorCode);
    }

    private async Task<PluginRuntime> CreateRuntimeAsync(string pluginId)
    {
        Directory.CreateDirectory(_root);
        var paths = new TestPaths(_root);
        var log = new TestLog();
        var store = new JsonPluginStateStore(paths);
        var manager = new PluginManager(paths, store, log);
        string package = Path.Combine(_root, "packages", pluginId);
        Directory.CreateDirectory(package);
        string assembly = Path.Combine(package, "LeslieCleaner.SamplePlugin.dll");
        File.Copy(typeof(global::LeslieCleaner.SamplePlugin.SamplePlugin).Assembly.Location, assembly);
        await using FileStream stream = File.OpenRead(assembly);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        await File.WriteAllTextAsync(Path.Combine(package, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = pluginId, name = pluginId, version = "1.0.0", author = "LeslieCleaner", type = "Cleaner",
            apiVersion = PluginApi.Version, entryAssembly = Path.GetFileName(assembly), sha256 = hash,
            permissions = Array.Empty<string>(), dependencies = Array.Empty<object>(),
        }));
        PluginOperationResult install = await manager.InstallAsync(new PluginInstallRequest(package));
        Assert.True(install.Success, install.Message);
        await manager.SetEnabledAsync(pluginId, true);
        string host = Path.Combine(FindV4Root(), "artifacts", "bin", "LeslieCleaner.PluginHost", "Release", "net8.0", "LeslieCleaner.PluginHost.dll");
        return new PluginRuntime(manager, store, log, host, PluginRuntimeLimits.Default with
        {
            DefaultExecutionTimeout = TimeSpan.FromSeconds(5),
            MonitorInterval = TimeSpan.FromMilliseconds(25),
        });
    }

    private static string FindV4Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LeslieCleaner.V4.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("V4 solution root was not found.");
    }

    private sealed class TestPaths : IPathService
    {
        public TestPaths(string root)
        {
            ProgramDataRoot = Path.Combine(root, "ProgramData");
            QuarantineDirectory = Path.Combine(ProgramDataRoot, "Quarantine");
            BackupDirectory = Path.Combine(ProgramDataRoot, "Backups");
            DataDirectory = Path.Combine(ProgramDataRoot, "Data");
            LogDirectory = Path.Combine(root, "Logs");
            ConfigDirectory = Path.Combine(root, "Config");
        }
        public string ProgramDataRoot { get; }
        public string QuarantineDirectory { get; }
        public string BackupDirectory { get; }
        public string DataDirectory { get; }
        public string LogDirectory { get; }
        public string ConfigDirectory { get; }
        public string EnsureDirectory(string path) { Directory.CreateDirectory(path); return path; }
    }

    private sealed class TestLog : ILogService
    {
        public void LogEvent(LogEventKind kind, string message, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void LogException(Exception exception, string context, IReadOnlyDictionary<string, object?>? properties = null) { }
    }
}

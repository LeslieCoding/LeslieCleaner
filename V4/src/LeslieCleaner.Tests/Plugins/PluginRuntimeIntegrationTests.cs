using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.SamplePlugin;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Plugins;

public sealed class PluginRuntimeIntegrationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Execute_Echo_RunsInDedicatedProcessAndReturnsPayload()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("echo.plugin");

        PluginRuntimeResult result = await runtime.ExecuteAsync(
            "echo.plugin",
            new PluginRuntimeRequest("echo", "{\"value\":42}"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(42, JsonDocument.Parse(result.PayloadJson!).RootElement.GetProperty("value").GetInt32());
        Assert.NotNull(result.Telemetry);
        Assert.NotEqual(Environment.ProcessId, result.Telemetry.ProcessId);
        Assert.True(result.Telemetry.IsRunning);
        Assert.True(result.Telemetry.ThreadCount > 0);
    }

    [Fact]
    public async Task Execute_FailFastCrash_DoesNotCrashCallerAndNextInvocationRecovers()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("crash.plugin");

        PluginRuntimeResult crashed = await runtime.ExecuteAsync(
            "crash.plugin",
            new PluginRuntimeRequest("crash", Timeout: TimeSpan.FromSeconds(5)));
        PluginRuntimeResult recovered = await runtime.ExecuteAsync(
            "crash.plugin",
            new PluginRuntimeRequest("echo", "{\"alive\":true}"));

        Assert.False(crashed.Success);
        Assert.Equal("HOST_CRASHED", crashed.ErrorCode);
        Assert.True(recovered.Success, recovered.Message);
        Assert.True(JsonDocument.Parse(recovered.PayloadJson!).RootElement.GetProperty("alive").GetBoolean());
        Assert.True((await runtime.GetTelemetryAsync("crash.plugin"))!.CrashCount >= 1);
    }

    [Fact]
    public async Task Execute_Timeout_TerminatesHostAndRecordsTelemetry()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("timeout.plugin");

        PluginRuntimeResult result = await runtime.ExecuteAsync(
            "timeout.plugin",
            new PluginRuntimeRequest("hang", Timeout: TimeSpan.FromMilliseconds(350)));
        PluginRuntimeTelemetry telemetry = (await runtime.GetTelemetryAsync("timeout.plugin"))!;

        Assert.False(result.Success);
        Assert.Equal("EXECUTION_TIMEOUT", result.ErrorCode);
        Assert.False(telemetry.IsRunning);
        Assert.Equal("EXECUTION_TIMEOUT", telemetry.LastTerminationReason);
    }

    [Fact]
    public async Task Execute_WithDefaultZeroPermissions_DeniesPrivilegedOperation()
    {
        await using PluginRuntime runtime = await CreateRuntimeAsync("permission.plugin");

        PluginRuntimeResult result = await runtime.ExecuteAsync(
            "permission.plugin",
            new PluginRuntimeRequest("request-file-delete"));

        Assert.False(result.Success);
        Assert.Equal("PERMISSION_DENIED", result.ErrorCode);
    }

    private async Task<PluginRuntime> CreateRuntimeAsync(string pluginId)
    {
        var store = new JsonPluginStateStore(_workspace.PathService);
        var manager = new PluginManager(_workspace.PathService, store, _workspace.Log);
        string package = Path.Combine(_workspace.RootDirectory, "packages", pluginId);
        Directory.CreateDirectory(package);
        string sourceAssembly = typeof(global::LeslieCleaner.SamplePlugin.SamplePlugin).Assembly.Location;
        string destinationAssembly = Path.Combine(package, "LeslieCleaner.SamplePlugin.dll");
        File.Copy(sourceAssembly, destinationAssembly);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(destinationAssembly)));
        await File.WriteAllTextAsync(Path.Combine(package, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = pluginId,
            name = pluginId,
            version = "1.0.0",
            author = "LeslieCleaner",
            type = "Cleaner",
            apiVersion = "1.0",
            entryAssembly = Path.GetFileName(destinationAssembly),
            sha256 = hash,
            description = "isolated integration sample",
            permissions = Array.Empty<string>(),
            dependencies = Array.Empty<object>(),
        }));
        PluginOperationResult installed = await manager.InstallAsync(new PluginInstallRequest(package));
        Assert.True(installed.Success, installed.Message);
        await manager.SetEnabledAsync(pluginId, true);

        string host = Path.Combine(
            FindV4Root(),
            "artifacts", "bin", "LeslieCleaner.PluginHost", "Release", "net8.0", "LeslieCleaner.PluginHost.dll");
        var limits = PluginRuntimeLimits.Default with
        {
            StartupTimeout = TimeSpan.FromSeconds(10),
            DefaultExecutionTimeout = TimeSpan.FromSeconds(5),
            MonitorInterval = TimeSpan.FromMilliseconds(25),
        };
        return new PluginRuntime(manager, store, _workspace.Log, host, limits);
    }

    private static string FindV4Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LeslieCleaner.V4.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("V4 solution root was not found from the test output directory.");
    }
}

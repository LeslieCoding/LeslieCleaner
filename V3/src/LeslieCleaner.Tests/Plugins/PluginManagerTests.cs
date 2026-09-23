using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Plugins;

public sealed class PluginManagerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _config = new JsonConfigService(Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _manager = new PluginManager(_workspace.PathService, _config, _workspace.Log);
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Discover_ValidPlugin_IsDisabledByDefaultAndCanBeEnabled()
    {
        CreatePlugin("sample.plugin", "plugin.dll", "safe payload", hashOverride: null);

        PluginDescriptor initial = Assert.Single(await _manager.DiscoverAsync());
        Assert.Equal(PluginTrustStatus.Disabled, initial.TrustStatus);

        await _manager.SetEnabledAsync("sample.plugin", true);

        PluginDescriptor enabled = Assert.Single(await _manager.DiscoverAsync());
        Assert.Equal(PluginTrustStatus.Ready, enabled.TrustStatus);
    }

    [Fact]
    public async Task Discover_HashMismatch_IsRejected()
    {
        CreatePlugin("tampered.plugin", "plugin.dll", "actual payload", hashOverride: new string('0', 64));

        PluginDescriptor plugin = Assert.Single(await _manager.DiscoverAsync());

        Assert.Equal(PluginTrustStatus.HashMismatch, plugin.TrustStatus);
    }

    [Fact]
    public async Task Discover_AssemblyPathEscape_IsRejected()
    {
        CreatePlugin("escape.plugin", "..\\outside.dll", "ignored", hashOverride: new string('0', 64), createAssembly: false);

        PluginDescriptor plugin = Assert.Single(await _manager.DiscoverAsync());

        Assert.Equal(PluginTrustStatus.UnsafePath, plugin.TrustStatus);
    }

    private void CreatePlugin(string id, string assemblyName, string payload, string? hashOverride, bool createAssembly = true)
    {
        string pluginDirectory = Path.Combine(_workspace.PathService.ProgramDataRoot, "Plugins", id);
        Directory.CreateDirectory(pluginDirectory);
        string assemblyPath = Path.Combine(pluginDirectory, assemblyName);
        if (createAssembly)
        {
            File.WriteAllText(assemblyPath, payload);
        }

        string hash = hashOverride ?? Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
        string manifest = JsonSerializer.Serialize(new
        {
            id,
            name = id,
            version = "1.0.0",
            entryAssembly = assemblyName,
            sha256 = hash,
            description = "test",
        });
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), manifest);
    }
}

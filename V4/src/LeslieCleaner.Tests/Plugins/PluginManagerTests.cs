using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Plugins;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Plugins;

public sealed class PluginManagerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _manager = new PluginManager(_workspace.PathService, new JsonPluginStateStore(_workspace.PathService), _workspace.Log);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Install_ValidPackage_PersistsDisabledStateAndAudit()
    {
        string package = CreatePackage("sample.plugin", "1.0.0");
        PluginOperationResult result = await _manager.InstallAsync(new PluginInstallRequest(package));
        Assert.True(result.Success);
        Assert.Equal(PluginTrustStatus.Disabled, Assert.Single(await _manager.DiscoverAsync()).TrustStatus);
        Assert.Contains(await _manager.GetAuditAsync(), item => item.Operation == "Install" && item.Success);
    }

    [Fact]
    public async Task Install_IncompatibleApi_IsRejected()
    {
        string package = CreatePackage("future.plugin", "1.0.0", apiVersion: "2.0");
        PluginOperationResult result = await _manager.InstallAsync(new PluginInstallRequest(package));
        Assert.False(result.Success);
        Assert.Equal(nameof(PluginTrustStatus.IncompatibleApi), result.ErrorCode);
    }

    [Fact]
    public async Task Install_UnapprovedPermission_IsRejected()
    {
        string package = CreatePackage("permission.plugin", "1.0.0", permissions: ["File.Read"]);
        PluginOperationResult result = await _manager.InstallAsync(new PluginInstallRequest(package));
        Assert.False(result.Success);
        Assert.Equal(nameof(PluginTrustStatus.PermissionDenied), result.ErrorCode);
    }

    [Fact]
    public async Task Install_DangerousPermission_RequiresSeparateConfirmation()
    {
        string package = CreatePackage("danger.plugin", "1.0.0", permissions: ["File.Delete"]);
        PluginOperationResult result = await _manager.InstallAsync(new PluginInstallRequest(package, ["File.Delete"]));
        Assert.False(result.Success);
        Assert.Equal("DANGEROUS_PERMISSION_CONFIRMATION_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Install_MissingDependency_IsRejected()
    {
        string package = CreatePackage("dependent.plugin", "1.0.0", dependencies: [new PluginDependency("base.plugin", "1.0.0")]);
        PluginOperationResult result = await _manager.InstallAsync(new PluginInstallRequest(package));
        Assert.False(result.Success);
        Assert.Equal(nameof(PluginTrustStatus.MissingDependency), result.ErrorCode);
    }

    [Fact]
    public async Task Enable_PersistsAcrossManagerInstances()
    {
        await _manager.InstallAsync(new PluginInstallRequest(CreatePackage("state.plugin", "1.0.0")));
        await _manager.SetEnabledAsync("state.plugin", true);
        var second = new PluginManager(_workspace.PathService, new JsonPluginStateStore(_workspace.PathService), _workspace.Log);
        Assert.Equal(PluginTrustStatus.Ready, Assert.Single(await second.DiscoverAsync()).TrustStatus);
    }

    [Fact]
    public async Task Remove_DependencyExists_IsBlocked()
    {
        await _manager.InstallAsync(new PluginInstallRequest(CreatePackage("base.plugin", "1.0.0")));
        await _manager.InstallAsync(new PluginInstallRequest(CreatePackage("dependent.plugin", "1.0.0", dependencies: [new PluginDependency("base.plugin", "1.0.0")])));
        PluginOperationResult result = await _manager.RemoveAsync(new PluginRemovalRequest("base.plugin"));
        Assert.False(result.Success);
        Assert.Equal("PLUGIN_HAS_DEPENDENTS", result.ErrorCode);
    }

    [Fact]
    public async Task Update_NewerPackage_CreatesBackupAndKeepsEnabledState()
    {
        await _manager.InstallAsync(new PluginInstallRequest(CreatePackage("update.plugin", "1.0.0", payload: "old")));
        await _manager.SetEnabledAsync("update.plugin", true);
        PluginOperationResult result = await _manager.UpdateAsync(new PluginInstallRequest(CreatePackage("update.plugin", "1.1.0", payload: "new")));
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(Directory.Exists(result.BackupPath));
        PluginDescriptor installed = Assert.Single(await _manager.DiscoverAsync());
        Assert.Equal("1.1.0", installed.Version);
        Assert.Equal(PluginTrustStatus.Ready, installed.TrustStatus);
    }

    [Fact]
    public async Task Discover_HashMismatch_IsRejectedWithoutExecution()
    {
        string root = Path.Combine(_workspace.PathService.ProgramDataRoot, "Plugins", "tampered.plugin");
        Directory.CreateDirectory(root);
        WriteManifest(root, "tampered.plugin", "1.0.0", "2.0", "plugin.dll", new string('0', 64), [], []);
        File.WriteAllText(Path.Combine(root, "plugin.dll"), "payload");
        Assert.Equal(PluginTrustStatus.IncompatibleApi, Assert.Single(await _manager.DiscoverAsync()).TrustStatus);
    }

    private string CreatePackage(string id, string version, string apiVersion = "1.0", string payload = "payload",
        string[]? permissions = null, PluginDependency[]? dependencies = null)
    {
        string root = Path.Combine(_workspace.RootDirectory, "packages", id + "-" + version + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "plugin.dll"), payload);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        WriteManifest(root, id, version, apiVersion, "plugin.dll", hash, permissions ?? [], dependencies ?? []);
        return root;
    }

    private static void WriteManifest(string root, string id, string version, string apiVersion, string assembly, string hash,
        string[] permissions, PluginDependency[] dependencies)
    {
        File.WriteAllText(Path.Combine(root, "plugin.json"), JsonSerializer.Serialize(new
        {
            id, name = id, version, author = "test", type = "Cleaner", apiVersion,
            entryAssembly = assembly, sha256 = hash, description = "test", permissions, dependencies,
        }));
    }
}

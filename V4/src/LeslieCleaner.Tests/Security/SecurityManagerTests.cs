using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

public sealed class SecurityManagerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly string _applicationPath;

    public SecurityManagerTests()
    {
        _applicationPath = _workspace.CreateFile("app.exe", "trusted-v1");
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Assess_MissingBaselineFailsClosedAndDoesNotTrustCurrentFile()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var manager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log, encryption,
            applicationPath: _applicationPath);

        SecurityAssessment initial = await manager.AssessAsync();
        File.WriteAllText(_applicationPath, "tampered-v2");
        SecurityAssessment tampered = await manager.AssessAsync();

        Assert.False(initial.ApplicationIntegrityValid);
        Assert.False(tampered.ApplicationIntegrityValid);
        Assert.Equal(initial.Score, tampered.Score);
        Assert.False(File.Exists(Path.Combine(_workspace.PathService.DataDirectory, "security-integrity-baseline.json")));
    }

    [Fact]
    public async Task Assess_DifferentInstallationPaths_RemainUntrustedWithoutExplicitBaselines()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        string secondInstallation = _workspace.CreateFile("second-install/app.exe", "trusted-v1");
        var firstManager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log, encryption,
            applicationPath: _applicationPath);
        var secondManager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log, encryption,
            applicationPath: secondInstallation);

        Assert.False((await firstManager.AssessAsync()).ApplicationIntegrityValid);
        Assert.False((await secondManager.AssessAsync()).ApplicationIntegrityValid);

        File.WriteAllText(secondInstallation, "tampered-v2");

        Assert.False((await secondManager.AssessAsync()).ApplicationIntegrityValid);
    }

    [Fact]
    public async Task Assess_TamperedAuditChain_IsReported()
    {
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var manager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log, encryption,
            applicationPath: _applicationPath);
        _ = await manager.AssessAsync();
        string auditPath = Path.Combine(_workspace.PathService.DataDirectory, "security-audit.jsonl");
        File.WriteAllText(auditPath, "{\"timestampUtc\":\"2020-01-01T00:00:00+00:00\",\"eventType\":\"x\",\"message\":\"x\",\"previousHash\":\"\",\"hash\":\"bad\"}\n");

        SecurityAssessment assessment = await manager.AssessAsync();

        Assert.False(assessment.AuditIntegrityValid);
    }

    [Fact]
    public async Task Assess_LegacyAudit_IsMigratedToProtectedSeal()
    {
        var legacyManager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log,
            applicationPath: _applicationPath);
        _ = await legacyManager.AssessAsync();

        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        var protectedManager = new SecurityManager(new FakePluginManager([]), _workspace.PathService, _workspace.Log, encryption,
            applicationPath: _applicationPath);
        SecurityAssessment assessment = await protectedManager.AssessAsync();
        string audit = await File.ReadAllTextAsync(Path.Combine(_workspace.PathService.DataDirectory, "security-audit.jsonl"));

        Assert.True(assessment.AuditIntegrityValid);
        Assert.Contains("Seal", audit, StringComparison.Ordinal);
    }

    private sealed class FakePluginManager(IReadOnlyList<PluginDescriptor> plugins) : IPluginManager
    {
        public Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(plugins);
        public Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PluginOperationResult> InstallAsync(PluginInstallRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PluginOperationResult(false, null, "Not used"));
        public Task<PluginOperationResult> UpdateAsync(PluginInstallRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PluginOperationResult(false, null, "Not used"));
        public Task<PluginOperationResult> RemoveAsync(PluginRemovalRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PluginOperationResult(false, request.PluginId, "Not used"));
        public Task<IReadOnlyList<PluginAuditRecord>> GetAuditAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PluginAuditRecord>>([]);
    }
}


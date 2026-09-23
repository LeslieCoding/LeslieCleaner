using System.Security.Cryptography;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

public sealed class SecurityPlatformV4Tests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void OperationGate_StartsFailSafeAndRequiresFreshHealthyAssessmentForRecovery()
    {
        var gate = new SecurityOperationGateV4();
        Assert.True(gate.EmergencyState.IsReadOnly);
        Assert.False(gate.IsSensitiveOperationAllowed);

        gate.EnterEmergencyReadOnly("core file changed", ["Restore trusted core files.", "Run a fresh assessment."]);
        Assert.Contains("core file", gate.EmergencyState.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.ThrowsAny<Exception>(() => gate.EnsureSensitiveOperationAllowed("delete"));

        gate.Apply(new SecurityAssessment(true, true, true, 0, 0, 100, "healthy", DateTimeOffset.UtcNow));
        Assert.False(gate.EmergencyState.IsReadOnly);
        gate.EnsureSensitiveOperationAllowed("delete");
    }

    [Fact]
    public void PermissionPolicy_DefaultDenyAndExactLeastPrivilegeGrant()
    {
        var policy = new SecurityPermissionPolicy(new ProtectedPathPolicy(_workspace.PathService, _workspace.RootDirectory));
        string allowedDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LeslieCleanerPermissionTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var request = new SecurityPermissionRequest("Cleaner", SecurityPermissionCategory.File,
                SecurityPermissionAction.Read, Path.Combine(allowedDirectory, "one.tmp"), "Scan temporary file");
            Assert.False(policy.Evaluate(request).Allowed);

            policy.ReplaceGrants([new SecurityPermissionGrant("Cleaner", SecurityPermissionCategory.File,
                SecurityPermissionAction.Read, allowedDirectory, IncludeDescendants: true)]);
            Assert.True(policy.Evaluate(request).Allowed);
            Assert.False(policy.Evaluate(request with { Action = SecurityPermissionAction.Delete }).Allowed);
            Assert.False(policy.Evaluate(request with { Subject = "Plugin" }).Allowed);
        }
        finally
        {
            Directory.Delete(allowedDirectory, recursive: true);
        }
    }

    [Fact]
    public void PermissionPolicy_RejectsInsecureNetworkAndAllowsExactHttpsGrant()
    {
        var policy = new SecurityPermissionPolicy(new ProtectedPathPolicy(_workspace.PathService, _workspace.RootDirectory));
        policy.ReplaceGrants([new SecurityPermissionGrant("Updater", SecurityPermissionCategory.Network,
            SecurityPermissionAction.Download, "https://updates.example.test/v4", IncludeDescendants: true)]);

        Assert.False(policy.Evaluate(new SecurityPermissionRequest("Updater", SecurityPermissionCategory.Network,
            SecurityPermissionAction.Download, "http://updates.example.test/v4/package", "Download update")).Allowed);
        Assert.True(policy.Evaluate(new SecurityPermissionRequest("Updater", SecurityPermissionCategory.Network,
            SecurityPermissionAction.Download, "https://updates.example.test/v4/package", "Download update")).Allowed);
        Assert.False(policy.Evaluate(new SecurityPermissionRequest("Updater", SecurityPermissionCategory.Network,
            SecurityPermissionAction.Upload, "https://updates.example.test/v4/package", "Upload data")).Allowed);
    }

    [Fact]
    public void ProtectedPathPolicy_NormalizesTraversalAndBlocksProtectedBoundaries()
    {
        string applicationRoot = Path.Combine(_workspace.RootDirectory, "ApplicationCore");
        Directory.CreateDirectory(applicationRoot);
        var policy = new ProtectedPathPolicy(_workspace.PathService, applicationRoot);

        PathSecurityDecision traversal = policy.Evaluate(Path.Combine(applicationRoot, "sub", "..", "core.dll"));
        PathSecurityDecision configuration = policy.Evaluate(Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"));
        string safe = _workspace.CreateFile("safe/data.txt", "ok");

        Assert.False(traversal.Allowed);
        Assert.Equal(Path.GetFullPath(Path.Combine(applicationRoot, "core.dll")), traversal.NormalizedPath);
        Assert.False(configuration.Allowed);
        Assert.True(policy.Evaluate(safe).Allowed);
    }

    [Fact]
    public async Task AuditStore_RoundTripsUnifiedFieldsAndDetectsTampering()
    {
        var store = new SecurityAuditStore(_workspace.PathService);
        var securityEvent = new SecurityAuditEvent(DateTimeOffset.UtcNow, SecurityAuditEventType.PermissionDenied,
            SecurityAuditLevel.Warning, "Cleaner", "Denied", "No grant", "Request minimum grant");
        await store.AppendAsync(securityEvent);

        SecurityAuditEvent stored = Assert.Single(await store.ReadAsync());
        Assert.Equal(securityEvent, stored);

        string auditPath = Path.Combine(_workspace.PathService.DataDirectory, "security-events-v4.jsonl");
        File.AppendAllText(auditPath, "{\"invalid\":true}" + Environment.NewLine);
        await Assert.ThrowsAsync<CryptographicException>(() => store.ReadAsync());
    }
}

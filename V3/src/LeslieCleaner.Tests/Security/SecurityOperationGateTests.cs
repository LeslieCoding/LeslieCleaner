using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Modules.AI;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

public sealed class SecurityOperationGateTests
{
    [Fact]
    public void Apply_UnhealthyAssessment_BlocksSensitiveOperations()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "application integrity failed", DateTimeOffset.UtcNow));

        Assert.False(gate.IsSensitiveOperationAllowed);
        Assert.Throws<LeslieCleanerException>(() => gate.EnsureSensitiveOperationAllowed("test"));
    }

    [Fact]
    public void Apply_HealthyAssessment_AllowsSensitiveOperations()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(true, true, true, 0, 0, 100, "healthy", DateTimeOffset.UtcNow));

        gate.EnsureSensitiveOperationAllowed("test");
        Assert.True(gate.IsSensitiveOperationAllowed);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task MaintenanceTaskRegistration_SecurityGateBlocked_ReturnsFalseWithoutLaunchingTaskScheduler()
    {
        using var workspace = new TempWorkspace();
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var planner = new MaintenancePlanner(config, workspace.Log, gate);

        bool registered = await planner.RegisterScheduledTaskAsync("C:\\never-launched.exe");

        Assert.False(registered);
    }
}

using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Execution;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
public sealed class UninstallSecurityGateTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Uninstall_SecurityGateBlocked_DoesNotLaunchUninstaller()
    {
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var executor = new UninstallExecutor(_workspace.Log, securityOperationGate: gate);
        var app = new InstalledApp("test", "Test app", null, null, 0, null, null,
            "does-not-exist.exe", AppSourceType.Win32, null, null);

        await Assert.ThrowsAsync<LeslieCleaner.Core.Diagnostics.LeslieCleanerException>(
            () => executor.UninstallOneAsync(app, TimeSpan.FromSeconds(1)));
    }
}

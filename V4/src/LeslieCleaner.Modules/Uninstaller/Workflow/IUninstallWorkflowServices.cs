using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Testable abstraction over official uninstall command execution.</summary>
public interface IUninstallRunner
{
    /// <summary>Runs the application's preferred official uninstall method.</summary>
    Task<UninstallTaskResult> UninstallOneAsync(InstalledApp application, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Testable adapter over the residual scan and reversible cleanup pipeline.</summary>
public interface IResidualCleanupRunner
{
    /// <summary>Finds targeted residuals without default full-disk recursion.</summary>
    Task<IReadOnlyList<ResidualItem>> ScanAsync(ResidualQuery query, CancellationToken cancellationToken = default);

    /// <summary>Cleans caller-approved residuals through the quarantine/backup engine.</summary>
    Task<CleanResult> CleanAsync(IReadOnlyList<ResidualItem> items, CancellationToken cancellationToken = default);
}


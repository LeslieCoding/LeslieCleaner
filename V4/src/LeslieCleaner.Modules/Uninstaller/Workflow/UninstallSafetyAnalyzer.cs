using System.Diagnostics;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Performs fail-safe preflight checks without terminating processes or changing the system.</summary>
public sealed class UninstallSafetyAnalyzer
{
    /// <summary>Checks application existence, command availability, running processes, and force confirmation.</summary>
    public Task<UninstallSafetyAssessment> AnalyzeAsync(
        UninstallWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        InstalledApp app = request.Application;
        var findings = new List<UninstallSafetyFinding>();

        bool hasOfficialMethod = app.Source is AppSourceType.Appx or AppSourceType.Store or AppSourceType.Steam or AppSourceType.Winget ||
            (app.Source == AppSourceType.Msi && !string.IsNullOrWhiteSpace(app.ProductCode)) ||
            !string.IsNullOrWhiteSpace(app.UninstallString);
        if (!hasOfficialMethod && !request.AllowForcedCleanup)
        {
            findings.Add(new UninstallSafetyFinding(
                "NO_UNINSTALL_METHOD", "No official uninstall method is available; forced cleanup was not authorized.",
                UninstallSafetySeverity.Blocking));
        }

        if (!request.UserConfirmed)
        {
            findings.Add(new UninstallSafetyFinding(
                "CONFIRMATION_REQUIRED", "Uninstall requires explicit user confirmation.",
                UninstallSafetySeverity.Blocking));
        }

        if (request.AllowForcedCleanup)
        {
            findings.Add(new UninstallSafetyFinding(
                "FORCED_CLEANUP_WARNING",
                "Forced uninstall may leave incomplete removal; only high-confidence application-owned items may be cleaned.",
                UninstallSafetySeverity.Warning));
        }

        IReadOnlyList<string> processes = FindRunningProcesses(app.InstallLocation, cancellationToken);
        if (processes.Count > 0)
        {
            findings.Add(new UninstallSafetyFinding(
                "APPLICATION_RUNNING",
                $"Close the application before uninstalling: {string.Join(", ", processes)}",
                UninstallSafetySeverity.Blocking));
        }

        bool canProceed = findings.All(static finding => finding.Severity != UninstallSafetySeverity.Blocking);
        return Task.FromResult(new UninstallSafetyAssessment(canProceed, processes, findings));
    }

    private static IReadOnlyList<string> FindRunningProcesses(string? installLocation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Path.IsPathFullyQualified(installLocation))
        {
            return [];
        }

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installLocation)) + Path.DirectorySeparatorChar;
        var names = new List<string>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (path is not null && Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(process.ProcessName);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Access denied is not evidence of ownership; do not turn it into a false positive.
                }
            }
        }

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }
}

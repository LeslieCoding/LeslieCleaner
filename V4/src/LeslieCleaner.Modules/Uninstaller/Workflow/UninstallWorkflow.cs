using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>
/// Coordinates Analyze → Fingerprint → Backup → Official Uninstall → Residual Scan → optional reversible cleanup → Report.
/// It never terminates processes automatically and refuses forced cleanup without explicit confirmation.
/// </summary>
public sealed class UninstallWorkflow
{
    private readonly UninstallSafetyAnalyzer _safetyAnalyzer;
    private readonly ApplicationFingerprintStore _fingerprintStore;
    private readonly IUninstallRunner _uninstallRunner;
    private readonly IResidualCleanupRunner _residualRunner;
    private readonly IBackupService _backupService;
    private readonly ILogService _logService;
    private readonly IApplicationResourceInventory? _resourceInventory;

    /// <summary>Creates a safe uninstall workflow.</summary>
    public UninstallWorkflow(
        UninstallSafetyAnalyzer safetyAnalyzer,
        ApplicationFingerprintStore fingerprintStore,
        IUninstallRunner uninstallRunner,
        IResidualCleanupRunner residualRunner,
        IBackupService backupService,
        ILogService logService,
        IApplicationResourceInventory? resourceInventory = null)
    {
        _safetyAnalyzer = safetyAnalyzer;
        _fingerprintStore = fingerprintStore;
        _uninstallRunner = uninstallRunner;
        _residualRunner = residualRunner;
        _backupService = backupService;
        _logService = logService;
        _resourceInventory = resourceInventory;
    }

    /// <summary>Executes the guarded workflow and returns a complete, restorable report.</summary>
    public async Task<UninstallWorkflowReport> ExecuteAsync(
        UninstallWorkflowRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        DateTimeOffset started = DateTimeOffset.UtcNow;
        Guid operationId = Guid.NewGuid();
        UninstallSafetyAssessment assessment = await _safetyAnalyzer.AnalyzeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!assessment.CanProceed)
        {
            string reason = string.Join("; ", assessment.Findings
                .Where(static finding => finding.Severity == UninstallSafetySeverity.Blocking)
                .Select(static finding => finding.Message));
            _logService.LogEvent(LogEventKind.Exception, "Uninstall preflight blocked", new Dictionary<string, object?>
            {
                ["OperationId"] = operationId,
                ["Application"] = request.Application.Name,
                ["Reason"] = reason,
            });
            throw new InvalidOperationException(reason);
        }

        IReadOnlyList<FingerprintEntry>? platformEntries = request.AdditionalFingerprintEntries;
        if (platformEntries is null && _resourceInventory is not null)
        {
            platformEntries = await _resourceInventory.CollectAsync(request.Application, cancellationToken)
                .ConfigureAwait(false);
        }

        ApplicationFingerprint fingerprint = _fingerprintStore.Create(request.Application, platformEntries);
        await _fingerprintStore.SaveAsync(fingerprint, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Guid> backups = await CreateBackupsAsync(fingerprint, cancellationToken).ConfigureAwait(false);

        _logService.LogEvent(LogEventKind.DeleteStart, "Safe uninstall workflow started", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["Application"] = request.Application.Name,
            ["FingerprintId"] = fingerprint.Id,
            ["BackupCount"] = backups.Count,
        });

        bool hasOfficialMethod = HasOfficialMethod(request.Application);
        UninstallTaskResult commandResult = hasOfficialMethod
            ? await _uninstallRunner.UninstallOneAsync(request.Application, timeout, cancellationToken).ConfigureAwait(false)
            : new UninstallTaskResult(request.Application.Id, request.Application.Name, true, null, "forced-cleanup", false, null);

        if (!commandResult.Success)
        {
            return CompleteReport(operationId, request, started, commandResult.Method, false, false, [], 0,
                backups, fingerprint.Id, [], commandResult.Error);
        }

        var query = new ResidualQuery(request.Application.Publisher, request.Application.Name,
            request.Application.InstallLocation, AllowDeepScan: false);
        IReadOnlyList<ResidualItem> residuals = await _residualRunner.ScanAsync(query, cancellationToken).ConfigureAwait(false);
        long residualBytes = residuals.Sum(static item => Math.Max(0, item.SizeBytes));
        bool forcedCleanup = !hasOfficialMethod && request.AllowForcedCleanup;
        IReadOnlyList<ResidualItem> remaining = residuals;

        if (forcedCleanup)
        {
            ResidualItem[] confirmed = [.. residuals.Where(static item => item.Confidence >= 0.71)];
            if (confirmed.Length > 0)
            {
                CleanResult clean = await _residualRunner.CleanAsync(confirmed, cancellationToken).ConfigureAwait(false);
                var cleanedPaths = clean.Items.Where(static item => item.Success)
                    .Select(static item => item.Item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                remaining = [.. residuals.Where(item => !cleanedPaths.Contains(item.Path))];
                _logService.LogEvent(LogEventKind.UserAction, "Forced residual cleanup completed", new Dictionary<string, object?>
                {
                    ["OperationId"] = operationId,
                    ["CleanOperationId"] = clean.OperationId,
                    ["ApprovedItems"] = confirmed.Length,
                    ["RemainingItems"] = remaining.Count,
                });
            }
        }

        return CompleteReport(operationId, request, started, commandResult.Method, hasOfficialMethod || forcedCleanup,
            forcedCleanup, residuals, residualBytes, backups, fingerprint.Id, remaining, null);
    }

    /// <summary>Explicitly restores every workflow backup in reverse creation order.</summary>
    public async Task RestoreAsync(UninstallWorkflowReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (Guid backupId in report.BackupIds.Reverse())
        {
            await _backupService.RestoreAsync(backupId, RestoreConflictPolicy.Overwrite, cancellationToken)
                .ConfigureAwait(false);
        }

        _logService.LogEvent(LogEventKind.UserAction, "Uninstall workflow backups restored", new Dictionary<string, object?>
        {
            ["OperationId"] = report.OperationId,
            ["BackupCount"] = report.BackupIds.Count,
        });
    }

    private async Task<IReadOnlyList<Guid>> CreateBackupsAsync(ApplicationFingerprint fingerprint, CancellationToken cancellationToken)
    {
        var backupIds = new List<Guid>();
        foreach (FingerprintEntry entry in fingerprint.Entries.Where(static entry => entry.Confidence >= 71))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BackupRecord? record = entry.Type switch
            {
                FingerprintResourceType.Registry => await _backupService.BackupRegistryKeyAsync(entry.Value, cancellationToken).ConfigureAwait(false),
                FingerprintResourceType.File when File.Exists(entry.Value) => await _backupService.BackupFileAsync(
                    entry.Value, BackupType.File, "Pre-uninstall file backup", cancellationToken: cancellationToken).ConfigureAwait(false),
                FingerprintResourceType.Configuration when File.Exists(entry.Value) => await _backupService.BackupFileAsync(
                    entry.Value, BackupType.Configuration, "Pre-uninstall configuration backup", cancellationToken: cancellationToken).ConfigureAwait(false),
                _ => null,
            };
            if (record is null)
            {
                continue;
            }

            BackupVerificationResult verification = await _backupService.VerifyAsync(record.Id, cancellationToken).ConfigureAwait(false);
            if (!verification.IsValid)
            {
                throw new IOException($"Pre-uninstall backup verification failed: {entry.Value}");
            }

            backupIds.Add(record.Id);
        }

        return backupIds;
    }

    private UninstallWorkflowReport CompleteReport(
        Guid operationId, UninstallWorkflowRequest request, DateTimeOffset started, string method,
        bool success, bool forcedCleanup, IReadOnlyList<ResidualItem> residuals, long residualBytes,
        IReadOnlyList<Guid> backups, Guid fingerprintId, IReadOnlyList<ResidualItem> remaining, string? error)
    {
        var report = new UninstallWorkflowReport(operationId, request.Application.Id, request.Application.Name,
            started, DateTimeOffset.UtcNow, method, success, forcedCleanup, residuals.Count, residualBytes,
            backups, fingerprintId, remaining, error);
        _logService.LogEvent(LogEventKind.DeleteFinish, "Safe uninstall workflow completed", new Dictionary<string, object?>
        {
            ["OperationId"] = operationId,
            ["Application"] = request.Application.Name,
            ["Method"] = method,
            ["Success"] = success,
            ["ForcedCleanup"] = forcedCleanup,
            ["ResidualItems"] = residuals.Count,
            ["ResidualBytes"] = residualBytes,
            ["RemainingItems"] = remaining.Count,
            ["RestoreAvailable"] = report.RestoreAvailable,
            ["Error"] = error,
        });
        return report;
    }

    private static bool HasOfficialMethod(InstalledApp application) =>
        application.Source is AppSourceType.Appx or AppSourceType.Store or AppSourceType.Steam or AppSourceType.Winget ||
        (application.Source == AppSourceType.Msi && !string.IsNullOrWhiteSpace(application.ProductCode)) ||
        !string.IsNullOrWhiteSpace(application.UninstallString);
}

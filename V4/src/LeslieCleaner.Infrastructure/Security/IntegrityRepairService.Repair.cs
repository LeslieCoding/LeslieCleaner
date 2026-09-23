using System.Security.Cryptography;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

public sealed partial class IntegrityRepairService
{
    /// <inheritdoc />
    public async Task<IntegrityRepairResult> RepairAsync(IntegrityRecoveryPrerequisites prerequisites,
        bool includeLowRiskConfiguration = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var errors = new List<string>();
            int restored = 0;
            IntegrityManifestDocument manifest;
            try
            {
                manifest = await ReadTrustedManifestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsIntegrityException(exception))
            {
                EnterEmergency("The trusted integrity manifest is missing, corrupt, or untrusted.",
                    ["Restore a trusted signed or explicitly authorized manifest.",
                        "Run startup integrity verification again."]);
                errors.Add(exception.Message);
                StartupIntegrityReport unavailable = CreateUnavailableReport();
                await PersistRecordsAsync(unavailable.Records, cancellationToken).ConfigureAwait(false);
                await AuditAsync(SecurityAuditEventType.IntegrityRepairFailed, SecurityAuditLevel.Critical,
                    "Failed", exception.Message, "Restore a trusted manifest before repair.", cancellationToken)
                    .ConfigureAwait(false);
                return new IntegrityRepairResult(false, false, 0, unavailable, errors, DateTimeOffset.UtcNow);
            }

            StartupIntegrityReport before = await VerifyManifestEntriesAsync(manifest, cancellationToken)
                .ConfigureAwait(false);
            Dictionary<Guid, FileIntegrityRecord> failed = before.Records
                .Where(record => record.Status != FileIntegrityStatus.Valid &&
                    (record.RiskLevel == SecurityRiskLevel.Critical || includeLowRiskConfiguration))
                .ToDictionary(static record => record.Id);

            foreach (IntegrityManifestEntry entry in manifest.Entries.Where(entry => failed.ContainsKey(entry.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string targetPath = ResolveManifestTargetPath(manifest, entry, out string trustedTargetRoot);
                    string backupPath = NormalizeManifestPath(manifest.TrustedBackupRoot, entry.BackupRelativePath);
                    EnsureNoReparsePoint(trustedTargetRoot, targetPath, allowMissingLeaf: true);
                    EnsureNoReparsePoint(manifest.TrustedBackupRoot, backupPath);
                    if (!File.Exists(backupPath))
                        throw new FileNotFoundException("Trusted backup file is missing.", backupPath);
                    string backupHash = await ComputeFileHashAsync(backupPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (!FixedTimeHexEquals(entry.Sha256, backupHash))
                        throw new CryptographicException(
                            $"Trusted backup hash verification failed for '{entry.RelativePath}'.");
                    VerifyOptionalSignature(backupHash, entry.SignatureBase64);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    string temporaryPath = targetPath + $".repair-{Guid.NewGuid():N}.tmp";
                    try
                    {
                        await CopyFileAsync(backupPath, temporaryPath, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        File.Move(temporaryPath, targetPath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                    }
                    restored++;
                }
                catch (Exception exception) when (IsIntegrityException(exception))
                {
                    errors.Add($"{entry.RelativePath}: {exception.Message}");
                }
            }

            StartupIntegrityReport after = await VerifyManifestEntriesAsync(manifest, cancellationToken)
                .ConfigureAwait(false);
            bool recovered = after.IsValid && prerequisites.AllSatisfied && errors.Count == 0;
            if (recovered)
            {
                _operationGate.Apply(new SecurityAssessment(true, true, true, 0, 0, 100,
                    "Trusted repair and every independent recovery condition passed.", DateTimeOffset.UtcNow));
            }
            else if (_emergencyGate.EmergencyState.IsReadOnly || after.HighRiskFailureCount > 0)
            {
                var conditions = BuildRecoveryConditions(after, prerequisites);
                if (conditions.Count == 0) conditions.Add("Resolve repair errors and run a fresh verification.");
                EnterEmergency("Security recovery is incomplete; read-only mode remains active.", conditions);
            }

            SecurityAuditEventType eventType = recovered
                ? SecurityAuditEventType.IntegrityRepairSucceeded : SecurityAuditEventType.IntegrityRepairFailed;
            await AuditAsync(eventType, recovered ? SecurityAuditLevel.Information : SecurityAuditLevel.Critical,
                recovered ? "Recovered" : "Blocked",
                recovered ? $"Restored and verified {restored} protected files."
                    : $"Repair restored {restored} files but one or more recovery conditions failed.",
                recovered ? "No action required." : "Satisfy every reported recovery condition before retrying.",
                cancellationToken).ConfigureAwait(false);
            return new IntegrityRepairResult(recovered, recovered, restored, after, errors, DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static List<string> BuildRecoveryConditions(StartupIntegrityReport report,
        IntegrityRecoveryPrerequisites prerequisites)
    {
        var conditions = new List<string>();
        if (!report.IsValid) conditions.Add("Repair every failed integrity record and pass re-verification.");
        if (!prerequisites.DatabaseIntegrityValid) conditions.Add("Pass database integrity verification.");
        if (!prerequisites.AuditIntegrityValid) conditions.Add("Pass security audit-chain verification.");
        if (!prerequisites.PermissionPolicySafe)
            conditions.Add("Load and validate the least-privilege permission policy.");
        if (!prerequisites.UpdateTrusted)
            conditions.Add("Pass update source, signature, and version trust checks.");
        if (!prerequisites.PluginSecurityValid) conditions.Add("Disable or validate every untrusted plugin.");
        if (!prerequisites.EncryptionHealthy) conditions.Add("Pass encryption and key-ring health checks.");
        return conditions;
    }
}
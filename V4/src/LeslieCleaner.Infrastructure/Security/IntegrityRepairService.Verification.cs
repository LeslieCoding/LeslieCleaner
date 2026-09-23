using System.Security.Cryptography;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

public sealed partial class IntegrityRepairService
{
    private async Task<StartupIntegrityReport> VerifyStartupCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            IntegrityManifestDocument manifest = await ReadTrustedManifestAsync(cancellationToken).ConfigureAwait(false);
            StartupIntegrityReport report = await VerifyManifestEntriesAsync(manifest, cancellationToken)
                .ConfigureAwait(false);
            if (report.HighRiskFailureCount > 0)
            {
                EnterEmergency("High-risk application integrity verification failed.",
                    ["Restore affected files from the trusted backup.", "Re-verify every protected file.",
                        "Pass all independent security recovery checks."]);
            }
            return report;
        }
        catch (Exception exception) when (IsIntegrityException(exception))
        {
            EnterEmergency("The trusted startup integrity manifest is missing, corrupt, or untrusted.",
                ["Restore a trusted signed or explicitly authorized manifest.",
                    "Run startup integrity verification again."]);
            StartupIntegrityReport report = CreateUnavailableReport();
            await PersistRecordsAsync(report.Records, cancellationToken).ConfigureAwait(false);
            await AuditAsync(SecurityAuditEventType.IntegrityFailure, SecurityAuditLevel.Critical, "Rejected",
                exception.Message, "Restore an explicitly trusted manifest; never trust current files implicitly.",
                cancellationToken).ConfigureAwait(false);
            return report;
        }
    }

    private async Task<StartupIntegrityReport> VerifyManifestEntriesAsync(IntegrityManifestDocument manifest,
        CancellationToken cancellationToken)
    {
        var records = new List<FileIntegrityRecord>(manifest.Entries.Count);
        foreach (IntegrityManifestEntry entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileIntegrityRecord record = await VerifyEntryAsync(manifest, entry, cancellationToken)
                .ConfigureAwait(false);
            records.Add(record);
            if (record.Status != FileIntegrityStatus.Valid)
            {
                await AuditAsync(SecurityAuditEventType.IntegrityFailure,
                    record.RiskLevel == SecurityRiskLevel.Critical
                        ? SecurityAuditLevel.Critical : SecurityAuditLevel.Warning,
                    record.Status.ToString(), record.Description,
                    record.RiskLevel == SecurityRiskLevel.Critical
                        ? "Keep sensitive functions disabled and repair from the trusted backup."
                        : "Review the configuration change and explicitly re-establish the baseline only if authorized.",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await PersistRecordsAsync(records, cancellationToken).ConfigureAwait(false);
        int high = records.Count(static record => record.Status != FileIntegrityStatus.Valid &&
            record.RiskLevel == SecurityRiskLevel.Critical);
        int valid = records.Count(static record => record.Status == FileIntegrityStatus.Valid);
        int low = records.Count - valid - high;
        return new StartupIntegrityReport(true, records, high, low, DateTimeOffset.UtcNow);
    }

    private async Task<FileIntegrityRecord> VerifyEntryAsync(IntegrityManifestDocument manifest,
        IntegrityManifestEntry entry, CancellationToken cancellationToken)
    {
        SecurityRiskLevel risk = entry.Kind == IntegrityArtifactKind.Configuration
            ? SecurityRiskLevel.Notice : SecurityRiskLevel.Critical;
        string? actualHash = null;
        long? actualSize = null;
        try
        {
            string path = ResolveManifestTargetPath(manifest, entry, out string trustedRoot);
            EnsureNoReparsePoint(trustedRoot, path, allowMissingLeaf: true);
            if (!File.Exists(path))
                return Record(entry, path, null, null, FileIntegrityStatus.Missing, risk,
                    "Protected file is missing.");
            var info = new FileInfo(path);
            actualSize = info.Length;
            actualHash = await ComputeFileHashAsync(path, cancellationToken).ConfigureAwait(false);
            if (entry.Size != actualSize || !FixedTimeHexEquals(entry.Sha256, actualHash))
                return Record(entry, path, actualHash, actualSize, FileIntegrityStatus.Modified, risk,
                    "Protected file size or SHA-256 hash differs from the trusted baseline.");
            try
            {
                VerifyOptionalSignature(actualHash, entry.SignatureBase64);
            }
            catch (CryptographicException exception)
            {
                return Record(entry, path, actualHash, actualSize, FileIntegrityStatus.SignatureInvalid, risk,
                    exception.Message);
            }
            return Record(entry, path, actualHash, actualSize, FileIntegrityStatus.Valid, SecurityRiskLevel.Safe,
                "SHA-256 hash and optional signature are valid.");
        }
        catch (UnsafeIntegrityPathException exception)
        {
            return Record(entry, entry.RelativePath, actualHash, actualSize, FileIntegrityStatus.UnsafePath,
                SecurityRiskLevel.Critical, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return Record(entry, entry.RelativePath, actualHash, actualSize, FileIntegrityStatus.VerificationFailed,
                SecurityRiskLevel.Critical, exception.Message);
        }
    }

    private static FileIntegrityRecord Record(IntegrityManifestEntry entry, string path, string? actualHash,
        long? actualSize, FileIntegrityStatus status, SecurityRiskLevel risk, string description) =>
        new(entry.Id, path, entry.Kind, entry.Sha256, actualHash, entry.Size, actualSize, status, risk,
            DateTimeOffset.UtcNow, description);
}
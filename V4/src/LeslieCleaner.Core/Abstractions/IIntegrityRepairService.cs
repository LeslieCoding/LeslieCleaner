using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Explicit trust, startup verification, trusted repair, and health scoring contract.</summary>
public interface IIntegrityRepairService
{
    /// <summary>Creates or explicitly replaces a trusted, sealed manifest and trusted backup.</summary>
    Task<TrustedBaselineResult> EstablishTrustedBaselineAsync(TrustedBaselineRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies all startup targets without ever creating an implicit baseline.</summary>
    Task<StartupIntegrityReport> VerifyStartupAsync(CancellationToken cancellationToken = default);

    /// <summary>Repairs failed files from the trusted backup, re-verifies them, and conditionally recovers the gate.</summary>
    Task<IntegrityRepairResult> RepairAsync(IntegrityRecoveryPrerequisites prerequisites,
        bool includeLowRiskConfiguration = false, CancellationToken cancellationToken = default);

    /// <summary>Reads the most recently persisted FileIntegrityRecords snapshot.</summary>
    Task<IReadOnlyList<FileIntegrityRecord>> GetRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Calculates the strict architecture-defined 30/20/20/15/15 health score.</summary>
    SecurityHealthScore CalculateHealthScore(SecurityHealthFactors factors);
}

/// <summary>Safely rotates encryption keys for an explicit set of protected local artifacts.</summary>
public interface IProtectedArtifactKeyRotationService
{
    /// <summary>Decrypts every artifact first, rotates, atomically re-encrypts, then retires the old key.</summary>
    Task<ProtectedArtifactRotationResult> RotateAsync(IReadOnlyList<string> encryptedArtifactPaths,
        CancellationToken cancellationToken = default);
}
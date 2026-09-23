namespace LeslieCleaner.Core.Abstractions;

/// <summary>Creates, indexes, verifies, restores, and retains reversible backups.</summary>
public interface IBackupService
{
    /// <summary>Exports a registry key and all descendants.</summary>
    Task<Models.BackupRecord> BackupRegistryKeyAsync(string registryKeyPath, CancellationToken cancellationToken = default);

    /// <summary>Captures one typed registry value without including sibling values.</summary>
    Task<Models.BackupRecord> BackupRegistryValueAsync(
        string registryKeyPath,
        string valueName,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a typed file, configuration, or database backup.</summary>
    Task<Models.BackupRecord> BackupFileAsync(
        string filePath,
        Models.BackupType type = Models.BackupType.File,
        string? description = null,
        Models.BackupStorageMode storageMode = Models.BackupStorageMode.FullCopy,
        CancellationToken cancellationToken = default);

    /// <summary>Restores a backup by its stable identifier, replacing an existing file target.</summary>
    Task RestoreAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>Restores a backup with an explicit file conflict policy.</summary>
    Task RestoreAsync(
        Guid backupId,
        Models.RestoreConflictPolicy conflictPolicy,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one backup by its stable identifier.</summary>
    Task<Models.BackupRecord?> GetBackupAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>Verifies payload existence, size, readability, and SHA-256 integrity.</summary>
    Task<Models.BackupVerificationResult> VerifyAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an unlocked backup and updates the unified index.</summary>
    Task DeleteAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>Protects or unprotects a backup from deletion.</summary>
    Task SetLockedAsync(Guid backupId, bool isLocked, CancellationToken cancellationToken = default);

    /// <summary>Applies age, count, and optional storage limits.</summary>
    Task<Models.BackupRetentionResult> ApplyRetentionAsync(
        Models.BackupRetentionPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every indexed backup in newest-first order.</summary>
    Task<IReadOnlyList<Models.BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default);
}

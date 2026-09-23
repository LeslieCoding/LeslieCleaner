namespace LeslieCleaner.Core.Models;

/// <summary>Describes a durable backup and its restoration scope.</summary>
public sealed record BackupRecord(
    Guid Id,
    string RegistryKeyPath,
    string FilePath,
    string MetadataPath,
    DateTimeOffset CreatedUtc,
    long SizeBytes,
    RegistryBackupKind Kind = RegistryBackupKind.Key,
    string? ValueName = null,
    RegistryValueSnapshot? ValueSnapshot = null)
{
    /// <summary>Logical payload category. Legacy metadata defaults to registry.</summary>
    public BackupType Type { get; init; } = BackupType.Registry;

    /// <summary>Human-readable backup name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Reason or operation description.</summary>
    public string? Description { get; init; }

    /// <summary>Original path for file, configuration, and database backups.</summary>
    public string? OriginalPath { get; init; }

    /// <summary>SHA-256 of the payload, encoded as uppercase hexadecimal.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Durable lifecycle state.</summary>
    public BackupStatus Status { get; init; } = BackupStatus.Completed;

    /// <summary>Storage strategy used for file-like payloads.</summary>
    public BackupStorageMode StorageMode { get; init; } = BackupStorageMode.FullCopy;

    /// <summary>Prevents automatic and manual deletion while set.</summary>
    public bool IsLocked { get; init; }

    /// <summary>Metadata schema version.</summary>
    public int Version { get; init; } = 2;
}

/// <summary>Supported backup payload categories.</summary>
public enum BackupType
{
    /// <summary>Windows registry data.</summary>
    Registry,
    /// <summary>General file data.</summary>
    File,
    /// <summary>Application configuration.</summary>
    Configuration,
    /// <summary>Application database.</summary>
    Database,
}

/// <summary>Durable backup lifecycle state.</summary>
public enum BackupStatus
{
    /// <summary>Payload is being created.</summary>
    Creating,
    /// <summary>Payload and metadata were verified.</summary>
    Completed,
    /// <summary>Integrity verification failed.</summary>
    Corrupt,
    /// <summary>At least one restore completed.</summary>
    Restored,
    /// <summary>Creation or restore failed.</summary>
    Failed,
}

/// <summary>How a file-like payload is preserved.</summary>
public enum BackupStorageMode
{
    /// <summary>Only path, size, hash, and timestamps are stored; restoration is unavailable.</summary>
    MetadataOnly,
    /// <summary>The source is moved into backup storage.</summary>
    Move,
    /// <summary>The source is copied using streaming I/O.</summary>
    FullCopy,
}

/// <summary>Registry backup granularity.</summary>
public enum RegistryBackupKind
{
    /// <summary>The complete key and its descendants.</summary>
    Key,
    /// <summary>One value without affecting sibling values during restoration.</summary>
    Value,
}

/// <summary>Typed, lossless snapshot of one registry value.</summary>
public sealed record RegistryValueSnapshot(
    bool Exists,
    string? Kind,
    string? StringValue,
    IReadOnlyList<string>? StringValues,
    string? BinaryValueBase64,
    int? DwordValue,
    long? QwordValue)
{
    /// <summary>Value name as preserved by the registry, including its original casing.</summary>
    public string? OriginalValueName { get; init; }
}

/// <summary>Integrity verification result for one backup.</summary>
public sealed record BackupVerificationResult(
    Guid BackupId,
    bool IsValid,
    string? Error,
    long ActualSizeBytes,
    string? ActualSha256);

/// <summary>Automatic cleanup limits. Locked backups are always retained.</summary>
public sealed record BackupRetentionPolicy
{
    /// <summary>Maximum age in days, or null to disable age cleanup.</summary>
    public int? MaxAgeDays { get; init; } = 30;

    /// <summary>Maximum retained count, or null to disable count cleanup.</summary>
    public int? MaxCount { get; init; } = 10;

    /// <summary>Optional aggregate payload ceiling.</summary>
    public long? MaxStorageBytes { get; init; }
}

/// <summary>Summary returned after enforcing retention.</summary>
public sealed record BackupRetentionResult(int DeletedCount, long FreedBytes, IReadOnlyList<Guid> DeletedBackupIds);

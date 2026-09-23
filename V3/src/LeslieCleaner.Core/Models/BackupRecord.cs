namespace LeslieCleaner.Core.Models;

/// <summary>Describes a durable registry backup and its restoration scope.</summary>
/// <param name="Id">Unique backup identifier.</param>
/// <param name="RegistryKeyPath">Canonical key path, including an optional 32-bit-view suffix.</param>
/// <param name="FilePath">Path to the key export or the value snapshot file.</param>
/// <param name="MetadataPath">Path to the JSON metadata record.</param>
/// <param name="CreatedUtc">Backup creation time in UTC.</param>
/// <param name="SizeBytes">Payload size in bytes.</param>
/// <param name="Kind">Backup granularity.</param>
/// <param name="ValueName">Value name for a value backup.</param>
/// <param name="ValueSnapshot">Typed value snapshot for a value backup.</param>
public sealed record BackupRecord(
    Guid Id,
    string RegistryKeyPath,
    string FilePath,
    string MetadataPath,
    DateTimeOffset CreatedUtc,
    long SizeBytes,
    RegistryBackupKind Kind = RegistryBackupKind.Key,
    string? ValueName = null,
    RegistryValueSnapshot? ValueSnapshot = null);

/// <summary>Registry backup granularity.</summary>
public enum RegistryBackupKind
{
    /// <summary>The complete key and its descendants.</summary>
    Key,

    /// <summary>One value without affecting sibling values during restoration.</summary>
    Value,
}

/// <summary>Typed, lossless snapshot of one registry value.</summary>
/// <param name="Exists">Whether the value existed when the snapshot was taken.</param>
/// <param name="Kind">Windows registry value type name.</param>
/// <param name="StringValue">String or expandable-string payload.</param>
/// <param name="StringValues">Multi-string payload.</param>
/// <param name="BinaryValueBase64">Base64-encoded binary payload.</param>
/// <param name="DwordValue">DWORD payload.</param>
/// <param name="QwordValue">QWORD payload.</param>
public sealed record RegistryValueSnapshot(
    bool Exists,
    string? Kind,
    string? StringValue,
    IReadOnlyList<string>? StringValues,
    string? BinaryValueBase64,
    int? DwordValue,
    long? QwordValue);

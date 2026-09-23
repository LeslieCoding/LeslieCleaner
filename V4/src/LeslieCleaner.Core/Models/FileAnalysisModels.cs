namespace LeslieCleaner.Core.Models;

/// <summary>Whitelisted stable sort fields for indexed file search.</summary>
public enum FileIndexSortField
{
    /// <summary>Full canonical path.</summary>
    Path,
    /// <summary>File name.</summary>
    Name,
    /// <summary>Logical file length.</summary>
    Size,
    /// <summary>Last modification time.</summary>
    Modified,
    /// <summary>Publisher metadata.</summary>
    Publisher,
    /// <summary>Product metadata.</summary>
    ProductName,
}

/// <summary>Complete, parameterized search conditions over the lightweight file index.</summary>
public sealed record FileIndexSearchQuery
{
    /// <summary>Text matched against name, path, publisher, and product name.</summary>
    public string? Text { get; init; }
    /// <summary>File-name substring.</summary>
    public string? Name { get; init; }
    /// <summary>Extension filter, with or without a leading period.</summary>
    public string? Extension { get; init; }
    /// <summary>Inclusive minimum logical size.</summary>
    public long? MinimumSizeBytes { get; init; }
    /// <summary>Inclusive maximum logical size.</summary>
    public long? MaximumSizeBytes { get; init; }
    /// <summary>Inclusive earliest modification time.</summary>
    public DateTimeOffset? ModifiedFromUtc { get; init; }
    /// <summary>Inclusive latest modification time.</summary>
    public DateTimeOffset? ModifiedToUtc { get; init; }
    /// <summary>Canonical location prefix.</summary>
    public string? LocationRoot { get; init; }
    /// <summary>Publisher substring.</summary>
    public string? Publisher { get; init; }
    /// <summary>Product-name substring.</summary>
    public string? ProductName { get; init; }
    /// <summary>Stable primary sort field.</summary>
    public FileIndexSortField SortBy { get; init; } = FileIndexSortField.Path;
    /// <summary>Sort descending when true.</summary>
    public bool Descending { get; init; }
    /// <summary>One-based page number.</summary>
    public int PageNumber { get; init; } = 1;
    /// <summary>Rows per page, from 1 through 500.</summary>
    public int PageSize { get; init; } = 100;
}

/// <summary>A stable page of indexed files and the matching total.</summary>
public sealed record FileIndexSearchPage(
    IReadOnlyList<FileIndexRecord> Items,
    long TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>Whether another page exists.</summary>
    public bool HasNextPage => (long)PageNumber * PageSize < TotalCount;
}

/// <summary>High-level category inferred locally from path and extension.</summary>
public enum AnalyzedFileCategory
{
    /// <summary>Windows or operating-system content.</summary>
    System,
    /// <summary>Installed application or game content.</summary>
    Application,
    /// <summary>User documents, media, archives, or projects.</summary>
    UserData,
    /// <summary>Cache, log, dump, or temporary content.</summary>
    Temporary,
    /// <summary>No confident local classification.</summary>
    Unknown,
}

/// <summary>One file yielded by the low-memory streaming scanner.</summary>
public sealed record AnalyzedFileEntry(
    string Path,
    string Name,
    string Extension,
    long LogicalSizeBytes,
    long? AllocatedSizeBytes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc,
    DateTimeOffset AccessedUtc,
    AnalyzedFileCategory Category);

/// <summary>Options for the public streaming file scan.</summary>
public sealed record FileStreamScanOptions
{
    /// <summary>Minimum logical size to yield.</summary>
    public long MinimumSizeBytes { get; init; }
    /// <summary>Optional extension filter, with or without a leading period.</summary>
    public string? Extension { get; init; }
    /// <summary>Reparse traversal is default-denied and must remain false for protected scans.</summary>
    public bool FollowReparsePoints { get; init; }
}

/// <summary>One immediate child returned by lazy directory-tree expansion.</summary>
public sealed record LazyFileTreeEntry(
    string Path,
    string Name,
    bool IsDirectory,
    bool HasChildren,
    long LogicalSizeBytes,
    long? AllocatedSizeBytes,
    DateTimeOffset ModifiedUtc,
    bool IsAccessible);

/// <summary>Availability of a drive metric that the platform may not expose.</summary>
public enum DriveMetricAvailability
{
    /// <summary>The value was measured from the operating system.</summary>
    Available,
    /// <summary>The current account or device did not expose the value.</summary>
    Unavailable,
    /// <summary>The drive type or platform does not support the metric.</summary>
    NotSupported,
}

/// <summary>Honest capacity, filesystem, and health evidence for one drive.</summary>
public sealed record DriveStorageSnapshot(
    string Name,
    string? VolumeLabel,
    DriveType DriveType,
    bool IsReady,
    string? FileSystem,
    long? TotalBytes,
    long? UsedBytes,
    long? FreeBytes,
    DriveMetricAvailability CapacityAvailability,
    string? HealthStatus,
    DriveMetricAvailability HealthAvailability,
    string HealthEvidence,
    string? Error);

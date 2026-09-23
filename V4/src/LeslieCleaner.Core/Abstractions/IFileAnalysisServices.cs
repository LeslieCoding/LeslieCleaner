using System.Runtime.CompilerServices;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Public low-memory, cancellation-aware streaming file scanner.</summary>
public interface IStreamingFileScanner
{
    /// <summary>Yields files as they are discovered without materializing the full tree.</summary>
    IAsyncEnumerable<AnalyzedFileEntry> ScanAsync(
        string root,
        FileStreamScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Loads only the immediate children of an expanded directory.</summary>
public interface ILazyFileTreeProvider
{
    /// <summary>Returns one level of children; callers request deeper levels only when expanded.</summary>
    Task<IReadOnlyList<LazyFileTreeEntry>> LoadChildrenAsync(
        string directory,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides measured drive capacity and explicitly available/unavailable health evidence.</summary>
public interface IDriveStorageService
{
    /// <summary>Returns every platform drive without inventing unavailable SMART data.</summary>
    Task<IReadOnlyList<DriveStorageSnapshot>> GetDrivesAsync(CancellationToken cancellationToken = default);
}

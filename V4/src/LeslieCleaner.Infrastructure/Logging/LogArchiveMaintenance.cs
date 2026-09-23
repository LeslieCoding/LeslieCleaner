using System.IO.Compression;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>Atomically compresses completed log segments after a configured age.</summary>
public sealed class LogArchiveMaintenance
{
    /// <summary>Compresses eligible top-level log files and removes a source only after verification.</summary>
    public async Task<int> CompressAsync(string logDirectory, TimeSpan minimumAge, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        if (minimumAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumAge));
        if (!Directory.Exists(logDirectory)) return 0;

        string root = Path.GetFullPath(logDirectory);
        int compressed = 0;
        foreach (string candidate in Directory.EnumerateFiles(root, "*.log", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetDirectoryName(source), root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Log path escaped the configured directory.");
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point log files cannot be archived.");
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(source) < minimumAge) continue;

            string finalArchive = source + ".zip";
            if (File.Exists(finalArchive)) continue;
            string temporaryArchive = finalArchive + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (FileStream output = new(temporaryArchive, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                    ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(source), CompressionLevel.Optimal);
                    await using Stream destination = entry.Open();
                    await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await input.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                using (ZipArchive verification = ZipFile.OpenRead(temporaryArchive))
                {
                    ZipArchiveEntry? entry = verification.GetEntry(Path.GetFileName(source));
                    if (entry is null || entry.Length != new FileInfo(source).Length)
                        throw new InvalidDataException("Compressed log verification failed.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryArchive, finalArchive);
                File.Delete(source);
                compressed++;
            }
            catch
            {
                if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
                throw;
            }
        }

        return compressed;
    }
}

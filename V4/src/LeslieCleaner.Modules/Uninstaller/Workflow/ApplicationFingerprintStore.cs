using System.Text.Json;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Creates and atomically persists minimal software fingerprints without full-disk scanning.</summary>
public sealed class ApplicationFingerprintStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _storeDirectory;

    /// <summary>Creates a store rooted at an absolute, non-reparse directory.</summary>
    /// <param name="storeDirectory">Fingerprint directory.</param>
    public ApplicationFingerprintStore(string storeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeDirectory);
        if (!Path.IsPathFullyQualified(storeDirectory))
        {
            throw new ArgumentException("Fingerprint directory must be absolute.", nameof(storeDirectory));
        }

        _storeDirectory = Path.GetFullPath(storeDirectory);
    }

    /// <summary>Builds a fingerprint from trusted application metadata and optional platform evidence.</summary>
    public ApplicationFingerprint Create(
        InstalledApp application,
        IReadOnlyList<FingerprintEntry>? additionalEntries = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        var entries = new List<FingerprintEntry>();
        if (!string.IsNullOrWhiteSpace(application.InstallLocation) &&
            Path.IsPathFullyQualified(application.InstallLocation))
        {
            entries.Add(new FingerprintEntry(
                FingerprintResourceType.Folder,
                Path.GetFullPath(application.InstallLocation),
                95,
                "Installed application location"));
        }

        if (!string.IsNullOrWhiteSpace(application.ProductCode))
        {
            entries.Add(new FingerprintEntry(
                FingerprintResourceType.Registry,
                $"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{application.ProductCode}",
                95,
                "MSI product code"));
        }

        if (additionalEntries is not null)
        {
            foreach (FingerprintEntry entry in additionalEntries)
            {
                if (entry.Confidence is < 0 or > 100 || string.IsNullOrWhiteSpace(entry.Value))
                {
                    throw new ArgumentException("Fingerprint evidence is invalid.", nameof(additionalEntries));
                }

                if (entry.Type is FingerprintResourceType.File or FingerprintResourceType.Folder or FingerprintResourceType.Configuration &&
                    !Path.IsPathFullyQualified(entry.Value))
                {
                    throw new ArgumentException("File-system fingerprint evidence must be absolute.", nameof(additionalEntries));
                }

                entries.Add(Normalize(entry));
            }
        }

        FingerprintEntry[] distinct = [.. entries
            .DistinctBy(static item => $"{item.Type}:{item.Value}", StringComparer.OrdinalIgnoreCase)];
        return new ApplicationFingerprint(
            Guid.NewGuid(), application.Id, application.Name, distinct, DateTimeOffset.UtcNow);
    }

    /// <summary>Atomically saves a fingerprint as JSON.</summary>
    public async Task<string> SaveAsync(ApplicationFingerprint fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        EnsureSafeStore();
        Directory.CreateDirectory(_storeDirectory);
        string finalPath = Path.Combine(_storeDirectory, $"{fingerprint.Id:N}.json");
        string temporaryPath = finalPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, fingerprint, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, finalPath, overwrite: false);
            return finalPath;
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    /// <summary>Reads a fingerprint by identifier.</summary>
    public async Task<ApplicationFingerprint?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSafeStore();
        string path = Path.Combine(_storeDirectory, $"{id:N}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<ApplicationFingerprint>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static FingerprintEntry Normalize(FingerprintEntry entry) =>
        entry.Type is FingerprintResourceType.File or FingerprintResourceType.Folder or FingerprintResourceType.Configuration
            ? entry with { Value = Path.GetFullPath(entry.Value) }
            : entry with { Value = entry.Value.Trim() };

    private void EnsureSafeStore()
    {
        string? current = _storeDirectory;
        var chain = new Stack<string>();
        while (!string.IsNullOrWhiteSpace(current))
        {
            chain.Push(current);
            current = Path.GetDirectoryName(current);
        }

        foreach (string path in chain)
        {
            if (Directory.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Fingerprint store contains a reparse point: {path}");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}


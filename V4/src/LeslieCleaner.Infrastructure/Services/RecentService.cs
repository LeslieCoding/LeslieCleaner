using System.Text.Json;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// Thread-safe recent-activity storage backed by
/// <c>%LocalAppData%\LeslieCleaner\recently_used.json</c>.
/// </summary>
public sealed class RecentService : IRecentService
{
    private const int MaxEntries = 50;
    private const string StoreFileName = "recently_used.json";

    private readonly string _storeFile;
    private readonly object _gate = new();
    private readonly List<RecentItem> _recent = [];

    /// <summary>Creates the service with its per-user persistence file.</summary>
    public RecentService() : this(GetDefaultStoreFile())
    {
    }

    /// <summary>Creates the service with an explicit persistence file.</summary>
    /// <param name="storeFile">JSON store used for recent activity.</param>
    public RecentService(string storeFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeFile);
        _storeFile = Path.GetFullPath(storeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(_storeFile)!);
        Load();
    }

    /// <inheritdoc />
    public void Record(string title, string? icon)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        title = title.Trim();
        icon ??= string.Empty;
        lock (_gate)
        {
            _recent.RemoveAll(existing =>
                existing.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            _recent.Insert(0, new RecentItem(title, icon, DateTimeOffset.UtcNow));

            if (_recent.Count > MaxEntries)
            {
                // Retain the newest entries. The old ascending sort retained the oldest 50.
                _recent.RemoveRange(MaxEntries, _recent.Count - MaxEntries);
            }

            Persist();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RecentItem> GetRecent(int? limit = null)
    {
        lock (_gate)
        {
            int take = limit.HasValue && limit.Value > 0 ? limit.Value : int.MaxValue;
            return _recent.Take(take).ToArray();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _recent.Clear();
            Persist();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storeFile))
            {
                return;
            }

            List<RecentItem>? items = JsonSerializer.Deserialize<List<RecentItem>>(
                File.ReadAllText(_storeFile));
            if (items is null)
            {
                return;
            }

            List<RecentItem> normalized = items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Title))
                .GroupBy(static item => item.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.OrderByDescending(static item => item.LastUsedUtc).First())
                .OrderByDescending(static item => item.LastUsedUtc)
                .Take(MaxEntries)
                .Select(static item => item with { Title = item.Title.Trim() })
                .ToList();
            _recent.AddRange(normalized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged or temporarily inaccessible user store must not prevent application startup.
        }
    }

    private void Persist()
    {
        try
        {
            WriteAtomically(_storeFile, JsonSerializer.Serialize(_recent));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Recent activity remains usable in memory if persistence temporarily fails.
        }
    }

    private static string GetDefaultStoreFile()
    {
        string baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeslieCleaner");
        return Path.Combine(baseDirectory, StoreFileName);
    }

    private static void WriteAtomically(string destination, string content)
    {
        string temporary = destination + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, destination, overwrite: true);
    }
}

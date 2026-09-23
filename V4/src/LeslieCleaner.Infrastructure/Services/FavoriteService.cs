using System.Text.Json;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// Thread-safe favorite storage backed by
/// <c>%LocalAppData%\LeslieCleaner\favorites.json</c>.
/// </summary>
public sealed class FavoriteService : IFavoriteService
{
    private const string StoreFileName = "favorites.json";

    private readonly string _storeFile;
    private readonly object _gate = new();
    private readonly List<FavoriteItem> _favorites = [];

    /// <summary>Creates the service with its per-user persistence file.</summary>
    public FavoriteService() : this(GetDefaultStoreFile())
    {
    }

    /// <summary>Creates the service with an explicit persistence file.</summary>
    /// <param name="storeFile">JSON store used for favorites.</param>
    public FavoriteService(string storeFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeFile);
        _storeFile = Path.GetFullPath(storeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(_storeFile)!);
        Load();
    }

    /// <inheritdoc />
    public void ToggleFavorite(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        title = title.Trim();
        lock (_gate)
        {
            int existingIndex = _favorites.FindIndex(item =>
                item.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                // ConcurrentBag.TryTake removed an arbitrary item. A locked list removes
                // the exact title that the user toggled.
                _favorites.RemoveAt(existingIndex);
            }
            else
            {
                int order = _favorites.Count == 0 ? 0 : _favorites.Max(static item => item.Order) + 1;
                _favorites.Add(new FavoriteItem(title, string.Empty, order));
            }

            Persist();
        }
    }

    /// <inheritdoc />
    public bool IsFavorite(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        title = title.Trim();
        lock (_gate)
        {
            return _favorites.Exists(item =>
                item.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FavoriteItem> GetFavorites()
    {
        lock (_gate)
        {
            return _favorites.OrderBy(static item => item.Order).ToArray();
        }
    }

    /// <inheritdoc />
    public void RemoveAll()
    {
        lock (_gate)
        {
            _favorites.Clear();
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

            List<FavoriteItem>? items = JsonSerializer.Deserialize<List<FavoriteItem>>(
                File.ReadAllText(_storeFile));
            if (items is null)
            {
                return;
            }

            List<FavoriteItem> normalized = items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Title))
                .GroupBy(static item => item.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.OrderBy(static item => item.Order).First())
                .OrderBy(static item => item.Order)
                .Select(static (item, index) => item with { Title = item.Title.Trim(), Order = index })
                .ToList();
            _favorites.AddRange(normalized);
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
            WriteAtomically(_storeFile, JsonSerializer.Serialize(_favorites));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Favorites are a convenience feature; preserve the in-memory state if persistence fails.
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

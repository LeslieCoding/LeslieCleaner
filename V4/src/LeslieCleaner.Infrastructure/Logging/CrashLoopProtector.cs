using System.Text.Json;
using System.Text.RegularExpressions;
using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>Persists privacy-safe crash reports and blocks rapid restart loops.</summary>
public sealed class CrashLoopProtector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _crashDirectory;
    private readonly string _stateFile;
    private readonly int _maximumCrashes;
    private readonly TimeSpan _window;

    /// <summary>Creates crash-loop state storage below the configured log directory.</summary>
    public CrashLoopProtector(IPathService pathService, int maximumCrashes = 3, TimeSpan? window = null)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCrashes);
        _window = window ?? TimeSpan.FromMinutes(5);
        if (_window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _maximumCrashes = maximumCrashes;
        _crashDirectory = Path.Combine(pathService.LogDirectory, "Crash");
        _stateFile = Path.Combine(_crashDirectory, "crash-loop-state.json");
    }

    /// <summary>True when another immediate restart is permitted.</summary>
    public bool IsRestartAllowed()
    {
        lock (_sync)
        {
            CrashState state = ReadState();
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - _window;
            return state.CrashesUtc.Count(x => x >= cutoff) < _maximumCrashes;
        }
    }

    /// <summary>Records a crash using type and sanitized technical details only.</summary>
    public string RecordCrash(Exception exception, string module, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        lock (_sync)
        {
            Directory.CreateDirectory(_crashDirectory);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            CrashState state = ReadState();
            DateTimeOffset cutoff = now - _window;
            DateTimeOffset[] crashes = state.CrashesUtc.Where(x => x >= cutoff).Append(now).ToArray();
            WriteAtomically(_stateFile, JsonSerializer.Serialize(new CrashState(crashes), JsonOptions));

            string path = Path.Combine(_crashDirectory, $"crash-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            string report = JsonSerializer.Serialize(new
            {
                TimestampUtc = now,
                Application = "LeslieCleaner",
                Version = appVersion,
                Module = Sanitize(module),
                ExceptionType = exception.GetType().FullName,
                Message = Sanitize(exception.Message),
                StackTrace = Sanitize(exception.StackTrace ?? string.Empty),
                RestartAllowed = crashes.Length < _maximumCrashes,
            }, JsonOptions);
            WriteAtomically(path, report);
            return path;
        }
    }

    /// <summary>Clears restart-loop counters after a stable session.</summary>
    public void MarkStable()
    {
        lock (_sync)
        {
            if (File.Exists(_stateFile)) File.Delete(_stateFile);
        }
    }

    /// <summary>Returns recent crash report paths without following subdirectories.</summary>
    public IReadOnlyList<string> GetCrashReports(int maximum = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        if (!Directory.Exists(_crashDirectory)) return [];
        return Directory.EnumerateFiles(_crashDirectory, "crash-*.json", SearchOption.TopDirectoryOnly)
            .Where(static path => !string.Equals(Path.GetFileName(path), "crash-loop-state.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(maximum)
            .ToArray();
    }

    private CrashState ReadState()
    {
        if (!File.Exists(_stateFile)) return new CrashState([]);
        try
        {
            return JsonSerializer.Deserialize<CrashState>(File.ReadAllText(_stateFile)) ?? new CrashState([]);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CrashState([]);
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }

    private static string Sanitize(string value)
    {
        string result = Regex.Replace(value, @"(?i)(\bBearer\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"(?i)(\b(?:password|passwd|pwd|secret|token|api[_-]?key|credential)\s*[:=]\s*)[^\s,;\}\]]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[REDACTED_EMAIL]");
        result = Regex.Replace(result, @"(?i)(?<![A-Za-z0-9_])[A-Z]:\\[^\r\n\t\""<>|]*", "[REDACTED_PATH]");
        return result;
    }

    private sealed record CrashState(IReadOnlyList<DateTimeOffset> CrashesUtc);
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Logging;

namespace LeslieCleaner.Infrastructure.Diagnostics;

/// <summary>Creates privacy-safe, integrity-verifiable diagnostic archives.</summary>
public sealed class DiagnosticsService
{
    /// <summary>Performs the documented operation.</summary>
    public const int LogDaysIncluded = 7;
    /// <summary>Performs the documented operation.</summary>
    public const int SummaryDays = 30;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SensitiveKeys = ["secret", "password", "token", "credential", "apikey", "api_key", "path", "url", "user"];
    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly IPerformanceMonitor? _performanceMonitor;
    private readonly CrashLoopProtector? _crashLoopProtector;

    /// <summary>Creates the diagnostics report service.</summary>
    public DiagnosticsService(
        IPathService pathService,
        IConfigService configService,
        ILogService logService,
        IPerformanceMonitor? performanceMonitor = null,
        CrashLoopProtector? crashLoopProtector = null)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _ = configService ?? throw new ArgumentNullException(nameof(configService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _performanceMonitor = performanceMonitor;
        _crashLoopProtector = crashLoopProtector;
    }

    /// <summary>Performs the documented operation.</summary>
    public Task<ErrorSummary> BuildErrorSummaryAsync(CancellationToken cancellationToken = default) =>
        LogSummaryBuilder.BuildAsync(_pathService.LogDirectory, SummaryDays, cancellationToken: cancellationToken);

    /// <summary>Performs the documented operation.</summary>
    public async Task<string> GenerateReportAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        string output = PrepareOutputDirectory(outputDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        string stem = $"LeslieCleaner-Diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        string finalPath = Path.Combine(output, stem + ".zip");
        string temporaryPath = Path.Combine(output, "." + stem + ".tmp");
        _logService.LogEvent(LogEventKind.UserAction, "Diagnostic report generation started");

        try
        {
            ErrorSummary summary = await BuildErrorSummaryAsync(cancellationToken).ConfigureAwait(false);
            string performanceJson = JsonSerializer.Serialize(_performanceMonitor?.GetHistory() ?? [], JsonOptions);
            string crashJson = await BuildCrashRecordsJsonAsync(cancellationToken).ConfigureAwait(false);
            string textSummary = BuildTextSummary(summary, performanceJson, crashJson);
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    await AddLogsAsync(archive, hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "error-summary.json", LogSummaryBuilder.ToJson(summary), hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "system-info.json", BuildSystemInfoJson(), hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "config-snapshot.json", BuildSanitizedConfigJson(), hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "performance-history.json", performanceJson, hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "crash-records.json", crashJson, hashes, cancellationToken).ConfigureAwait(false);
                    await AddTextAsync(archive, "diagnostics-summary.txt", textSummary, hashes, cancellationToken).ConfigureAwait(false);
                    string manifest = JsonSerializer.Serialize(new { Algorithm = "SHA-256", Entries = hashes.Select(x => new { Name = x.Key, Sha256 = x.Value }) }, JsonOptions);
                    await AddRawAsync(archive, "integrity-manifest.json", manifest, cancellationToken).ConfigureAwait(false);
                }
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateArchive(temporaryPath, hashes);
            RejectReparsePoint(output);
            File.Move(temporaryPath, finalPath);
            _logService.LogEvent(LogEventKind.UserAction, "Diagnostic report generation completed", new Dictionary<string, object?>
            {
                ["Warnings"] = summary.TotalWarnings,
                ["Errors"] = summary.TotalErrors,
            });
            return finalPath;
        }
        catch
        {
            TryDelete(temporaryPath);
            TryDelete(finalPath);
            throw;
        }
    }

    /// <summary>Performs the documented operation.</summary>
    public string BuildSanitizedConfigJson()
    {
        string file = Path.Combine(_pathService.ConfigDirectory, "settings.json");
        if (!File.Exists(file)) return "{}";
        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(file));
            if (root is null) return "{}";
            SanitizeNode(root, string.Empty);
            return root.ToJsonString(JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logService.LogException(ex, "DiagnosticsService.BuildSanitizedConfigJson");
            return "{}";
        }
    }

    private async Task AddLogsAsync(ZipArchive archive, IDictionary<string, string> hashes, CancellationToken token)
    {
        string root = Path.GetFullPath(_pathService.LogDirectory);
        if (!Directory.Exists(root)) return;
        RejectReparsePoint(root);
        DateTime cutoff = DateTime.UtcNow.AddDays(-LogDaysIncluded);
        int index = 0;
        IEnumerable<string> candidates = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(static path => Path.GetExtension(path).Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetExtension(path).Equals(".jsonl", StringComparison.OrdinalIgnoreCase));
        foreach (string candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            string file = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetDirectoryName(file)?.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Log path escaped the configured log directory.");
            RejectReparsePoint(file);
            if (File.GetLastWriteTimeUtc(file) < cutoff) continue;
            string content = await ReadAllTextSharedAsync(file, token).ConfigureAwait(false);
            string extension = Path.GetExtension(file).Equals(".jsonl", StringComparison.OrdinalIgnoreCase)
                ? ".jsonl"
                : ".log";
            await AddTextAsync(archive, $"logs/log-{++index:D3}{extension}", Redact(content), hashes, token).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadAllTextSharedAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> BuildCrashRecordsJsonAsync(CancellationToken token)
    {
        IReadOnlyList<string> reports = _crashLoopProtector?.GetCrashReports() ?? [];
        var records = new List<JsonNode>();
        foreach (string report in reports)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                string content = await File.ReadAllTextAsync(report, token).ConfigureAwait(false);
                JsonNode? record = JsonNode.Parse(Redact(content));
                if (record is not null) records.Add(record);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logService.LogException(ex, "DiagnosticsService.BuildCrashRecordsJsonAsync");
            }
        }
        return JsonSerializer.Serialize(records, JsonOptions);
    }

    private static string BuildTextSummary(ErrorSummary summary, string performanceJson, string crashJson)
    {
        using JsonDocument performance = JsonDocument.Parse(performanceJson);
        using JsonDocument crashes = JsonDocument.Parse(crashJson);
        return $"LeslieCleaner diagnostics{Environment.NewLine}" +
               $"Generated (UTC): {summary.GeneratedUtc:O}{Environment.NewLine}" +
               $"Warnings: {summary.TotalWarnings}{Environment.NewLine}" +
               $"Errors: {summary.TotalErrors}{Environment.NewLine}" +
               $"Performance samples: {performance.RootElement.GetArrayLength()}{Environment.NewLine}" +
               $"Crash records: {crashes.RootElement.GetArrayLength()}{Environment.NewLine}";
    }
    private static async Task AddTextAsync(ZipArchive archive, string name, string content, IDictionary<string, string> hashes, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Redact(content));
        hashes[name] = Convert.ToHexString(SHA256.HashData(bytes));
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private static async Task AddRawAsync(ZipArchive archive, string name, string content, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private static string PrepareOutputDirectory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Path.IsPathFullyQualified(value)) throw new ArgumentException("Diagnostic output directory must be absolute.", nameof(value));
        string path = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        RejectExistingReparsePoints(path);
        Directory.CreateDirectory(path);
        RejectExistingReparsePoints(path);
        return path;
    }

    private static void RejectExistingReparsePoints(string path)
    {
        string root = Path.GetPathRoot(path) ?? throw new ArgumentException("Invalid path.", nameof(path));
        string current = root;
        foreach (string segment in path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current)) RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reparse points are not allowed for diagnostic export.");
    }

    private static void ValidateArchive(string path, IReadOnlyDictionary<string, string> hashes)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        foreach (KeyValuePair<string, string> expected in hashes)
        {
            ZipArchiveEntry entry = archive.GetEntry(expected.Key) ?? throw new InvalidDataException("Diagnostic entry is missing.");
            using Stream stream = entry.Open();
            byte[] actual = SHA256.HashData(stream);
            if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected.Value)))
                throw new InvalidDataException("Diagnostic archive integrity check failed.");
        }
        if (archive.GetEntry("integrity-manifest.json") is null) throw new InvalidDataException("Integrity manifest is missing.");
    }

    private static string Redact(string value)
    {
        string result = Regex.Replace(value, @"(?i)(\bBearer\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"(?i)(\b(?:password|passwd|pwd|secret|token|api[_-]?key|credential)\s*[:=]\s*)[^\s,;\}\]]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[REDACTED_EMAIL]");
        result = Regex.Replace(result, @"(?<![A-Za-z0-9_])\\\\[^\s\\/:*?""<>|]+\\[^\r\n\t""<>|]*", "[REDACTED_PATH]");
        result = Regex.Replace(result, @"(?i)(?<![A-Za-z0-9_])[A-Z]:\\[^\r\n\t""<>|]*", "[REDACTED_PATH]");
        return result;
    }

    private static void SanitizeNode(JsonNode node, string parent)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> pair in obj.ToArray())
            {
                string key = string.IsNullOrEmpty(parent) ? pair.Key : $"{parent}:{pair.Key}";
                if (SensitiveKeys.Any(x => key.Contains(x, StringComparison.OrdinalIgnoreCase)) && pair.Value is JsonValue) obj[pair.Key] = "***";
                else if (pair.Value is not null) SanitizeNode(pair.Value, key);
            }
        }
        else if (node is JsonArray array)
            foreach (JsonNode? item in array) if (item is not null) SanitizeNode(item, parent);
    }

    private static string BuildSystemInfoJson() => JsonSerializer.Serialize(new
    {
        OsVersion = Environment.OSVersion.ToString(),
        Os64Bit = Environment.Is64BitOperatingSystem,
        DotNetVersion = Environment.Version.ToString(),
        ProcessorCount = Environment.ProcessorCount,
        WorkingSetMB = Math.Round(Environment.WorkingSet / 1048576.0, 1),
        AppVersion = typeof(DiagnosticsService).Assembly.GetName().Version?.ToString() ?? "4.0.0",
        GeneratedUtc = DateTimeOffset.UtcNow,
    }, JsonOptions);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

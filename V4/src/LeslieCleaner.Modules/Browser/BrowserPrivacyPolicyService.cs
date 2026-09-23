using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Modules.Browser;

/// <summary>
/// Applies explicit cookie and history policies after creating verified database backups.
/// Password databases and session files are never opened by this service.
/// </summary>
public sealed class BrowserPrivacyPolicyService
{
    private static readonly string[] TrackerDomains =
    [
        "doubleclick.net", "google-analytics.com", "googletagmanager.com", "facebook.net",
        "scorecardresearch.com", "adnxs.com", "criteo.com", "hotjar.com",
    ];

    private static readonly string[] TrackerCookieNames =
        ["_ga", "_gid", "_gat", "_fbp", "_gcl_au", "IDE", "NID"];

    private readonly IBackupService _backupService;
    private readonly ILogService _logService;
    private readonly ISecurityOperationGate? _securityOperationGate;

    /// <summary>Creates a fine-grained privacy service.</summary>
    public BrowserPrivacyPolicyService(
        IBackupService backupService,
        ILogService logService,
        ISecurityOperationGate? securityOperationGate = null)
    {
        _backupService = backupService;
        _logService = logService;
        _securityOperationGate = securityOperationGate;
    }

    /// <summary>Analyzes cookie and history counts without modifying the profile.</summary>
    public async Task<BrowserPrivacyAssessment> AnalyzeAsync(
        BrowserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string cookiePath = GetDatabasePath(profile, BrowserDataType.Cookies);
        string historyPath = GetDatabasePath(profile, BrowserDataType.History);
        long cookies = File.Exists(cookiePath)
            ? await CountAsync(cookiePath, profile.Engine == BrowserEngineKind.Chromium ? "cookies" : "moz_cookies", cancellationToken)
                .ConfigureAwait(false)
            : 0;
        long trackers = File.Exists(cookiePath)
            ? await CountTrackingCookiesAsync(cookiePath, profile.Engine, cancellationToken).ConfigureAwait(false)
            : 0;
        long history = File.Exists(historyPath)
            ? await CountAsync(historyPath, profile.Engine == BrowserEngineKind.Chromium ? "visits" : "moz_historyvisits", cancellationToken)
                .ConfigureAwait(false)
            : 0;

        int penalty = (int)Math.Min(60, trackers * 3) + (int)Math.Min(20, cookies / 250) + (history > 10_000 ? 10 : 0);
        var recommendations = new List<string>();
        if (trackers > 0)
        {
            recommendations.Add($"发现 {trackers} 个本地规则命中的跟踪 Cookie，可选择仅清理跟踪项。");
        }
        if (history > 10_000)
        {
            recommendations.Add($"浏览历史共 {history} 条，可按最近一小时、一天、一周或自定义时间段清理。");
        }
        if (recommendations.Count == 0)
        {
            recommendations.Add("未发现需要立即处理的跟踪 Cookie 或过大历史记录。");
        }

        return new BrowserPrivacyAssessment(cookies, trackers, history, Math.Clamp(100 - penalty, 0, 100), recommendations);
    }

    /// <summary>Applies an explicit policy after confirmation, backup, and verification.</summary>
    public async Task<BrowserPrivacyCleanResult> CleanAsync(
        BrowserProfile profile,
        BrowserPrivacyPolicy policy,
        bool userConfirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(policy);
        if (!userConfirmed)
        {
            throw new InvalidOperationException("Browser privacy cleanup requires explicit user confirmation.");
        }
        if (policy.IncludeSessionData)
        {
            throw new NotSupportedException("Session data requires the separate manual session-cleanup flow.");
        }
        ValidateRange(policy.HistoryRange);
        _securityOperationGate?.EnsureSensitiveOperationAllowed("Fine-grained browser privacy cleanup");

        var backups = new List<Guid>();
        long deletedCookies = 0;
        long deletedVisits = 0;
        try
        {
            if (policy.CookieMode != CookieCleanupMode.KeepAll)
            {
                string cookiePath = GetDatabasePath(profile, BrowserDataType.Cookies);
                if (File.Exists(cookiePath))
                {
                    backups.Add(await BackupAndVerifyAsync(cookiePath, cancellationToken).ConfigureAwait(false));
                    deletedCookies = await DeleteCookiesAsync(cookiePath, profile.Engine, policy, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (policy.HistoryRange is not null)
            {
                string historyPath = GetDatabasePath(profile, BrowserDataType.History);
                if (File.Exists(historyPath))
                {
                    backups.Add(await BackupAndVerifyAsync(historyPath, cancellationToken).ConfigureAwait(false));
                    deletedVisits = await DeleteHistoryAsync(historyPath, profile.Engine, policy.HistoryRange, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch
        {
            foreach (Guid backupId in backups.AsEnumerable().Reverse())
            {
                await _backupService.RestoreAsync(backupId, RestoreConflictPolicy.Overwrite, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            throw;
        }

        _logService.LogEvent(LogEventKind.DeleteFinish, "Fine-grained browser privacy cleanup completed",
            new Dictionary<string, object?>
            {
                ["Browser"] = profile.BrowserId,
                ["CookieMode"] = policy.CookieMode.ToString(),
                ["DeletedCookies"] = deletedCookies,
                ["DeletedHistoryVisits"] = deletedVisits,
                ["BackupCount"] = backups.Count,
            });
        return new BrowserPrivacyCleanResult(deletedCookies, deletedVisits, backups);
    }

    private async Task<Guid> BackupAndVerifyAsync(string path, CancellationToken cancellationToken)
    {
        BackupRecord record = await _backupService.BackupFileAsync(
            path, BackupType.Database, "Browser privacy policy backup",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        BackupVerificationResult result = await _backupService.VerifyAsync(record.Id, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new IOException($"Browser database backup verification failed: {path}");
        }
        return record.Id;
    }

    private static async Task<long> DeleteCookiesAsync(
        string path,
        BrowserEngineKind engine,
        BrowserPrivacyPolicy policy,
        CancellationToken cancellationToken)
    {
        string table = engine == BrowserEngineKind.Chromium ? "cookies" : "moz_cookies";
        string host = engine == BrowserEngineKind.Chromium ? "host_key" : "host";
        await using SqliteConnection connection = await OpenAsync(path, readOnly: false, cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        List<string> domains;
        switch (policy.CookieMode)
        {
            case CookieCleanupMode.TrackingOnly:
                domains = [.. TrackerDomains];
                command.CommandText = $"DELETE FROM {table} WHERE {BuildDomainPredicate(host, domains, command)} OR name IN ({BuildValues(TrackerCookieNames, command, "n")});";
                break;
            case CookieCleanupMode.SelectedDomains:
                domains = NormalizeDomains(policy.SelectedDomains, requireAny: true);
                command.CommandText = $"DELETE FROM {table} WHERE {BuildDomainPredicate(host, domains, command)};";
                break;
            case CookieCleanupMode.AllExceptWhitelist:
                domains = NormalizeDomains(policy.WhitelistDomains ?? BrowserCatalog.DefaultCookieWhitelist, requireAny: false);
                command.CommandText = domains.Count == 0
                    ? $"DELETE FROM {table};"
                    : $"DELETE FROM {table} WHERE NOT ({BuildDomainPredicate(host, domains, command)});";
                break;
            default:
                return 0;
        }

        long deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static async Task<long> DeleteHistoryAsync(
        string path,
        BrowserEngineKind engine,
        BrowserHistoryRange range,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(path, readOnly: false, cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        string table = engine == BrowserEngineKind.Chromium ? "visits" : "moz_historyvisits";
        string time = engine == BrowserEngineKind.Chromium ? "visit_time" : "visit_date";
        var predicates = new List<string>();
        if (range.FromUtc is { } from)
        {
            predicates.Add($"{time} >= $from");
            command.Parameters.AddWithValue("$from", ToBrowserTimestamp(from, engine));
        }
        if (range.ToUtc is { } to)
        {
            predicates.Add($"{time} < $to");
            command.Parameters.AddWithValue("$to", ToBrowserTimestamp(to, engine));
        }
        command.CommandText = $"DELETE FROM {table}" + (predicates.Count == 0 ? ";" : $" WHERE {string.Join(" AND ", predicates)};");
        long deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static async Task<long> CountTrackingCookiesAsync(
        string path,
        BrowserEngineKind engine,
        CancellationToken cancellationToken)
    {
        string table = engine == BrowserEngineKind.Chromium ? "cookies" : "moz_cookies";
        string host = engine == BrowserEngineKind.Chromium ? "host_key" : "host";
        await using SqliteConnection connection = await OpenAsync(path, readOnly: true, cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {BuildDomainPredicate(host, [.. TrackerDomains], command)} OR name IN ({BuildValues(TrackerCookieNames, command, "n")});";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountAsync(string path, string table, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(path, readOnly: true, cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<SqliteConnection> OpenAsync(string path, bool readOnly, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static string BuildDomainPredicate(
        string column,
        IReadOnlyList<string> domains,
        SqliteCommand command)
    {
        var predicates = new List<string>(domains.Count);
        for (int i = 0; i < domains.Count; i++)
        {
            string parameter = $"$d{i}";
            predicates.Add($"(ltrim(lower({column}), '.') = {parameter} OR ltrim(lower({column}), '.') LIKE '%.' || {parameter})");
            command.Parameters.AddWithValue(parameter, domains[i]);
        }
        return predicates.Count == 0 ? "0" : string.Join(" OR ", predicates);
    }

    private static string BuildValues(IReadOnlyList<string> values, SqliteCommand command, string prefix)
    {
        var names = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            string parameter = $"${prefix}{i}";
            names.Add(parameter);
            command.Parameters.AddWithValue(parameter, values[i]);
        }
        return string.Join(", ", names);
    }

    private static List<string> NormalizeDomains(IReadOnlyList<string>? domains, bool requireAny)
    {
        var normalized = domains?.Select(static domain => domain.Trim().TrimStart('.').ToLowerInvariant())
            .Where(static domain => domain.Length > 0 && Uri.CheckHostName(domain) == UriHostNameType.Dns)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (requireAny && normalized.Count == 0)
        {
            throw new ArgumentException("At least one valid domain is required.", nameof(domains));
        }
        return normalized;
    }

    private static long ToBrowserTimestamp(DateTimeOffset value, BrowserEngineKind engine)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        DateTimeOffset epoch = engine == BrowserEngineKind.Chromium
            ? new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.UnixEpoch;
        return checked((utc - epoch).Ticks / 10);
    }

    private static void ValidateRange(BrowserHistoryRange? range)
    {
        if (range?.FromUtc is { } from && range.ToUtc is { } to && from >= to)
        {
            throw new ArgumentException("History range start must be before end.", nameof(range));
        }
    }

    private static string GetDatabasePath(BrowserProfile profile, BrowserDataType type) =>
        profile.Engine == BrowserEngineKind.Chromium
            ? BrowserCatalog.GetChromiumDatabasePath(type, profile.ProfileDirectory)
            : BrowserCatalog.GetFirefoxDatabasePath(type, profile.ProfileDirectory);
}

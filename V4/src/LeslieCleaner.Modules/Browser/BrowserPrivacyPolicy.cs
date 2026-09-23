namespace LeslieCleaner.Modules.Browser;

/// <summary>Cookie cleanup scope selected by the user.</summary>
public enum CookieCleanupMode
{
    /// <summary>Do not change cookies.</summary>
    KeepAll,
    /// <summary>Remove only known tracking domains and tracking cookie names.</summary>
    TrackingOnly,
    /// <summary>Remove cookies for explicit domains.</summary>
    SelectedDomains,
    /// <summary>Remove every cookie except explicit login/allow-list domains.</summary>
    AllExceptWhitelist,
}

/// <summary>UTC history interval; null bounds mean unbounded.</summary>
/// <param name="FromUtc">Inclusive lower UTC bound.</param>
/// <param name="ToUtc">Exclusive upper UTC bound.</param>
public sealed record BrowserHistoryRange(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc)
{
    /// <summary>Creates a range covering all history.</summary>
    public static BrowserHistoryRange All { get; } = new(null, null);

    /// <summary>Creates a range ending now and starting at a standard recent interval.</summary>
    public static BrowserHistoryRange Recent(TimeSpan duration, DateTimeOffset nowUtc)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        return new BrowserHistoryRange(nowUtc.ToUniversalTime() - duration, nowUtc.ToUniversalTime());
    }
}

/// <summary>User-controlled browser privacy policy.</summary>
/// <param name="CookieMode">Cookie cleanup mode.</param>
/// <param name="SelectedDomains">Domains removed in SelectedDomains mode.</param>
/// <param name="WhitelistDomains">Domains retained in AllExceptWhitelist mode.</param>
/// <param name="HistoryRange">History range to delete; null keeps all history.</param>
/// <param name="IncludeSessionData">Session files require a separate manual flow and are not silently removed.</param>
public sealed record BrowserPrivacyPolicy(
    CookieCleanupMode CookieMode,
    IReadOnlyList<string>? SelectedDomains = null,
    IReadOnlyList<string>? WhitelistDomains = null,
    BrowserHistoryRange? HistoryRange = null,
    bool IncludeSessionData = false);

/// <summary>Read-only privacy analysis for one browser profile.</summary>
/// <param name="CookieCount">Total cookie rows.</param>
/// <param name="TrackingCookieCount">Rows matching local tracker rules.</param>
/// <param name="HistoryCount">History visit rows.</param>
/// <param name="PrivacyScore">0–100, higher is safer.</param>
/// <param name="Recommendations">Evidence-based recommendations.</param>
public sealed record BrowserPrivacyAssessment(
    long CookieCount,
    long TrackingCookieCount,
    long HistoryCount,
    int PrivacyScore,
    IReadOnlyList<string> Recommendations);

/// <summary>Result of a fine-grained privacy cleanup.</summary>
/// <param name="DeletedCookies">Deleted cookie rows.</param>
/// <param name="DeletedHistoryVisits">Deleted visit rows.</param>
/// <param name="BackupIds">Verified database backups available for restore.</param>
public sealed record BrowserPrivacyCleanResult(
    long DeletedCookies,
    long DeletedHistoryVisits,
    IReadOnlyList<Guid> BackupIds);


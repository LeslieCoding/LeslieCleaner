using System.Security.Cryptography;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Browser;
using LeslieCleaner.Tests.Support;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Tests;

public sealed class BrowserPrivacyPolicyTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task Analyze_ClassifiesKnownTrackerDomainAndCookieName()
    {
        BrowserProfile profile = CreateChromiumProfile();
        CreateCookies(profile, [
            (".doubleclick.net", "session"),
            ("analytics.example", "_ga"),
            ("login.example", "auth"),
        ]);
        CreateHistory(profile, []);
        var service = new BrowserPrivacyPolicyService(new CopyBackupService(), _workspace.Log);

        BrowserPrivacyAssessment result = await service.AnalyzeAsync(profile);

        Assert.Equal(3, result.CookieCount);
        Assert.Equal(2, result.TrackingCookieCount);
        Assert.InRange(result.PrivacyScore, 0, 99);
    }

    [Fact]
    public async Task Clean_TrackingOnly_BackupsThenKeepsLoginCookie()
    {
        BrowserProfile profile = CreateChromiumProfile();
        CreateCookies(profile, [
            ("ads.doubleclick.net", "id"),
            ("login.example", "auth"),
        ]);
        var backup = new CopyBackupService();
        var service = new BrowserPrivacyPolicyService(backup, _workspace.Log);

        BrowserPrivacyCleanResult result = await service.CleanAsync(
            profile, new BrowserPrivacyPolicy(CookieCleanupMode.TrackingOnly), userConfirmed: true);

        Assert.Equal(1, result.DeletedCookies);
        Assert.Single(result.BackupIds);
        Assert.Equal(1, backup.VerifyCalls);
        Assert.Equal(["login.example"], ReadHosts(profile));
    }

    [Fact]
    public async Task Clean_CustomHistoryRange_DeletesOnlyMatchingVisits()
    {
        BrowserProfile profile = CreateChromiumProfile();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CreateHistory(profile, [now.AddDays(-10), now.AddHours(-2), now.AddMinutes(-10)]);
        var service = new BrowserPrivacyPolicyService(new CopyBackupService(), _workspace.Log);
        var range = new BrowserHistoryRange(now.AddDays(-1), now);

        BrowserPrivacyCleanResult result = await service.CleanAsync(
            profile, new BrowserPrivacyPolicy(CookieCleanupMode.KeepAll, HistoryRange: range), true);

        Assert.Equal(2, result.DeletedHistoryVisits);
        Assert.Equal(1, CountRows(BrowserCatalog.GetChromiumDatabasePath(BrowserDataType.History, profile.ProfileDirectory), "visits"));
    }

    [Fact]
    public async Task Clean_RequiresConfirmationAndRejectsSilentSessionDeletion()
    {
        BrowserProfile profile = CreateChromiumProfile();
        var service = new BrowserPrivacyPolicyService(new CopyBackupService(), _workspace.Log);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CleanAsync(
            profile, new BrowserPrivacyPolicy(CookieCleanupMode.KeepAll), false));
        await Assert.ThrowsAsync<NotSupportedException>(() => service.CleanAsync(
            profile, new BrowserPrivacyPolicy(CookieCleanupMode.KeepAll, IncludeSessionData: true), true));
    }

    public void Dispose() => _workspace.Dispose();

    private BrowserProfile CreateChromiumProfile()
    {
        string profile = Path.Combine(_workspace.RootDirectory, "Default");
        Directory.CreateDirectory(Path.Combine(profile, "Network"));
        return new BrowserProfile("chrome", "Chrome (Default)", BrowserEngineKind.Chromium, profile, profile);
    }

    private static void CreateCookies(BrowserProfile profile, IReadOnlyList<(string Host, string Name)> rows)
    {
        string path = BrowserCatalog.GetChromiumDatabasePath(BrowserDataType.Cookies, profile.ProfileDirectory);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE cookies(host_key TEXT NOT NULL, name TEXT NOT NULL);";
        command.ExecuteNonQuery();
        foreach ((string host, string name) in rows)
        {
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO cookies(host_key,name) VALUES($host,$name);";
            command.Parameters.AddWithValue("$host", host);
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }
    }

    private static void CreateHistory(BrowserProfile profile, IReadOnlyList<DateTimeOffset> visits)
    {
        string path = BrowserCatalog.GetChromiumDatabasePath(BrowserDataType.History, profile.ProfileDirectory);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE visits(visit_time INTEGER NOT NULL);";
        command.ExecuteNonQuery();
        DateTimeOffset epoch = new(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);
        foreach (DateTimeOffset visit in visits)
        {
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO visits(visit_time) VALUES($time);";
            command.Parameters.AddWithValue("$time", (visit.ToUniversalTime() - epoch).Ticks / 10);
            command.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<string> ReadHosts(BrowserProfile profile)
    {
        string path = BrowserCatalog.GetChromiumDatabasePath(BrowserDataType.Cookies, profile.ProfileDirectory);
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT host_key FROM cookies ORDER BY host_key;";
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    private static long CountRows(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)command.ExecuteScalar()!;
    }

    private sealed class CopyBackupService : IBackupService
    {
        private readonly Dictionary<Guid, BackupRecord> _records = [];
        public int VerifyCalls { get; private set; }

        public Task<BackupRecord> BackupFileAsync(string filePath, BackupType type = BackupType.File,
            string? description = null, BackupStorageMode storageMode = BackupStorageMode.FullCopy,
            CancellationToken cancellationToken = default)
        {
            Guid id = Guid.NewGuid();
            string copy = filePath + ".backup." + id.ToString("N");
            File.Copy(filePath, copy);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy)));
            var record = new BackupRecord(id, string.Empty, copy, copy + ".json", DateTimeOffset.UtcNow,
                new FileInfo(copy).Length) { Type = type, OriginalPath = filePath, Sha256 = hash };
            _records.Add(id, record);
            return Task.FromResult(record);
        }

        public Task<BackupVerificationResult> VerifyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            VerifyCalls++;
            BackupRecord record = _records[id];
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(record.FilePath)));
            return Task.FromResult(new BackupVerificationResult(id, hash == record.Sha256, null,
                new FileInfo(record.FilePath).Length, hash));
        }

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
            RestoreAsync(id, RestoreConflictPolicy.Overwrite, cancellationToken);
        public Task RestoreAsync(Guid id, RestoreConflictPolicy policy, CancellationToken cancellationToken = default)
        { BackupRecord record = _records[id]; File.Copy(record.FilePath, record.OriginalPath!, true); return Task.CompletedTask; }
        public Task<BackupRecord?> GetBackupAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_records.GetValueOrDefault(id));
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { _records.Remove(id); return Task.CompletedTask; }
        public Task SetLockedAsync(Guid id, bool isLocked, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BackupRetentionResult> ApplyRetentionAsync(BackupRetentionPolicy policy, CancellationToken cancellationToken = default) => Task.FromResult(new BackupRetentionResult(0, 0, []));
        public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupRecord>>([.. _records.Values]);
        public Task<BackupRecord> BackupRegistryKeyAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupRecord> BackupRegistryValueAsync(string path, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

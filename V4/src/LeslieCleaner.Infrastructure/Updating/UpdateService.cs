using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Diagnostics;

namespace LeslieCleaner.Infrastructure.Updating;

/// <summary>Secure channel-aware, resumable and recoverable application update service.</summary>
public sealed class UpdateService
{
    /// <summary>Base update server URL configuration key.</summary>
    public const string ServerUrlConfigKey = "update:serverUrl";
    /// <summary>Current application version configuration key.</summary>
    public const string CurrentVersionConfigKey = "update:currentVersion";
    /// <summary>Stable/Beta/Development channel configuration key.</summary>
    public const string ChannelConfigKey = "update:channel";
    /// <summary>Signature enforcement configuration key.</summary>
    public const string RequireSignedManifestConfigKey = "update:requireSignedManifest";
    /// <summary>ECDSA public signing key configuration key.</summary>
    public const string SigningPublicKeyConfigKey = "update:signingPublicKey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private readonly IConfigService _configService;
    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly DiagnosticsService? _diagnosticsService;
    private readonly HttpClient _httpClient;
    private readonly bool _customTransport;
    private readonly SemaphoreSlim _historyLock = new(1, 1);

    /// <summary>Creates the update service.</summary>
    public UpdateService(
        IConfigService configService,
        IPathService pathService,
        ILogService logService,
        HttpClient? httpClient = null,
        DiagnosticsService? diagnosticsService = null)
    {
        _configService = configService;
        _pathService = pathService;
        _logService = logService;
        _customTransport = httpClient is not null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _diagnosticsService = diagnosticsService;
    }

    /// <summary>Compares numeric semantic versions and pads absent components with zero.</summary>
    public static int CompareVersions(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        int[] left = ParseParts(a);
        int[] right = ParseParts(b);
        for (int index = 0; index < 4; index++)
        {
            if (left[index] != right[index]) return left[index].CompareTo(right[index]);
        }

        return 0;

        static int[] ParseParts(string value)
        {
            string[] segments = value.Split('-', 2)[0].Split('.');
            var parts = new int[4];
            for (int index = 0; index < segments.Length && index < parts.Length; index++)
            {
                _ = int.TryParse(segments[index], out parts[index]);
            }

            return parts;
        }
    }

    /// <summary>Returns the configured channel, defaulting to Stable.</summary>
    public UpdateChannel GetConfiguredChannel()
    {
        string configured = _configService.GetValue(ChannelConfigKey, nameof(UpdateChannel.Stable));
        return Enum.TryParse(configured, true, out UpdateChannel channel)
            ? channel
            : UpdateChannel.Stable;
    }

    /// <summary>Fetches, authenticates and resolves an update for the current installation.</summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(
        string installDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        string currentVersion = _configService.GetValue(CurrentVersionConfigKey, "4.0.0");
        UpdateChannel channel = GetConfiguredChannel();
        string serverUrl = ResolveServerUrl(channel);
        if (string.IsNullOrWhiteSpace(serverUrl))
            return new UpdateCheckResult(false, currentVersion, null, []);

        if (!TryCreateTrustedSource(serverUrl, out Uri? source, out string reason))
            return new UpdateCheckResult(false, currentVersion, null, [], RejectionReason: reason);

        UpdateManifest? manifest = await FetchManifestAsync(source!, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            await EmitDiagnosticsAsync(currentVersion, null, serverUrl, cancellationToken).ConfigureAwait(false);
            return new UpdateCheckResult(false, currentVersion, null, []);
        }

        if (!TryValidateManifest(manifest, out reason) || !TryVerifyManifestSignature(manifest, out reason))
        {
            LogSecurityRejection(reason);
            return new UpdateCheckResult(false, currentVersion, null, [], RejectionReason: reason);
        }

        if (manifest.Channel != channel)
        {
            reason = $"Manifest channel {manifest.Channel} does not match selected channel {channel}.";
            return new UpdateCheckResult(false, currentVersion, null, [], RejectionReason: reason);
        }

        if (CompareVersions(manifest.Version, currentVersion) <= 0)
            return new UpdateCheckResult(false, currentVersion, null, []);

        UpdateCompatibilityResult compatibility = EvaluateCompatibility(manifest, currentVersion, installDirectory);
        if (!compatibility.IsCompatible)
            return new UpdateCheckResult(false, currentVersion, null, [], RejectionReason: compatibility.Reason);

        (UpdatePackageKind kind, IReadOnlyList<UpdateFileEntry> candidates) =
            ResolvePackage(manifest, currentVersion);
        var files = new List<UpdateFileEntry>();
        foreach (UpdateFileEntry entry in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveContainedPath(installDirectory, entry.Path, out string localPath) ||
                HasReparsePointInExistingPath(installDirectory, localPath))
                return new UpdateCheckResult(false, currentVersion, null, [],
                    RejectionReason: $"Unsafe install path: {entry.Path}");

            if (!File.Exists(localPath) ||
                !await VerifyHashAsync(localPath, entry.Sha256, cancellationToken).ConfigureAwait(false))
                files.Add(entry);
        }

        await EmitDiagnosticsAsync(currentVersion, manifest.Version, serverUrl, cancellationToken).ConfigureAwait(false);
        await RecordHistoryAsync(CreateHistory(currentVersion, manifest.Version, channel, kind,
            UpdateOutcome.Checked, null, files.Select(static file => file.Path).ToArray(),
            manifest.RestartRequired), cancellationToken).ConfigureAwait(false);
        return new UpdateCheckResult(true, currentVersion, manifest, files, kind);
    }

    /// <summary>Checks current version, operating system, architecture and file dependencies.</summary>
    public static UpdateCompatibilityResult EvaluateCompatibility(
        UpdateManifest manifest,
        string currentVersion,
        string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        UpdateCompatibility? rules = manifest.Compatibility;
        if (rules is null) return new UpdateCompatibilityResult(true, null);
        if (!string.IsNullOrWhiteSpace(rules.MinimumVersion) &&
            CompareVersions(currentVersion, rules.MinimumVersion) < 0)
            return new UpdateCompatibilityResult(false,
                $"Current version {currentVersion} is below required {rules.MinimumVersion}.");

        string os = OperatingSystem.IsWindows() ? "windows" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "macos" : "unknown";
        if (rules.OperatingSystems is { Count: > 0 } &&
            !rules.OperatingSystems.Contains(os, StringComparer.OrdinalIgnoreCase))
            return new UpdateCompatibilityResult(false, $"Operating system {os} is unsupported.");

        string architecture = RuntimeInformation.OSArchitecture.ToString();
        if (rules.Architectures is { Count: > 0 } &&
            !rules.Architectures.Contains(architecture, StringComparer.OrdinalIgnoreCase))
            return new UpdateCompatibilityResult(false, $"Architecture {architecture} is unsupported.");

        foreach (string required in rules.RequiredFiles ?? [])
        {
            if (!TryResolveContainedPath(installDirectory, required, out string dependency) ||
                HasReparsePointInExistingPath(installDirectory, dependency) ||
                !File.Exists(dependency))
                return new UpdateCompatibilityResult(false, $"Required dependency is missing: {required}");
        }

        return new UpdateCompatibilityResult(true, null);
    }
    /// <summary>Downloads using resume, pause, cancellation and verified progress.</summary>
    public Task<string> DownloadUpdateAsync(
        UpdateManifest manifest,
        IReadOnlyList<UpdateFileEntry> filesToDownload,
        CancellationToken cancellationToken = default) =>
        DownloadUpdateAsync(manifest, filesToDownload, UpdateDownloadOptions.Default, cancellationToken);

    /// <summary>Downloads a verified update with explicit transfer controls.</summary>
    public async Task<string> DownloadUpdateAsync(
        UpdateManifest manifest,
        IReadOnlyList<UpdateFileEntry> filesToDownload,
        UpdateDownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(filesToDownload);
        ArgumentNullException.ThrowIfNull(options);
        if (options.BufferSize < 4096) throw new ArgumentOutOfRangeException(nameof(options));
        if (!TryValidateManifest(manifest, out string reason) ||
            !TryVerifyManifestSignature(manifest, out reason))
            throw new LeslieCleanerException($"Unsafe update manifest: {reason}");

        if (!TryCreateTrustedSource(ResolveServerUrl(manifest.Channel), out Uri? source, out reason))
            throw new LeslieCleanerException(reason);

        string stagingRoot = _pathService.EnsureDirectory(
            Path.Combine(_pathService.EnsureDirectory(_pathService.DataDirectory), "UpdateStaging"));
        if (!TryResolveContainedPath(stagingRoot, manifest.Version, out string stagingDirectory))
            throw new LeslieCleanerException("Unsafe update version directory.");
        Directory.CreateDirectory(stagingDirectory);
        if (HasReparsePointInExistingPath(stagingRoot, stagingDirectory))
            throw new LeslieCleanerException("Update staging path contains a reparse point.");

        using var timeoutSource = options.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : new CancellationTokenSource();
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        CancellationToken token = linkedSource.Token;
        long totalBytes = filesToDownload.Sum(static file => file.Size);
        long completedBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            foreach (UpdateFileEntry entry in filesToDownload)
            {
                token.ThrowIfCancellationRequested();
                await options.PauseToken.WaitWhilePausedAsync(token).ConfigureAwait(false);
                string destination = ResolveSafeStagingPath(stagingDirectory, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                completedBytes += await DownloadFileResumableAsync(
                    source!, entry, destination, completedBytes, totalBytes, stopwatch, options, token)
                    .ConfigureAwait(false);
                if (!await VerifyHashAsync(destination, entry.Sha256, token).ConfigureAwait(false))
                {
                    DeleteDirectoryBestEffort(stagingDirectory);
                    throw new LeslieCleanerException($"更新文件校验失败（SHA256 不匹配）: {entry.Path}");
                }
            }

            await WriteAtomicTextAsync(Path.Combine(stagingDirectory, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions), token).ConfigureAwait(false);
            string oldVersion = _configService.GetValue(CurrentVersionConfigKey, "4.0.0");
            await RecordHistoryAsync(CreateHistory(oldVersion, manifest.Version, manifest.Channel,
                ResolvePackage(manifest, oldVersion).Kind, UpdateOutcome.Downloaded, null,
                filesToDownload.Select(static file => file.Path).ToArray(), manifest.RestartRequired),
                token).ConfigureAwait(false);
            return stagingDirectory;
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Update download exceeded its configured timeout.");
        }
        catch (LeslieCleanerException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, "UpdateService.Download");
            throw;
        }
    }

    /// <summary>
    /// Backs up data, performs application-owned migrations, verifies them, applies files,
    /// and restores the data backup after any migration, verification or apply failure.
    /// </summary>
    public async Task<UpdateTransactionResult> ExecuteInstallTransactionAsync(
        UpdateManifest manifest,
        string oldVersion,
        UpdatePackageKind packageKind,
        IUpdateDataMigration dataMigration,
        Func<CancellationToken, Task> applyVerifiedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(dataMigration);
        ArgumentNullException.ThrowIfNull(applyVerifiedFiles);
        string backupId = string.Empty;
        bool rollbackAttempted = false;
        bool rollbackSucceeded = false;
        bool migrationSucceeded = false;
        string? error = null;
        try
        {
            backupId = await dataMigration.BackupAsync(
                oldVersion, manifest.Version, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(backupId))
                throw new LeslieCleanerException("Update data backup did not return an identifier.");
            await dataMigration.MigrateAsync(manifest.Migrations ?? [], cancellationToken).ConfigureAwait(false);
            migrationSucceeded = await dataMigration.VerifyAsync(cancellationToken).ConfigureAwait(false);
            if (!migrationSucceeded) throw new LeslieCleanerException("Update data verification failed.");
            await applyVerifiedFiles(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = exception.Message;
            if (!string.IsNullOrWhiteSpace(backupId))
            {
                rollbackAttempted = true;
                try
                {
                    await dataMigration.RestoreAsync(backupId, CancellationToken.None).ConfigureAwait(false);
                    rollbackSucceeded = true;
                }
                catch (Exception restoreException)
                {
                    _logService.LogException(restoreException, "UpdateService.DataRollback");
                }
            }
        }

        bool success = error is null;
        UpdateOutcome outcome = success ? UpdateOutcome.Installed :
            rollbackSucceeded ? UpdateOutcome.RolledBack : UpdateOutcome.Failed;
        UpdateHistoryRecord record = CreateHistory(oldVersion, manifest.Version, manifest.Channel,
            packageKind, outcome, error, manifest.Files.Select(static file => file.Path).ToArray(),
            manifest.RestartRequired, rollbackAttempted, rollbackSucceeded, migrationSucceeded);
        await RecordHistoryAsync(record, CancellationToken.None).ConfigureAwait(false);
        string reportPath = await WriteReportAsync(record, CancellationToken.None).ConfigureAwait(false);
        return new UpdateTransactionResult(success, rollbackSucceeded, error, reportPath);
    }

    /// <summary>Returns a snapshot of persisted update history.</summary>
    public async Task<IReadOnlyList<UpdateHistoryRecord>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        string path = GetHistoryPath();
        await _historyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return [];
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<List<UpdateHistoryRecord>>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (JsonException exception)
        {
            _logService.LogException(exception, "UpdateService.ReadHistory");
            return [];
        }
        finally
        {
            _historyLock.Release();
        }
    }
    /// <summary>Writes privacy-minimized update diagnostics and optionally uploads it.</summary>
    public async Task<string> EmitDiagnosticsAsync(
        string currentVersion,
        string? latestVersion,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ErrorSummary summary = _diagnosticsService is not null
            ? await _diagnosticsService.BuildErrorSummaryAsync(cancellationToken).ConfigureAwait(false)
            : new ErrorSummary(DateTimeOffset.UtcNow, 30, 0, 0, [], []);
        string json = JsonSerializer.Serialize(new
        {
            currentVersion,
            latestVersion,
            generatedUtc = DateTimeOffset.UtcNow,
            warnings = summary.TotalWarnings,
            errors = summary.TotalErrors,
            byEventKind = summary.ByEventKind,
            topExceptions = summary.TopExceptions,
        }, JsonOptions);
        string path = Path.Combine(
            _pathService.EnsureDirectory(_pathService.DataDirectory), "update-diagnostics.json");
        await WriteAtomicTextAsync(path, json, cancellationToken).ConfigureAwait(false);

        if (TryCreateTrustedSource(serverUrl, out Uri? source, out _) && source is not null)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await _httpClient.PostAsync(
                    new Uri(source, "diagnostics"), content, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                _logService.LogEvent(LogEventKind.UserAction, "更新诊断已上送",
                    new Dictionary<string, object?> { ["StatusCode"] = (int)response.StatusCode });
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logService.LogException(exception, "UpdateService.DiagnosticsUpload");
            }
        }

        return json;
    }

    /// <summary>Starts the external transactional updater after validating all paths.</summary>
    public void ApplyUpdate(
        string stagingDirectory,
        string installDirectory,
        string mainExecutable,
        string updaterExecutable)
    {
        string staging = Path.GetFullPath(stagingDirectory);
        string install = Path.GetFullPath(installDirectory);
        string restart = Path.GetFullPath(mainExecutable);
        string updater = Path.GetFullPath(updaterExecutable);
        if (!Directory.Exists(staging) || !Directory.Exists(install) ||
            HasReparsePointInExistingPath(staging, staging) ||
            HasReparsePointInExistingPath(install, install) ||
            !TryEnsureFileWithinRoot(install, restart) ||
            !File.Exists(updater))
            throw new LeslieCleanerException("Updater paths failed safety validation.");

        Process.Start(new ProcessStartInfo
        {
            FileName = updater,
            ArgumentList =
            {
                "--pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--source", staging,
                "--target", install,
                "--restart", restart,
            },
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    /// <summary>Verifies a file with SHA-256 using fixed-time comparison.</summary>
    public static async Task<bool> VerifyHashAsync(
        string filePath,
        string expectedSha256Hex,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(expectedSha256Hex));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Builds the deterministic payload covered by a manifest signature.</summary>
    public static byte[] GetManifestSigningPayload(UpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var payload = new
        {
            version = manifest.Version,
            files = manifest.Files.Select(CanonicalFile),
            releaseNotes = manifest.ReleaseNotes,
            signingKeyId = manifest.SigningKeyId,
            channel = manifest.Channel,
            mandatory = manifest.Mandatory,
            compatibility = manifest.Compatibility,
            packages = manifest.Packages?.Select(package => new
            {
                kind = package.Kind,
                size = package.Size,
                fromVersion = package.FromVersion,
                files = package.Files?.Select(CanonicalFile),
            }),
            migrations = manifest.Migrations,
            restartRequired = manifest.RestartRequired,
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        static object CanonicalFile(UpdateFileEntry entry) => new
        {
            path = entry.Path,
            sha256 = entry.Sha256,
            size = entry.Size,
            url = entry.Url,
        };
    }

    private async Task<long> DownloadFileResumableAsync(
        Uri source,
        UpdateFileEntry entry,
        string destination,
        long completedBefore,
        long totalBytes,
        Stopwatch stopwatch,
        UpdateDownloadOptions options,
        CancellationToken cancellationToken)
    {
        string partial = destination + ".part";
        long existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (existing > entry.Size)
        {
            File.Delete(partial);
            existing = 0;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, ResolveFileUri(source, entry.Url));
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (existing > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            existing = 0;
            File.Delete(partial);
        }
        else if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new HttpRequestException(
                $"Update source rejected resume request: {(int)response.StatusCode}.");
        }

        response.EnsureSuccessStatusCode();
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream output = new(partial, existing > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] buffer = new byte[options.BufferSize];
        long fileBytes = existing;
        while (true)
        {
            await options.PauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            fileBytes += read;
            if (fileBytes > entry.Size)
                throw new LeslieCleanerException($"Downloaded file exceeds manifest size: {entry.Path}");
            long aggregate = completedBefore + fileBytes;
            double elapsed = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
            options.Progress?.Report(new UpdateDownloadProgress(
                entry.Path, aggregate, totalBytes, aggregate / elapsed,
                totalBytes == 0 ? 100 : aggregate * 100d / totalBytes));
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        await output.DisposeAsync().ConfigureAwait(false);
        await input.DisposeAsync().ConfigureAwait(false);
        if (fileBytes != entry.Size)
            throw new LeslieCleanerException($"Downloaded size mismatch for {entry.Path}: {fileBytes}/{entry.Size}");
        try
        {
            File.Move(partial, destination, true);
        }
        catch (IOException exception)
        {
            throw new LeslieCleanerException($"Cannot move downloaded file to destination: {exception.Message}", exception);
        }
        return fileBytes;
    }
    private string ResolveServerUrl(UpdateChannel channel)
    {
        string key = $"{ServerUrlConfigKey}:{channel.ToString().ToLowerInvariant()}";
        return _configService.GetValue(key, _configService.GetValue(ServerUrlConfigKey, string.Empty));
    }

    private bool TryCreateTrustedSource(string value, out Uri? source, out string error)
    {
        source = null;
        if (!Uri.TryCreate(value.EndsWith('/') ? value : value + "/", UriKind.Absolute, out Uri? candidate))
        {
            error = "Update source is not an absolute URI.";
            return false;
        }

        bool loopbackHttp = candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback;
        bool secure = candidate.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(candidate.UserInfo);
        if (!secure && !loopbackHttp && !_customTransport)
        {
            error = "Remote update sources must use trusted HTTPS.";
            return false;
        }

        if (candidate.Scheme != Uri.UriSchemeHttps && candidate.Scheme != Uri.UriSchemeHttp ||
            !string.IsNullOrEmpty(candidate.UserInfo))
        {
            error = "Update source scheme or credentials are unsafe.";
            return false;
        }

        source = candidate;
        error = string.Empty;
        return true;
    }

    private static Uri ResolveFileUri(Uri source, string value)
    {
        if (!Uri.TryCreate(source, value, out Uri? result) ||
            result.Scheme != source.Scheme ||
            !result.Host.Equals(source.Host, StringComparison.OrdinalIgnoreCase) ||
            result.Port != source.Port ||
            !string.IsNullOrEmpty(result.UserInfo))
            throw new LeslieCleanerException("Update file URL escapes the trusted source.");
        return result;
    }

    private async Task<UpdateManifest?> FetchManifestAsync(
        Uri source,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                new Uri(source, "manifest.json"), HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<UpdateManifest>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logService.LogException(exception, "UpdateService.FetchManifest");
            return null;
        }
    }

    private static (UpdatePackageKind Kind, IReadOnlyList<UpdateFileEntry> Files) ResolvePackage(
        UpdateManifest manifest,
        string currentVersion)
    {
        IReadOnlyList<UpdatePackageDescriptor> packages = manifest.Packages ?? [];
        UpdatePackageDescriptor? incremental = packages
            .Where(package => package.Kind == UpdatePackageKind.Incremental &&
                              package.FromVersion is not null &&
                              CompareVersions(package.FromVersion, currentVersion) == 0)
            .OrderBy(static package => package.Size)
            .FirstOrDefault();
        UpdatePackageDescriptor? full = packages
            .Where(static package => package.Kind == UpdatePackageKind.Full)
            .OrderBy(static package => package.Size)
            .FirstOrDefault();
        UpdatePackageDescriptor? selected = incremental is not null &&
            (full is null || incremental.Size <= full.Size)
            ? incremental
            : full;
        return selected is null
            ? (UpdatePackageKind.Incremental, manifest.Files)
            : (selected.Kind, selected.Files ?? manifest.Files);
    }

    private static bool TryValidateManifest(UpdateManifest manifest, out string error)
    {
        if (!Version.TryParse(manifest.Version.Split('-', 2)[0], out _))
        {
            error = "Version is not a valid semantic numeric version.";
            return false;
        }

        IEnumerable<UpdateFileEntry> packageFiles =
            manifest.Packages?.SelectMany(static package => package.Files ?? []) ?? [];
        foreach (UpdateFileEntry entry in manifest.Files.Concat(packageFiles))
        {
            if (!TryResolveContainedPath(Path.GetTempPath(), entry.Path, out _) ||
                entry.Size < 0 || entry.Sha256.Length != 64 ||
                !entry.Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(entry.Url))
            {
                error = $"File metadata is invalid or escapes its root: {entry.Path}";
                return false;
            }
        }

        foreach (UpdateMigrationDescriptor migration in manifest.Migrations ?? [])
        {
            if (string.IsNullOrWhiteSpace(migration.Id) ||
                !Version.TryParse(migration.FromVersion, out _) ||
                !Version.TryParse(migration.ToVersion, out _))
            {
                error = "Migration metadata is invalid.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryVerifyManifestSignature(UpdateManifest manifest, out string error)
    {
        bool required = _configService.GetValue(RequireSignedManifestConfigKey, true);
        if (!required && string.IsNullOrWhiteSpace(manifest.Signature))
        {
            error = string.Empty;
            return true;
        }

        string publicKey = _configService.GetValue(SigningPublicKeyConfigKey, string.Empty);
        if (string.IsNullOrWhiteSpace(manifest.Signature) || string.IsNullOrWhiteSpace(publicKey))
        {
            error = "A signed manifest and configured signing public key are required.";
            return false;
        }

        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            if (!verifier.VerifyData(GetManifestSigningPayload(manifest),
                Convert.FromBase64String(manifest.Signature), HashAlgorithmName.SHA256))
            {
                error = "Manifest signature verification failed.";
                return false;
            }
        }
        catch (Exception exception) when (
            exception is CryptographicException or FormatException)
        {
            error = "Manifest signing key or signature is malformed.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryResolveContainedPath(
        string rootDirectory,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
            return false;
        string root = Path.GetFullPath(rootDirectory).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        fullPath = candidate;
        return true;
    }

    private static string ResolveSafeStagingPath(string root, string relativePath)
    {
        if (!TryResolveContainedPath(root, relativePath, out string path) ||
            HasReparsePointInExistingPath(root, path))
            throw new LeslieCleanerException($"Unsafe update file path: {relativePath}");
        return path;
    }

    private static bool HasReparsePointInExistingPath(string root, string candidate)
    {
        string fullRoot = Path.GetFullPath(root);
        string current = fullRoot;
        if (Path.Exists(current) &&
            (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        string relative = Path.GetRelativePath(fullRoot, Path.GetFullPath(candidate));
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Path.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }

        return false;
    }

    private static bool TryEnsureFileWithinRoot(string root, string file)
    {
        string normalized = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(file).StartsWith(normalized, StringComparison.OrdinalIgnoreCase) &&
               !HasReparsePointInExistingPath(root, file);
    }
    private async Task RecordHistoryAsync(
        UpdateHistoryRecord record,
        CancellationToken cancellationToken)
    {
        string path = GetHistoryPath();
        await _historyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<UpdateHistoryRecord> records = [];
            if (File.Exists(path))
            {
                try
                {
                    await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                        81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    records = await JsonSerializer.DeserializeAsync<List<UpdateHistoryRecord>>(
                        input, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
                }
                catch (JsonException exception)
                {
                    _logService.LogException(exception, "UpdateService.HistoryRecovery");
                }
            }

            records.Add(record);
            if (records.Count > 500) records.RemoveRange(0, records.Count - 500);
            await WriteAtomicTextAsync(
                path, JsonSerializer.Serialize(records, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _historyLock.Release();
        }
    }

    private async Task<string> WriteReportAsync(
        UpdateHistoryRecord record,
        CancellationToken cancellationToken)
    {
        string reports = _pathService.EnsureDirectory(Path.Combine(
            _pathService.EnsureDirectory(_pathService.DataDirectory), "UpdateReports"));
        string path = Path.Combine(reports,
            $"{record.CreatedUtc:yyyyMMdd-HHmmss}-{record.Id:N}.json");
        await WriteAtomicTextAsync(
            path, JsonSerializer.Serialize(record, JsonOptions), cancellationToken).ConfigureAwait(false);
        return path;
    }

    private string GetHistoryPath() => Path.Combine(
        _pathService.EnsureDirectory(_pathService.DataDirectory), "update-history.json");

    private static UpdateHistoryRecord CreateHistory(
        string oldVersion,
        string newVersion,
        UpdateChannel channel,
        UpdatePackageKind packageKind,
        UpdateOutcome outcome,
        string? error,
        IReadOnlyList<string>? changedFiles = null,
        bool restartRequired = false,
        bool rollbackAttempted = false,
        bool rollbackSucceeded = false,
        bool migrationSucceeded = false) =>
        new(Guid.NewGuid(), oldVersion, newVersion, channel, packageKind, outcome,
            DateTimeOffset.UtcNow, error, rollbackAttempted, rollbackSucceeded,
            changedFiles ?? [], migrationSucceeded, restartRequired);

    private static async Task WriteAtomicTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(
                temporary, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void LogSecurityRejection(string reason) =>
        _logService.LogEvent(LogEventKind.Exception, "Rejected unsafe update manifest",
            new Dictionary<string, object?> { ["Reason"] = reason });

    private static void DeleteDirectoryBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
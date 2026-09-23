using System.Security.Cryptography;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Diagnostics;

namespace LeslieCleaner.Infrastructure.Updating;

/// <summary>
/// 自定义增量更新服务。流程：版本清单 JSON 拉取 → 语义化版本比较 →
/// 文件级增量（只下载本地缺失/SHA256 不同的文件）→ 逐文件校验 → 暂存目录 →
/// 交由 Updater 辅助进程（主程序退出后替换再重启）。
/// 任何校验失败清理暂存目录并抛错（回滚到未更新状态）。
/// </summary>
public sealed class UpdateService
{
    /// <summary>更新服务器地址配置键。</summary>
    public const string ServerUrlConfigKey = "update:serverUrl";

    /// <summary>当前版本配置键。</summary>
    public const string CurrentVersionConfigKey = "update:currentVersion";

    /// <summary>Manifest signature enforcement configuration key.</summary>
    public const string RequireSignedManifestConfigKey = "update:requireSignedManifest";

    /// <summary>Base64 SubjectPublicKeyInfo ECDSA P-256 update-signing public key configuration key.</summary>
    public const string SigningPublicKeyConfigKey = "update:signingPublicKey";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IConfigService _configService;
    private readonly IPathService _pathService;
    private readonly ILogService _logService;
    private readonly DiagnosticsService? _diagnosticsService;
    private readonly HttpClient _httpClient;

    /// <summary>以配置、路径与日志服务创建更新服务。</summary>
    /// <param name="configService">配置服务。</param>
    /// <param name="pathService">路径服务。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="httpClient">HTTP 客户端（测试可注入）。</param>
    /// <param name="diagnosticsService">诊断服务（错误汇总上送），可为 <see langword="null"/>。</param>
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
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _diagnosticsService = diagnosticsService;
    }

    /// <summary>
    /// 语义化版本比较（x.y[.z[.w]]，缺段补 0）。a&gt;b 返回正数。
    /// </summary>
    /// <param name="a">版本 a。</param>
    /// <param name="b">版本 b。</param>
    public static int CompareVersions(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        int[] partsA = ParseParts(a);
        int[] partsB = ParseParts(b);
        for (int i = 0; i < 4; i++)
        {
            if (partsA[i] != partsB[i])
            {
                return partsA[i].CompareTo(partsB[i]);
            }
        }

        return 0;

        static int[] ParseParts(string version)
        {
            string[] segments = version.Split('.');
            var parts = new int[4];
            for (int i = 0; i < segments.Length && i < 4; i++)
            {
                _ = int.TryParse(segments[i], out parts[i]);
            }

            return parts;
        }
    }

    /// <summary>
    /// 检查更新：拉取服务器清单，版本比较 + 计算文件级增量。
    /// </summary>
    /// <param name="installDirectory">当前安装目录（用于本地哈希比对）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(string installDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        string serverUrl = _configService.GetValue(ServerUrlConfigKey, string.Empty);
        string currentVersion = _configService.GetValue(CurrentVersionConfigKey, "1.0.0");
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return new UpdateCheckResult(false, currentVersion, null, []);
        }

        UpdateManifest? manifest = await FetchManifestAsync(serverUrl, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            await EmitDiagnosticsAsync(currentVersion, latestVersion: null, serverUrl, cancellationToken).ConfigureAwait(false);
            return new UpdateCheckResult(false, currentVersion, null, []);
        }

        if (!TryValidateManifest(manifest, out string validationError) ||
            !TryVerifyManifestSignature(manifest, out validationError))
        {
            _logService.LogEvent(LogEventKind.Exception, "Rejected unsafe update manifest",
                new Dictionary<string, object?> { ["Reason"] = validationError });
            return new UpdateCheckResult(false, currentVersion, null, []);
        }

        if (CompareVersions(manifest.Version, currentVersion) <= 0)
        {
            await EmitDiagnosticsAsync(currentVersion, latestVersion: null, serverUrl, cancellationToken).ConfigureAwait(false);
            return new UpdateCheckResult(false, currentVersion, null, []);
        }

        var filesToDownload = new List<UpdateFileEntry>();
        foreach (UpdateFileEntry entry in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveContainedPath(installDirectory, entry.Path, out string localPath))
            {
                _logService.LogEvent(LogEventKind.Exception, "Rejected unsafe update install path",
                    new Dictionary<string, object?> { ["Path"] = entry.Path });
                return new UpdateCheckResult(false, currentVersion, null, []);
            }
            if (!File.Exists(localPath) ||
                !await VerifyHashAsync(localPath, entry.Sha256, cancellationToken).ConfigureAwait(false))
            {
                filesToDownload.Add(entry);
            }
        }

        await EmitDiagnosticsAsync(currentVersion, manifest.Version, serverUrl, cancellationToken).ConfigureAwait(false);
        return new UpdateCheckResult(filesToDownload.Count > 0, currentVersion, manifest, filesToDownload);
    }

    /// <summary>
    /// 生成更新诊断上下文：错误汇总（仅事件种类/异常类型/次数，不含任何路径与用户名）
    /// 写入本地 update-diagnostics.json；服务器已配置时随请求上送（失败静默降级不影响更新）。
    /// </summary>
    /// <param name="currentVersion">当前版本。</param>
    /// <param name="latestVersion">服务器最新版本（无更新时为 <see langword="null"/>）。</param>
    /// <param name="serverUrl">更新服务器地址（空字符串表示未配置）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<string> EmitDiagnosticsAsync(
        string currentVersion, string? latestVersion, string serverUrl, CancellationToken cancellationToken = default)
    {
        ErrorSummary summary = _diagnosticsService is not null
            ? await _diagnosticsService.BuildErrorSummaryAsync(cancellationToken).ConfigureAwait(false)
            : new ErrorSummary(DateTimeOffset.UtcNow, 30, 0, 0, [], []);

        var payload = new
        {
            currentVersion,
            latestVersion,
            generatedUtc = DateTimeOffset.UtcNow,
            warnings = summary.TotalWarnings,
            errors = summary.TotalErrors,
            byEventKind = summary.ByEventKind,
            topExceptions = summary.TopExceptions,
        };
        string json = JsonSerializer.Serialize(payload, JsonOptions);

        string diagnosticsFile = Path.Combine(
            _pathService.EnsureDirectory(_pathService.DataDirectory), "update-diagnostics.json");
        await File.WriteAllTextAsync(diagnosticsFile, json, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(serverUrl))
        {
            try
            {
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await _httpClient
                    .PostAsync(serverUrl.TrimEnd('/') + "/diagnostics", content, cancellationToken).ConfigureAwait(false);
                _logService.LogEvent(LogEventKind.UserAction, "更新诊断已上送", new Dictionary<string, object?>
                {
                    ["StatusCode"] = (int)response.StatusCode,
                });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // 上送失败静默降级：本地存档已完成，不影响更新主流程
                _logService.LogException(ex, "UpdateService.EmitDiagnosticsAsync.Upload");
            }
        }

        return json;
    }

    /// <summary>
    /// 下载变更文件到暂存目录并逐文件校验 SHA256。失败清理暂存目录（回滚）。
    /// </summary>
    /// <param name="manifest">更新清单。</param>
    /// <param name="filesToDownload">待下载文件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>暂存目录路径。</returns>
    public async Task<string> DownloadUpdateAsync(
        UpdateManifest manifest,
        IReadOnlyList<UpdateFileEntry> filesToDownload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(filesToDownload);
        if (!TryValidateManifest(manifest, out string validationError) ||
            !TryVerifyManifestSignature(manifest, out validationError))
        {
            throw new LeslieCleanerException($"Unsafe update manifest: {validationError}");
        }

        string serverUrl = _configService.GetValue(ServerUrlConfigKey, string.Empty);
        string stagingRoot = _pathService.EnsureDirectory(
            Path.Combine(_pathService.EnsureDirectory(_pathService.DataDirectory), "UpdateStaging"));
        if (!TryResolveContainedPath(stagingRoot, manifest.Version, out string stagingDirectory))
        {
            throw new LeslieCleanerException("Unsafe update version directory.");
        }
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(stagingDirectory);
        _logService.LogEvent(LogEventKind.UserAction, "更新下载开始", new Dictionary<string, object?>
        {
            ["Version"] = manifest.Version,
            ["Files"] = filesToDownload.Count,
        });

        try
        {
            foreach (UpdateFileEntry entry in filesToDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryResolveContainedPath(stagingDirectory, entry.Path, out string destination))
                {
                    throw new LeslieCleanerException($"Unsafe update file path: {entry.Path}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                string fileUrl = serverUrl.TrimEnd('/') + "/" + entry.Url.TrimStart('/');
                await using (Stream source = await _httpClient.GetStreamAsync(fileUrl, cancellationToken).ConfigureAwait(false))
                await using (FileStream target = new(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }

                if (!await VerifyHashAsync(destination, entry.Sha256, cancellationToken).ConfigureAwait(false))
                {
                    throw new LeslieCleanerException($"更新文件校验失败（SHA256 不匹配）: {entry.Path}");
                }
            }
        }
        catch (Exception)
        {
            // 校验/下载失败：清理暂存目录，回滚到未更新状态
            try
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
            }

            throw;
        }

        // 暂存清单供 Updater 使用
        await File.WriteAllTextAsync(
            Path.Combine(stagingDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);

        _logService.LogEvent(LogEventKind.UserAction, "更新下载完成，等待应用",
            new Dictionary<string, object?> { ["Version"] = manifest.Version });
        return stagingDirectory;
    }

    /// <summary>
    /// 应用更新：启动 Updater 辅助进程（等待主程序退出 → 替换 → 重启）。
    /// 调用方应在返回后立即退出主程序。
    /// </summary>
    /// <param name="stagingDirectory">暂存目录。</param>
    /// <param name="installDirectory">安装目录。</param>
    /// <param name="mainExecutable">主程序路径（更新完成后重启）。</param>
    /// <param name="updaterExecutable">Updater 辅助程序路径。</param>
    public void ApplyUpdate(
        string stagingDirectory,
        string installDirectory,
        string mainExecutable,
        string updaterExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(mainExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(updaterExecutable);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = updaterExecutable,
            Arguments = $"--pid {Environment.ProcessId} --source \"{stagingDirectory}\" --target \"{installDirectory}\" --restart \"{mainExecutable}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        });

        _logService.LogEvent(LogEventKind.UserAction, "Updater 辅助进程已启动，主程序即将退出以完成替换",
            new Dictionary<string, object?> { ["Pid"] = Environment.ProcessId });
    }

    /// <summary>
    /// 校验文件 SHA256。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <param name="expectedSha256Hex">期望哈希（hex）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task<bool> VerifyHashAsync(string filePath, string expectedSha256Hex, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).Equals(expectedSha256Hex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds the deterministic payload covered by an update-manifest signature.</summary>
    public static byte[] GetManifestSigningPayload(UpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var payload = new
        {
            version = manifest.Version,
            files = manifest.Files.Select(static entry => new
            {
                path = entry.Path,
                sha256 = entry.Sha256,
                size = entry.Size,
                url = entry.Url,
            }),
            releaseNotes = manifest.ReleaseNotes,
            signingKeyId = manifest.SigningKeyId,
        };
        return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    }

    private async Task<UpdateManifest?> FetchManifestAsync(string serverUrl, CancellationToken cancellationToken)
    {
        try
        {
            string manifestUrl = serverUrl.TrimEnd('/') + "/manifest.json";
            await using Stream stream = await _httpClient.GetStreamAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logService.LogException(ex, "UpdateService.FetchManifestAsync");
            return null;
        }
    }

    private static bool TryValidateManifest(UpdateManifest manifest, out string error)
    {
        if (!Version.TryParse(manifest.Version, out _))
        {
            error = "Version is not a valid numeric version.";
            return false;
        }

        if (manifest.Files is null)
        {
            error = "File list is missing.";
            return false;
        }

        foreach (UpdateFileEntry entry in manifest.Files)
        {
            if (!TryResolveContainedPath(Path.GetTempPath(), entry.Path, out _))
            {
                error = $"File path escapes its root: {entry.Path}";
                return false;
            }

            if (entry.Size < 0 || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
            {
                error = $"File metadata is invalid: {entry.Path}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryVerifyManifestSignature(UpdateManifest manifest, out string error)
    {
        bool requireSignature = _configService.GetValue(RequireSignedManifestConfigKey, true);
        if (!requireSignature && string.IsNullOrWhiteSpace(manifest.Signature))
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
            byte[] signature = Convert.FromBase64String(manifest.Signature);
            byte[] subjectPublicKeyInfo = Convert.FromBase64String(publicKey);
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
            if (!verifier.VerifyData(GetManifestSigningPayload(manifest), signature, HashAlgorithmName.SHA256))
            {
                error = "Manifest signature verification failed.";
                return false;
            }
        }
        catch (CryptographicException)
        {
            error = "Manifest signing key or signature is malformed.";
            return false;
        }
        catch (FormatException)
        {
            error = "Manifest signing key or signature is not Base64.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryResolveContainedPath(string rootDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }

        string fullRoot = Path.GetFullPath(rootDirectory);
        string rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}

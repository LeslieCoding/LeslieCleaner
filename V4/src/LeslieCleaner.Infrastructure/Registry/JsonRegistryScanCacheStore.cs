using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 原子、有界的 JSON 注册表扫描快存。损坏或超限快存会安全降级为重新扫描，
/// 不会把无法验证的旧结果显示给用户。
/// </summary>
public sealed class JsonRegistryScanCacheStore : IRegistryScanCacheStore
{
    private const long MaximumCacheBytes = 32L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _cachePath;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>使用用户配置目录创建快存。</summary>
    public JsonRegistryScanCacheStore(IPathService pathService, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _cachePath = Path.Combine(
            pathService.EnsureDirectory(pathService.ConfigDirectory),
            "registry-scan-cache-v1.json");
    }

    /// <summary>使用显式文件位置创建快存，用于受控测试。</summary>
    public JsonRegistryScanCacheStore(string cachePath, ILogService logService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        _cachePath = Path.GetFullPath(cachePath);
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <inheritdoc />
    public async Task<RegistryScanCacheSnapshot?> TryReadAsync(
        string fingerprint,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var info = new FileInfo(_cachePath);
            if (info.Length <= 0 || info.Length > MaximumCacheBytes)
            {
                TryDeleteCache();
                return null;
            }

            try
            {
                await using FileStream stream = new(
                    _cachePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 64 * 1024, useAsync: true);
                RegistryScanCacheSnapshot? snapshot = await JsonSerializer.DeserializeAsync<RegistryScanCacheSnapshot>(
                    stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (snapshot is null || !snapshot.IsUsable(fingerprint, utcNow))
                {
                    return null;
                }

                return snapshot with { Findings = snapshot.Findings.ToArray() };
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                _logService.LogException(exception, "Registry cache read failed; falling back to fresh scan");
                TryDeleteCache();
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        RegistryScanCacheSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ExpiresUtc <= snapshot.CreatedUtc ||
            snapshot.ExpiresUtc - snapshot.CreatedUtc > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Registry cache lifetime must be between zero and seven days.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? directory = Path.GetDirectoryName(_cachePath);
        string temporaryPath = _cachePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(directory!);
            await using (FileStream stream = new(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(temporaryPath).Length > MaximumCacheBytes)
            {
                throw new InvalidDataException("Registry cache exceeds the 32 MiB safety limit.");
            }

            if (File.Exists(_cachePath))
            {
                File.Replace(temporaryPath, _cachePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _cachePath);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TryDeleteCache();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void TryDeleteCache() => TryDelete(_cachePath);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Cache invalidation is best-effort; the fingerprint gate still prevents reuse.
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only cache is harmless because mismatched/expired data is never returned.
        }
    }
}

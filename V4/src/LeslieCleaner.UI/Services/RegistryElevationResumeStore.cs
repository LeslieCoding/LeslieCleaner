using System.IO;
using System.Text.Json;
using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.UI.Services;

/// <summary>提权重启前保存的注册表目标；恢复后仅用于重新选择真实扫描结果。</summary>
public sealed record RegistryResumeTarget(string KeyPath, string? ValueName);

/// <summary>
/// 注册表提权恢复的一次性短时状态。文件不授权任何删除，消费后立即移除，
/// 页面仍会重新扫描且要求用户再次确认修复。
/// </summary>
public sealed class RegistryElevationResumeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(10);
    private const int MaximumTargets = 200;
    private const long MaximumFileBytes = 1024 * 1024;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>在当前用户配置目录创建恢复存储。</summary>
    public RegistryElevationResumeStore(IPathService pathService)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        _path = Path.Combine(pathService.EnsureDirectory(pathService.ConfigDirectory),
            "registry-elevation-resume.json");
    }

    /// <summary>原子保存去重后的目标并返回随机恢复令牌。</summary>
    public async Task<string> SaveAsync(
        IReadOnlyList<RegistryResumeTarget> targets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        RegistryResumeTarget[] normalized = targets
            .Where(static target => !string.IsNullOrWhiteSpace(target.KeyPath))
            .DistinctBy(static target => target.KeyPath +
                (target.ValueName is null ? "\0K" : "\0V" + target.ValueName),
                StringComparer.OrdinalIgnoreCase)
            .Take(MaximumTargets)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one registry resume target is required.", nameof(targets));
        }

        string token = Guid.NewGuid().ToString("N");
        var snapshot = new RegistryResumeSnapshot(token, DateTimeOffset.UtcNow, normalized);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, _path, overwrite: true);
            return token;
        }
        finally
        {
            TryDeletePath(temporary);
            _gate.Release();
        }
    }

    /// <summary>验证令牌、期限与边界后一次性消费目标；任何无效状态均返回空。</summary>
    public async Task<IReadOnlyList<RegistryResumeTarget>> ConsumeAsync(
        string? token, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(token, "N", out _))
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return [];
                }

                long length = new FileInfo(_path).Length;
                if (length <= 0 || length > MaximumFileBytes)
                {
                    return [];
                }

                await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                RegistryResumeSnapshot? snapshot = await JsonSerializer.DeserializeAsync<RegistryResumeSnapshot>(
                    stream, JsonOptions, cancellationToken).ConfigureAwait(false);

                if (snapshot is null || !string.Equals(snapshot.Token, token, StringComparison.Ordinal) ||
                    snapshot.Targets.Count is <= 0 or > MaximumTargets ||
                    snapshot.Targets.Any(static target => string.IsNullOrWhiteSpace(target.KeyPath)))
                {
                    return [];
                }

                TimeSpan age = DateTimeOffset.UtcNow - snapshot.CreatedUtc;
                if (age < TimeSpan.Zero || age > MaximumAge)
                {
                    return [];
                }

                return snapshot.Targets.ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return [];
            }
            finally
            {
                TryDelete();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>UAC 被取消时丢弃尚未消费的恢复状态。</summary>
    public async Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { TryDelete(); }
        finally { _gate.Release(); }
    }

    private void TryDelete()
    {
        TryDeletePath(_path);
    }

    private static void TryDeletePath(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private sealed record RegistryResumeSnapshot(
        string Token, DateTimeOffset CreatedUtc, IReadOnlyList<RegistryResumeTarget> Targets);
}

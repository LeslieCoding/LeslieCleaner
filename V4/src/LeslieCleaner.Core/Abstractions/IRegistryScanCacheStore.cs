using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>注册表扫描的七天持久化快存储。</summary>
public interface IRegistryScanCacheStore
{
    /// <summary>读取指纹一致且未过期的快照。</summary>
    Task<RegistryScanCacheSnapshot?> TryReadAsync(
        string fingerprint,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>以原子替换方式保存快照。</summary>
    Task WriteAsync(
        RegistryScanCacheSnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>使快照失效。</summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>计算选定注册表位置与相关文件依赖的权威状态指纹。</summary>
public interface IRegistryStateFingerprintProvider
{
    /// <summary>计算当前状态的 SHA-256 指纹。</summary>
    Task<string> ComputeAsync(CancellationToken cancellationToken = default);
}

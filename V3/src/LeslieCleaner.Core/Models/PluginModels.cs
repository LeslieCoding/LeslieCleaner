namespace LeslieCleaner.Core.Models;

/// <summary>插件信任状态。</summary>
public enum PluginTrustStatus
{
    /// <summary>清单和程序集已验证，且插件已启用。</summary>
    Ready,

    /// <summary>清单和程序集已验证，但插件保持默认禁用状态。</summary>
    Disabled,

    /// <summary>插件清单无法解析或不符合安全字段要求。</summary>
    InvalidManifest,

    /// <summary>清单声明的程序集不存在。</summary>
    MissingAssembly,

    /// <summary>程序集内容与清单 SHA-256 不一致。</summary>
    HashMismatch,

    /// <summary>程序集路径尝试超出插件自身目录。</summary>
    UnsafePath,
}

/// <summary>经过本地验证后的插件描述。</summary>
public sealed record PluginDescriptor(
    string Id,
    string Name,
    string Version,
    string Description,
    string ManifestPath,
    string AssemblyPath,
    PluginTrustStatus TrustStatus,
    string? Detail);

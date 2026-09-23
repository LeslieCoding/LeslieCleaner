using System.Text.Json.Serialization;

namespace LeslieCleaner.Infrastructure.Updating;

/// <summary>更新清单中的单个文件。</summary>
/// <param name="Path">相对安装目录的文件路径。</param>
/// <param name="Sha256">SHA256 校验（hex 小写）。</param>
/// <param name="Size">文件大小（字节）。</param>
/// <param name="Url">相对服务器的下载地址。</param>
public sealed record UpdateFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("url")] string Url);

/// <summary>版本更新清单（服务器 manifest.json）。</summary>
/// <param name="Version">版本号（语义化 x.y.z）。</param>
/// <param name="Files">本版本全量文件清单（文件级增量：客户端只下载本地缺失/哈希不同的文件）。</param>
/// <param name="ReleaseNotes">更新说明。</param>
/// <param name="Signature">Base64 ECDSA signature over the canonical manifest payload.</param>
/// <param name="SigningKeyId">Identifier of the public key used to verify the signature.</param>
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("files")] IReadOnlyList<UpdateFileEntry> Files,
    [property: JsonPropertyName("releaseNotes")] string? ReleaseNotes,
    [property: JsonPropertyName("signature")] string? Signature = null,
    [property: JsonPropertyName("signingKeyId")] string? SigningKeyId = null);

/// <summary>更新检查结果。</summary>
/// <param name="UpdateAvailable">是否有新版本。</param>
/// <param name="CurrentVersion">当前版本。</param>
/// <param name="Manifest">服务器清单（无更新时为 <see langword="null"/>）。</param>
/// <param name="FilesToDownload">需要下载的变更文件（本地缺失或哈希不同）。</param>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    UpdateManifest? Manifest,
    IReadOnlyList<UpdateFileEntry> FilesToDownload);

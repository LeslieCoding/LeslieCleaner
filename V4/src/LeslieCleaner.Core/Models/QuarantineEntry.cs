namespace LeslieCleaner.Core.Models;

/// <summary>隔离区条目（与 metadata.json 一一对应）。</summary>
public sealed record QuarantineEntry
{
    /// <summary>条目标识（亦为隔离目录名）。</summary>
    public required Guid Id { get; init; }

    /// <summary>文件原始绝对路径。</summary>
    public required string OriginalPath { get; init; }

    /// <summary>隔离区内载荷文件路径。</summary>
    public required string QuarantinedPath { get; init; }

    /// <summary>metadata.json 路径。</summary>
    public required string MetadataPath { get; init; }

    /// <summary>文件大小（字节）。</summary>
    public required long SizeBytes { get; init; }

    /// <summary>隔离时间（UTC）。</summary>
    public required DateTimeOffset QuarantinedUtc { get; init; }

    /// <summary>过期时间（UTC），超过后由 <c>PurgeExpiredAsync</c> 永久删除。</summary>
    public required DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>隔离原因。</summary>
    public required string Reason { get; init; }

    /// <summary>关联的清理操作标识，可为 <see langword="null"/>。</summary>
    public string? OperationId { get; init; }

    /// <summary>隔离过程中的错误描述；成功时为 <see langword="null"/>。</summary>
    public string? Error { get; init; }
}

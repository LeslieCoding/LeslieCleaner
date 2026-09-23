namespace LeslieCleaner.Core.Models;

/// <summary>密钥轮换结果。</summary>
public sealed record KeyRotationResult(string PreviousKeyId, string ActiveKeyId, DateTimeOffset RotatedUtc);

namespace LeslieCleaner.Core.Models;

/// <summary>面向界面的更新检查结果。</summary>
public sealed record UpdateStatus(
    string CurrentVersion,
    string? AvailableVersion,
    int FilesToDownload,
    string Message,
    DateTimeOffset CheckedUtc);

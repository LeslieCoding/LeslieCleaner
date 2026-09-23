namespace LeslieCleaner.Core.Models;

/// <summary>应用与插件安全评估结果。</summary>
public sealed record SecurityAssessment(
    bool ApplicationIntegrityValid,
    bool DatabaseIntegrityValid,
    bool AuditIntegrityValid,
    int TrustedPlugins,
    int RejectedPlugins,
    int Score,
    string Summary,
    DateTimeOffset EvaluatedUtc);

using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Validates a concrete file target immediately before a destructive cleanup operation.</summary>
public interface ICleanupTargetPolicy
{
    /// <summary>Returns an allow/block decision after canonical-path, reparse-point and Windows protection checks.</summary>
    CleanupTargetDecision EvaluateFile(string path);
}

/// <summary>A pre-execution cleanup target decision.</summary>
public sealed record CleanupTargetDecision(
    bool Allowed,
    string? NormalizedPath,
    OperationFailureKind FailureKind,
    string Reason);

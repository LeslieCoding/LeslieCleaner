using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Canonicalizes file-system input and rejects protected boundaries and link traversal.</summary>
public interface IProtectedPathPolicy
{
    /// <summary>Checks a path without touching its contents.</summary>
    PathSecurityDecision Evaluate(string path);
}

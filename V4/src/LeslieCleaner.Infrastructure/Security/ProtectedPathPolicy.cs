using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>Rejects malformed paths, protected roots, application core paths, and link traversal.</summary>
public sealed class ProtectedPathPolicy : IProtectedPathPolicy
{
    private readonly string[] _protectedRoots;

    /// <summary>Creates the policy from system, application, and security configuration boundaries.</summary>
    public ProtectedPathPolicy(IPathService pathService)
        : this(pathService, AppContext.BaseDirectory)
    {
    }

    /// <summary>Creates a policy with an explicit application directory for deterministic testing.</summary>
    public ProtectedPathPolicy(IPathService pathService, string applicationDirectory)
    {
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        _protectedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                applicationDirectory,
                pathService.ConfigDirectory,
            }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeBoundary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public PathSecurityDecision Evaluate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Denied(null, "Path is empty or whitespace.");
        if (path.IndexOf('\0') >= 0) return Denied(null, "Path contains a null character.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Denied(null, "Path cannot be canonicalized.");
        }

        string normalized = TrimEndingSeparators(fullPath);
        string? volumeRoot = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(volumeRoot) &&
            string.Equals(normalized, TrimEndingSeparators(volumeRoot), StringComparison.OrdinalIgnoreCase))
        {
            return Denied(normalized, "Volume roots are protected.");
        }

        if (_protectedRoots.Any(root => IsSameOrDescendant(normalized, root)))
        {
            return Denied(normalized, "Path is inside a protected system, application, or security boundary.");
        }

        try
        {
            if (ContainsExistingReparsePoint(normalized))
            {
                return Denied(normalized, "Path crosses a symbolic link, junction, or other reparse point.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Denied(normalized, "Path metadata could not be verified safely.");
        }

        return new PathSecurityDecision(true, normalized, "Path passed canonical boundary and link checks.",
            SecurityRiskLevel.Safe, "No action required.");
    }

    private static bool ContainsExistingReparsePoint(string path)
    {
        string? current = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(current)) return true;

        string remainder = path[current.Length..];
        foreach (string segment in remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }

        return false;
    }

    private static string NormalizeBoundary(string path) => TrimEndingSeparators(Path.GetFullPath(path));

    private static string TrimEndingSeparators(string path)
    {
        string? root = Path.GetPathRoot(path);
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 && !string.IsNullOrEmpty(root) ? root : trimmed;
    }

    private static bool IsSameOrDescendant(string candidate, string boundary) =>
        string.Equals(candidate, boundary, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(boundary + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static PathSecurityDecision Denied(string? path, string reason) => new(false, path, reason,
        SecurityRiskLevel.Critical, "Choose a non-protected local path that does not traverse a link.");
}

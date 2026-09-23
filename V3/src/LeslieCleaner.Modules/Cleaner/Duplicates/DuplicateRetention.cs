namespace LeslieCleaner.Modules.Cleaner.Duplicates;

/// <summary>智能保留规则（纯函数）。</summary>
public static class DuplicateRetention
{
    /// <summary>
    /// 按策略从重复组中选出保留文件，其余为可删除冗余。
    /// </summary>
    /// <param name="files">组内文件。</param>
    /// <param name="policy">保留策略。</param>
    /// <param name="preferredDirectory"><see cref="KeepPolicy.PreferredDirectory"/> 时的优先目录。</param>
    /// <returns>(保留文件, 冗余文件)。</returns>
    public static (string Keeper, IReadOnlyList<string> Redundant) Select(
        IReadOnlyList<string> files,
        KeepPolicy policy,
        string? preferredDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("文件组为空", nameof(files));
        }

        if (files.Count == 1)
        {
            return (files[0], []);
        }

        string keeper = policy switch
        {
            KeepPolicy.Newest => files
                .OrderByDescending(static f => SafeLastWriteTime(f))
                .ThenBy(static f => f, StringComparer.OrdinalIgnoreCase).First(),
            KeepPolicy.Oldest => files
                .OrderBy(static f => SafeLastWriteTime(f))
                .ThenBy(static f => f, StringComparer.OrdinalIgnoreCase).First(),
            KeepPolicy.ShortestPath => files
                .OrderBy(static f => f.Length)
                .ThenBy(static f => f, StringComparer.OrdinalIgnoreCase).First(),
            KeepPolicy.PreferredDirectory => files
                .OrderByDescending(f => IsUnderDirectory(f, preferredDirectory))
                .ThenBy(static f => f.Length)
                .ThenBy(static f => f, StringComparer.OrdinalIgnoreCase).First(),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null),
        };

        return (keeper, [.. files.Where(f => !f.Equals(keeper, StringComparison.OrdinalIgnoreCase))]);
    }

    private static bool IsUnderDirectory(string file, string? directory) =>
        !string.IsNullOrWhiteSpace(directory) &&
        file.StartsWith(
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static DateTime SafeLastWriteTime(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }
}

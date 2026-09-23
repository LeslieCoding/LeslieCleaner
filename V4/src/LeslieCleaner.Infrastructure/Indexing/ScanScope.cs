namespace LeslieCleaner.Infrastructure.Indexing;

/// <summary>
/// 索引扫描范围定义。允许区：Program Files / Program Files (x86) / AppData /
/// ProgramData / Documents / Desktop；禁区：Windows / WinSxS / System32 /
/// DriverStore / pagefile / hiberfil / swapfile。
/// 排除规则本身固定（圣经规定），但全部路径经环境变量与
/// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> 解析，不硬编码盘符与系统目录。
/// 匹配走 <see cref="MemoryExtensions"/> Span API，向量化友好、零分配。
/// </summary>
public static class ScanScope
{
    private static readonly string[] ForbiddenPrefixes = BuildForbiddenPrefixes();
    private static readonly string[] ForbiddenFileNames = ["pagefile.sys", "hiberfil.sys", "swapfile.sys"];

    /// <summary>
    /// 解析允许索引的根目录集合（仅返回实际存在的目录）。
    /// </summary>
    public static IReadOnlyList<string> GetAllowedRoots()
    {
        var roots = new List<string>(8);
        AddIfExists(roots, Environment.GetEnvironmentVariable("ProgramFiles"));
        AddIfExists(roots, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        return roots;
    }

    /// <summary>
    /// 判断路径是否位于禁区（Windows/WinSxS/System32/DriverStore 及交换文件）。
    /// </summary>
    /// <param name="path">完整路径。</param>
    public static bool IsForbidden(ReadOnlySpan<char> path)
    {
        foreach (string prefix in ForbiddenPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        ReadOnlySpan<char> fileName = Path.GetFileName(path);
        foreach (string forbidden in ForbiddenFileNames)
        {
            if (fileName.Equals(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断目录是否应被剪枝（禁区或重解析点之外的系统目录）。
    /// </summary>
    /// <param name="directoryPath">目录完整路径。</param>
    public static bool IsForbiddenDirectory(ReadOnlySpan<char> directoryPath) => IsForbidden(directoryPath);

    private static void AddIfExists(List<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            roots.Add(path);
        }
    }

    private static string[] BuildForbiddenPrefixes()
    {
        var prefixes = new List<string>(6);
        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windowsDir))
        {
            // 整个 Windows 目录树均为禁区（含 WinSxS/System32/DriverStore）
            prefixes.Add(Normalize(windowsDir));
        }

        return [.. prefixes];
    }

    private static string Normalize(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
}

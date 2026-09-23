using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Registry;

/// <summary>
/// 注册表无效项检测规则集。全部为纯函数：输入键值数据与存在性判定委托，
/// 输出命中项或 <see langword="null"/>；不触碰注册表与文件系统，可独立单元测试。
/// </summary>
public static class RegistryRules
{
    private static readonly string[] ExecutableExtensions = [".exe", ".com", ".cmd", ".bat", ".scr"];

    /// <summary>
    /// 无效卸载项：InstallLocation 或 DisplayIcon 指向不存在的路径。
    /// </summary>
    /// <param name="keyPath">卸载项键路径。</param>
    /// <param name="displayName">DisplayName 值。</param>
    /// <param name="installLocation">InstallLocation 值，可为 <see langword="null"/>。</param>
    /// <param name="displayIcon">DisplayIcon 值（可能带 ,0 后缀），可为 <see langword="null"/>。</param>
    /// <param name="pathExists">路径存在性判定（文件或目录）。</param>
    public static RegistryFinding? EvaluateUninstallEntry(
        string keyPath,
        string? displayName,
        string? installLocation,
        string? displayIcon,
        Func<string, bool> pathExists)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(pathExists);

        string? broken = FirstMissing(StripIconIndex(displayIcon), pathExists)
                         ?? FirstMissing(installLocation, pathExists);
        if (broken is null)
        {
            return null;
        }

        string name = string.IsNullOrWhiteSpace(displayName) ? keyPath : displayName;
        return new RegistryFinding(
            keyPath, ValueName: null, RegistryIssueKind.InvalidUninstallEntry,
            RegistryRiskScorer.Score(broken),
            $"卸载项 \"{name}\" 引用不存在的路径: {broken}", broken);
    }

    /// <summary>
    /// 无效扩展名：扩展名默认 ProgID 在 HKCR 下不存在。
    /// </summary>
    /// <param name="extensionKeyPath">扩展名键路径，如 HKCR\.xyz。</param>
    /// <param name="progId">默认值（ProgID），可为 <see langword="null"/>。</param>
    /// <param name="progIdExists">ProgID 键存在性判定。</param>
    public static RegistryFinding? EvaluateFileExtension(
        string extensionKeyPath,
        string? progId,
        Func<string, bool> progIdExists)
    {
        ArgumentNullException.ThrowIfNull(extensionKeyPath);
        ArgumentNullException.ThrowIfNull(progIdExists);

        if (string.IsNullOrWhiteSpace(progId) || progIdExists(progId))
        {
            return null;
        }

        return new RegistryFinding(
            extensionKeyPath, ValueName: null, RegistryIssueKind.InvalidFileExtension,
            RegistryRiskLevel.Safe,
            $"扩展名 {GetExtensionName(extensionKeyPath)} 指向不存在的 ProgID: {progId}", progId);
    }

    /// <summary>
    /// 无效 COM/CLSID：InprocServer32 默认值指向不存在的文件。
    /// </summary>
    /// <param name="clsidKeyPath">CLSID 键路径。</param>
    /// <param name="serverPath">InprocServer32 默认值，可为 <see langword="null"/>。</param>
    /// <param name="fileExists">文件存在性判定。</param>
    public static RegistryFinding? EvaluateInprocServer(
        string clsidKeyPath,
        string? serverPath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(clsidKeyPath);
        ArgumentNullException.ThrowIfNull(fileExists);

        string? normalized = NormalizePath(serverPath);
        if (normalized is null)
        {
            return null;
        }

        if (fileExists(normalized))
        {
            return null;
        }

        // COM InprocServer32 允许裸模块名（如 combase.dll），由加载器按系统目录搜索；
        // 仅在 System32/SysWOW64 均不存在时才判定无效，避免误删系统 COM 注册
        if (!Path.IsPathRooted(normalized))
        {
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string sysWow64 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
            if (fileExists(Path.Combine(system32, normalized)) || fileExists(Path.Combine(sysWow64, normalized)))
            {
                return null;
            }
        }

        return new RegistryFinding(
            clsidKeyPath, ValueName: null, RegistryIssueKind.InvalidComServer,
            RegistryRiskScorer.Score(normalized),
            $"COM 服务器文件不存在: {normalized}", normalized);
    }

    /// <summary>
    /// 失效字体引用：字体文件名在字体目录下不存在。
    /// </summary>
    /// <param name="keyPath">Fonts 键路径。</param>
    /// <param name="valueName">字体值名（显示名）。</param>
    /// <param name="fontFileName">字体文件名（可能为相对文件名）。</param>
    /// <param name="fontsDirectory">系统字体目录。</param>
    /// <param name="fileExists">文件存在性判定。</param>
    public static RegistryFinding? EvaluateFont(
        string keyPath,
        string valueName,
        string? fontFileName,
        string fontsDirectory,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(valueName);
        ArgumentNullException.ThrowIfNull(fileExists);

        string? normalized = NormalizePath(fontFileName);
        if (normalized is null)
        {
            return null;
        }

        string fullPath = Path.IsPathRooted(normalized) ? normalized : Path.Combine(fontsDirectory, normalized);
        if (fileExists(fullPath))
        {
            return null;
        }

        return new RegistryFinding(
            keyPath, valueName, RegistryIssueKind.InvalidFontReference,
            RegistryRiskScorer.Score(fullPath),
            $"字体 \"{valueName}\" 引用的文件不存在: {fullPath}", fullPath);
    }

    /// <summary>
    /// 失效路径值：值内容解析后指向不存在的路径（用于 Run 键等命令行值）。
    /// </summary>
    /// <param name="keyPath">键路径。</param>
    /// <param name="valueName">值名。</param>
    /// <param name="valueContent">值内容（可能为带参数的命令行）。</param>
    /// <param name="pathExists">路径存在性判定。</param>
    public static RegistryFinding? EvaluatePathValue(
        string keyPath,
        string valueName,
        string? valueContent,
        Func<string, bool> pathExists)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(pathExists);

        string? executable = ExtractExecutablePath(valueContent);
        if (executable is null || CommandImageExists(executable, pathExists))
        {
            return null;
        }

        return new RegistryFinding(
            keyPath, valueName, RegistryIssueKind.InvalidPathValue,
            RegistryRiskScorer.Score(executable),
            $"值 \"{valueName}\" 指向不存在的路径: {executable}", executable);
    }

    /// <summary>
    /// 从命令行文本提取可执行文件路径（处理引号与参数、环境变量展开）。
    /// 无法可靠提取时返回 <see langword="null"/>。
    /// </summary>
    /// <param name="commandLine">命令行文本。</param>
    public static string? ExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (ContainsUnresolvedEnvironmentToken(expanded))
        {
            // 未定义变量无法可靠判断实际路径；宁可不报，也不能据此建议删除启动项。
            return null;
        }

        ReadOnlySpan<char> span = expanded.AsSpan();
        if (span.StartsWith("\""))
        {
            int closing = span[1..].IndexOf('"');
            if (closing < 0)
            {
                return null;
            }

            return new string(span[1..(closing + 1)]);
        }

        // 无引号：允许路径中含空格，寻找首个处于命令映像边界的可执行扩展名。
        int imageEnd = FindCommandImageEnd(span);
        if (imageEnd > 0)
        {
            return new string(span[..imageEnd]);
        }

        int spaceIndex = span.IndexOf(' ');
        string candidate = spaceIndex < 0 ? expanded : new string(span[..spaceIndex]);
        return candidate.Length > 2 && (candidate.Contains('\\') || candidate.Contains('/')) ? candidate : null;
    }

    /// <summary>剥离 DisplayIcon 的 ",N" 图标索引后缀并规范化。</summary>
    /// <param name="iconValue">DisplayIcon 原始值。</param>
    public static string? StripIconIndex(string? iconValue)
    {
        string? path = NormalizePath(iconValue);
        if (path is null)
        {
            return null;
        }

        int commaIndex = path.LastIndexOf(',');
        if (commaIndex > 2 && int.TryParse(path[(commaIndex + 1)..], out _))
        {
            path = path[..commaIndex];
        }

        return path.Trim('"');
    }

    /// <summary>规范化路径值：展开环境变量、去引号；为空返回 <see langword="null"/>。</summary>
    /// <param name="path">原始路径。</param>
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return expanded.Length == 0 || ContainsUnresolvedEnvironmentToken(expanded) ? null : expanded;
    }

    private static bool CommandImageExists(string executable, Func<string, bool> pathExists)
    {
        if (pathExists(executable))
        {
            return true;
        }

        if (Path.IsPathRooted(executable) ||
            executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        // CreateProcess/ShellExecute 会在 Windows 系统目录及 PATH 中解析裸程序名。
        IEnumerable<string?> searchDirectories =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            .. (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static directory => directory.Trim('"')),
        ];
        foreach (string directory in searchDirectories
                     .Where(static directory => !string.IsNullOrWhiteSpace(directory))
                     .Select(static directory => directory!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (pathExists(Path.Combine(directory, executable)))
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                // 畸形 PATH 项不应使完整注册表扫描失败。
            }
        }

        return false;
    }

    private static int FindCommandImageEnd(ReadOnlySpan<char> commandLine)
    {
        int bestEnd = -1;
        foreach (string extension in ExecutableExtensions)
        {
            int searchOffset = 0;
            while (searchOffset < commandLine.Length)
            {
                int relativeIndex = commandLine[searchOffset..].IndexOf(extension, StringComparison.OrdinalIgnoreCase);
                if (relativeIndex < 0)
                {
                    break;
                }

                int end = searchOffset + relativeIndex + extension.Length;
                bool atBoundary = end == commandLine.Length || char.IsWhiteSpace(commandLine[end]);
                if (atBoundary && (bestEnd < 0 || end < bestEnd))
                {
                    bestEnd = end;
                    break;
                }

                searchOffset = end;
            }
        }

        return bestEnd;
    }

    private static bool ContainsUnresolvedEnvironmentToken(string value)
    {
        int opening = value.IndexOf('%');
        while (opening >= 0 && opening + 1 < value.Length)
        {
            int closing = value.IndexOf('%', opening + 1);
            if (closing > opening + 1)
            {
                return true;
            }

            opening = value.IndexOf('%', opening + 1);
        }

        return false;
    }

    private static string? FirstMissing(string? candidate, Func<string, bool> pathExists)
    {
        string? normalized = NormalizePath(candidate);
        return normalized is not null && !pathExists(normalized) ? normalized : null;
    }

    private static string GetExtensionName(string extensionKeyPath)
    {
        int separatorIndex = extensionKeyPath.LastIndexOf('\\');
        return separatorIndex < 0 ? extensionKeyPath : extensionKeyPath[(separatorIndex + 1)..];
    }
}

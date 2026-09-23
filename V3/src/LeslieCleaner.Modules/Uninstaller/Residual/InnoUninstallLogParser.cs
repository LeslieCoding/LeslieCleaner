using System.Text;

namespace LeslieCleaner.Modules.Uninstaller.Residual;

/// <summary>
/// Inno Setup 卸载日志（unins*.dat）容错解析器。官方格式未完整公开，
/// 按可解析部分提取：扫描二进制中的 UTF-16LE 绝对路径记录（X:\...），
/// 去重后返回。损坏/未知版本返回空集合而非抛错（容错优先）。
/// </summary>
public static class InnoUninstallLogParser
{
    private const int MinPathChars = 8;
    private const int MaxPathChars = 512;

    /// <summary>
    /// 从 unins*.dat 字节流提取全部 UTF-16LE 绝对路径。
    /// </summary>
    /// <param name="data">文件内容。</param>
    public static IReadOnlyList<string> ExtractPaths(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder(MaxPathChars);
        bool inPath = false;

        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            char c = (char)(data[i] | (data[i + 1] << 8));
            if (!inPath)
            {
                // 路径起始：盘符 + ":\\"（UTF-16LE 三字符序列）
                if (IsAsciiLetter(c) && i + 5 < data.Length &&
                    data[i + 2] == ':' && data[i + 3] == 0 &&
                    data[i + 4] == '\\' && data[i + 5] == 0)
                {
                    inPath = true;
                    builder.Clear();
                    builder.Append(c);
                }
            }
            else
            {
                if (c == 0 || !IsPathChar(c))
                {
                    CommitPath(paths, builder);
                    inPath = false;
                    // 当前字符若是新盘符起始，由下一轮重新判定
                    if (c != 0 && IsAsciiLetter(c))
                    {
                        i -= 2;
                    }
                }
                else if (builder.Length < MaxPathChars)
                {
                    builder.Append(c);
                }
                else
                {
                    inPath = false;
                    builder.Clear();
                }
            }
        }

        if (inPath)
        {
            CommitPath(paths, builder);
        }

        return [.. paths.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)];
    }

    private static void CommitPath(HashSet<string> paths, StringBuilder builder)
    {
        if (builder.Length >= MinPathChars)
        {
            paths.Add(builder.ToString());
        }
    }

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsPathChar(char c) =>
        c is >= ' ' and <= '~' || c >= 0x80; // 可打印 ASCII + 非 ASCII（中文等文件名）
}

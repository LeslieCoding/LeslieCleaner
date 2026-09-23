namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>
/// 生产扫描环境。位置键经环境变量与 <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>
/// 解析，零硬编码盘符与系统路径。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SystemScanEnvironment : IScanEnvironment
{
    /// <inheritdoc />
    public string? ResolveLocation(string locationKey)
    {
        ArgumentNullException.ThrowIfNull(locationKey);
        return locationKey switch
        {
            "Temp" => Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            "Windows" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "ProgramData" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LocalAppData" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoamingAppData" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemDrive" => Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))?.TrimEnd(Path.DirectorySeparatorChar),
            "UserProfile" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            _ => null,
        };
    }

    /// <summary>固定盘位置键前缀（回收站等需覆盖全部盘符的目标使用）。</summary>
    public const string FixedDrivesLocationKey = "FixedDrives";

    /// <inheritdoc />
    public IReadOnlyList<string> ExpandDirectories(string locationKey, string relativePattern)
    {
        // 全部固定盘展开：每个就绪固定盘根目录分别走相对路径展开（如各盘 $Recycle.Bin）
        if (string.Equals(locationKey, FixedDrivesLocationKey, StringComparison.OrdinalIgnoreCase))
        {
            var expanded = new List<string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                string root = drive.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar);
                expanded.AddRange(ExpandSingleBase(root, relativePattern));
            }

            return expanded;
        }

        string? basePath = ResolveLocation(locationKey);
        if (string.IsNullOrEmpty(basePath))
        {
            return [];
        }

        return ExpandSingleBase(basePath, relativePattern);
    }

    private static IReadOnlyList<string> ExpandSingleBase(string basePath, string relativePattern)
    {
        if (!Directory.Exists(basePath))
        {
            return [];
        }

        var current = new List<string> { basePath };
        foreach (string segment in relativePattern.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = new List<string>();
            if (segment == "*")
            {
                foreach (string directory in current)
                {
                    try
                    {
                        next.AddRange(Directory.EnumerateDirectories(directory));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // 无权限子目录跳过（该目录本身即候选失败，不影响其他分支）
                    }
                }
            }
            else
            {
                foreach (string directory in current)
                {
                    string candidate = Path.Combine(directory, segment);
                    if (Directory.Exists(candidate))
                    {
                        next.Add(candidate);
                    }
                }
            }

            current = next;
            if (current.Count == 0)
            {
                break;
            }
        }

        return current;
    }
}

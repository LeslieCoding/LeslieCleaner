using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Infrastructure.Services;

/// <summary>
/// 路径服务实现。全部特殊文件夹经 <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>
/// 解析，不硬编码任何系统路径；应用子目录名固定为产品名 LeslieCleaner。
/// </summary>
public sealed class PathService : IPathService
{
    private const string ProductDirectoryName = "LeslieCleaner";

    /// <inheritdoc />
    public string ProgramDataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductDirectoryName);

    /// <inheritdoc />
    public string QuarantineDirectory { get; }

    /// <inheritdoc />
    public string BackupDirectory { get; }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductDirectoryName, "Logs");

    /// <inheritdoc />
    public string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductDirectoryName, "Config");

    /// <summary>创建路径服务实例并解析全部目录。</summary>
    public PathService()
    {
        QuarantineDirectory = Path.Combine(ProgramDataRoot, "Quarantine");
        BackupDirectory = Path.Combine(ProgramDataRoot, "Backups");
        DataDirectory = Path.Combine(ProgramDataRoot, "Data");
    }

    /// <inheritdoc />
    public string EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(path);
        return path;
    }
}

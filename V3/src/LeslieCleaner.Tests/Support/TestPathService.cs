using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Tests.Support;

/// <summary>测试专用路径服务：全部目录锚定在调用方提供的临时根目录下，绝不触碰真实系统目录。</summary>
public sealed class TestPathService : IPathService
{
    public TestPathService(string root)
    {
        ProgramDataRoot = Path.Combine(root, "ProgramData", "LeslieCleaner");
        QuarantineDirectory = Path.Combine(ProgramDataRoot, "Quarantine");
        BackupDirectory = Path.Combine(ProgramDataRoot, "Backups");
        DataDirectory = Path.Combine(ProgramDataRoot, "Data");
        LogDirectory = Path.Combine(root, "LocalAppData", "LeslieCleaner", "Logs");
        ConfigDirectory = Path.Combine(root, "LocalAppData", "LeslieCleaner", "Config");
    }

    public string ProgramDataRoot { get; }

    public string QuarantineDirectory { get; }

    public string BackupDirectory { get; }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string ConfigDirectory { get; }

    public string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

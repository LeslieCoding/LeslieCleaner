namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 路径服务契约。集中提供全部特殊文件夹路径，统一走
/// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>，禁止硬编码路径。
/// </summary>
public interface IPathService
{
    /// <summary>应用数据根目录：%ProgramData%\LeslieCleaner。</summary>
    string ProgramDataRoot { get; }

    /// <summary>隔离区目录：%ProgramData%\LeslieCleaner\Quarantine。</summary>
    string QuarantineDirectory { get; }

    /// <summary>备份目录：%ProgramData%\LeslieCleaner\Backups。</summary>
    string BackupDirectory { get; }

    /// <summary>数据库目录：%ProgramData%\LeslieCleaner\Data。</summary>
    string DataDirectory { get; }

    /// <summary>日志目录：%LocalAppData%\LeslieCleaner\Logs。</summary>
    string LogDirectory { get; }

    /// <summary>配置目录：%LocalAppData%\LeslieCleaner\Config。</summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// 确保指定目录存在（不存在则创建）。
    /// </summary>
    /// <param name="path">目录绝对路径。</param>
    /// <returns>目录路径本身，便于链式使用。</returns>
    string EnsureDirectory(string path);
}

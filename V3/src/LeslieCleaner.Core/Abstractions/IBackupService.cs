namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 备份服务契约。注册表清理前自动生成 .reg + metadata.json，支持一键恢复。
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 导出指定注册表项（含子项）为 .reg 文件并写入元数据。
    /// </summary>
    /// <param name="registryKeyPath">完整注册表路径，如 HKLM\Software\Example。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>备份记录。</returns>
    Task<Models.BackupRecord> BackupRegistryKeyAsync(string registryKeyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 备份指定注册表值。还原时只恢复该值，不会覆盖同一键中的其它后续修改。
    /// </summary>
    /// <param name="registryKeyPath">完整注册表键路径。</param>
    /// <param name="valueName">待备份的值名；默认值使用空字符串。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>备份记录。</returns>
    Task<Models.BackupRecord> BackupRegistryValueAsync(
        string registryKeyPath,
        string valueName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从备份恢复注册表（reg import）。
    /// </summary>
    /// <param name="backupId">备份记录标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RestoreAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出全部备份记录（按创建时间倒序）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Models.BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default);
}

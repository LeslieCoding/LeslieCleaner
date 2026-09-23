namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 注册表引擎契约。提供带取消与日志的注册表读写；
/// 任何删除/修改操作必须先经 <see cref="IBackupService"/> 备份方可执行。
/// </summary>
public interface IRegistryEngine
{
    /// <summary>
    /// 枚举指定键的直接子键名。键不存在时返回空集合。
    /// </summary>
    /// <param name="keyPath">完整注册表路径，如 HKCU\Software\Microsoft。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<string>> EnumerateSubKeysAsync(string keyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 枚举指定键的全部值名。键不存在时返回空集合。
    /// </summary>
    /// <param name="keyPath">完整注册表路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<string>> EnumerateValuesAsync(string keyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取值。键或值不存在时返回 <see langword="null"/>。
    /// </summary>
    /// <param name="keyPath">完整注册表路径。</param>
    /// <param name="valueName">值名；默认值为空字符串。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<object?> ReadValueAsync(string keyPath, string valueName = "", CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断键是否存在。
    /// </summary>
    /// <param name="keyPath">完整注册表路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<bool> KeyExistsAsync(string keyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除键（递归）。调用方必须已完成备份；本方法内部会再次校验备份记录存在，否则抛出异常。
    /// </summary>
    /// <param name="keyPath">完整注册表路径。</param>
    /// <param name="backupId">删除前生成的备份记录标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteKeyAsync(string keyPath, Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除单个值。与 <see cref="DeleteKeyAsync"/> 一样强制校验备份记录存在。
    /// </summary>
    /// <param name="keyPath">完整注册表路径。</param>
    /// <param name="valueName">待删除的值名。</param>
    /// <param name="backupId">删除前生成的备份记录标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteValueAsync(string keyPath, string valueName, Guid backupId, CancellationToken cancellationToken = default);
}

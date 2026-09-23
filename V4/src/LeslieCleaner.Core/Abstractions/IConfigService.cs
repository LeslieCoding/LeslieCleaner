namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 配置服务契约。JSON 存储 + FileSystemWatcher 热更新 + 变更通知 + 线程安全读写 + 默认值自举。
/// 配置目录：%LocalAppData%\LeslieCleaner\Config。
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 配置文件外部变更（热更新生效）时触发。键不存在于变更集时表示整体重载。
    /// </summary>
    event EventHandler<Models.ConfigChangedEventArgs>? Changed;

    /// <summary>
    /// 读取配置值；键不存在时写入并返回 <paramref name="defaultValue"/>（默认值自举）。
    /// </summary>
    /// <typeparam name="T">值类型，须可被 System.Text.Json 序列化。</typeparam>
    /// <param name="key">分层键，使用 ':' 分隔，如 "quarantine:retentionDays"。</param>
    /// <param name="defaultValue">默认值。</param>
    T GetValue<T>(string key, T defaultValue);

    /// <summary>
    /// 写入配置值并异步持久化，随后触发 <see cref="Changed"/>。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="key">分层键。</param>
    /// <param name="value">新值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 强制从磁盘重载配置（通常由文件监听自动触发，无需手动调用）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

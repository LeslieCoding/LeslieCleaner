namespace LeslieCleaner.Core.Models;

/// <summary>配置变更事件参数。</summary>
/// <param name="changedKeys">发生变更的配置键集合。</param>
/// <param name="isFullReload">是否为整体重载（无法精确 diff 时为 <see langword="true"/>）。</param>
public sealed class ConfigChangedEventArgs(IReadOnlyList<string> changedKeys, bool isFullReload) : EventArgs
{
    /// <summary>发生变更的配置键集合。</summary>
    public IReadOnlyList<string> ChangedKeys { get; } = changedKeys;

    /// <summary>是否为整体重载。</summary>
    public bool IsFullReload { get; } = isFullReload;
}

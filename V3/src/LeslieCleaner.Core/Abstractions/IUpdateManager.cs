using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>受校验更新检查契约。</summary>
public interface IUpdateManager
{
    /// <summary>检查已配置更新源，并仅返回通过清单解析和版本比较的结果。</summary>
    Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default);
}

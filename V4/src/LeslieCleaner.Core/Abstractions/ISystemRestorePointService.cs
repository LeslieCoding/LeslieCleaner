namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// 系统还原点服务契约。危险操作（批量清理、注册表清理、驱动/服务变更）前自动创建还原点。
/// </summary>
public interface ISystemRestorePointService
{
    /// <summary>
    /// 通过 WMI SystemRestore 类创建系统还原点。
    /// </summary>
    /// <param name="description">还原点描述（建议包含操作名，如 "LeslieCleaner Registry Clean"）。</param>
    /// <param name="cancellationToken">取消令牌（WMI 调用本身不可中断，取消在调用前与返回后生效）。</param>
    /// <returns>创建成功返回 <see langword="true"/>；系统保护未启用或被组策略禁用时返回 <see langword="false"/>。</returns>
    Task<bool> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}

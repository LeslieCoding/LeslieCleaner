namespace LeslieCleaner.Core.Abstractions;

/// <summary>
/// Windows Shell 回收站服务。此服务只处理系统回收站，不直接操作 <c>$Recycle.Bin</c> 目录。
/// </summary>
public interface ISystemRecycleBinService
{
    /// <summary>读取所有本地驱动器回收站的权威系统快照。</summary>
    Task<Models.RecycleBinSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有本地驱动器的回收站，并在返回前重新查询以验证实际结果。
    /// </summary>
    Task<Models.RecycleBinEmptyResult> EmptyAsync(CancellationToken cancellationToken = default);
}

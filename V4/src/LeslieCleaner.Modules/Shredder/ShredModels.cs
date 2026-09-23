namespace LeslieCleaner.Modules.Shredder;

/// <summary>粉碎方法。</summary>
public enum ShredMethod
{
    /// <summary>DoD 5220.22-M 3 遍（0x00 → 0xFF → 随机）。</summary>
    DoD3Pass,

    /// <summary>DoD 5220.22-M ECE 7 遍。</summary>
    DoD7Pass,

    /// <summary>Gutmann 35 遍。</summary>
    Gutmann35Pass,

    /// <summary>SSD 安全策略：加密随机单遍 + 截断 + 删除（SSD 覆写无意义且伤寿命）。</summary>
    SsdSafe,
}

/// <summary>队列中的粉碎任务。</summary>
/// <param name="Id">任务标识。</param>
/// <param name="Path">文件或文件夹路径。</param>
/// <param name="IsDirectory">是否文件夹。</param>
/// <param name="Status">状态。</param>
/// <param name="TotalFiles">包含文件数（扫描后填充）。</param>
/// <param name="ProcessedFiles">已处理文件数。</param>
/// <param name="Error">失败原因。</param>
public sealed record ShredTask(
    Guid Id,
    string Path,
    bool IsDirectory,
    ShredTaskStatus Status,
    int TotalFiles,
    int ProcessedFiles,
    string? Error);

/// <summary>任务状态。</summary>
public enum ShredTaskStatus
{
    /// <summary>排队中。</summary>
    Pending,

    /// <summary>执行中。</summary>
    Running,

    /// <summary>已完成。</summary>
    Completed,

    /// <summary>失败。</summary>
    Failed,

    /// <summary>已取消。</summary>
    Cancelled,

    /// <summary>被防护拦截。</summary>
    Blocked,
}

/// <summary>粉碎进度快照。</summary>
/// <param name="CurrentFile">当前文件。</param>
/// <param name="CurrentPass">当前遍数（1 起）。</param>
/// <param name="TotalPasses">总遍数。</param>
/// <param name="ProcessedFiles">已处理文件数。</param>
/// <param name="TotalFiles">总文件数。</param>
/// <param name="ProcessedBytes">当前文件已覆写字节。</param>
/// <param name="TotalBytes">当前文件总字节。</param>
public sealed record ShredProgress(
    string CurrentFile,
    int CurrentPass,
    int TotalPasses,
    int ProcessedFiles,
    int TotalFiles,
    long ProcessedBytes,
    long TotalBytes);

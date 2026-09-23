namespace LeslieCleaner.Modules.Startup;

/// <summary>启动项类别。</summary>
public enum StartupItemKind
{
    /// <summary>注册表 Run/RunOnce（64 位 / 32 位 / HKCU / HKLM）。</summary>
    RegistryRun,

    /// <summary>Startup 文件夹（用户 / 公共）。</summary>
    StartupFolder,

    /// <summary>计划任务。</summary>
    ScheduledTask,

    /// <summary>服务自启动。</summary>
    Service,

    /// <summary>驱动自启动。</summary>
    Driver,
}

/// <summary>启动影响评级。</summary>
public enum StartupImpact
{
    /// <summary>低（系统组件/微软签名）。</summary>
    Low,

    /// <summary>中。</summary>
    Medium,

    /// <summary>高（常见重型程序/无签名第三方）。</summary>
    High,
}

/// <summary>一个启动项。</summary>
/// <param name="Id">稳定标识（类别+位置+名称的哈希无关拼接）。</param>
/// <param name="Name">显示名。</param>
/// <param name="Kind">类别。</param>
/// <param name="Location">位置描述（键路径/文件夹/任务名/服务名）。</param>
/// <param name="Command">命令行或 ImagePath，可为 <see langword="null"/>。</param>
/// <param name="Publisher">发布者（版本信息），可为 <see langword="null"/>。</param>
/// <param name="Enabled">当前是否启用。</param>
/// <param name="Impact">启动影响评级。</param>
/// <param name="Suggestion">规则引擎生成的建议文本（引用真实依据）。</param>
public sealed record StartupItem(
    string Id,
    string Name,
    StartupItemKind Kind,
    string Location,
    string? Command,
    string? Publisher,
    bool Enabled,
    StartupImpact Impact,
    string Suggestion);

/// <summary>禁用/启用操作结果。</summary>
/// <param name="ItemId">启动项标识。</param>
/// <param name="Success">是否成功。</param>
/// <param name="Error">失败原因。</param>
public sealed record StartupChangeResult(string ItemId, bool Success, string? Error);

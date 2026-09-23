namespace LeslieCleaner.Modules.Uninstaller;

/// <summary>应用来源类型。</summary>
public enum AppSourceType
{
    /// <summary>Win32 注册表卸载项。</summary>
    Win32,

    /// <summary>MSI（Windows Installer）。</summary>
    Msi,

    /// <summary>MSIX/Appx/UWP。</summary>
    Appx,

    /// <summary>Microsoft Store 应用。</summary>
    Store,

    /// <summary>Winget。</summary>
    Winget,

    /// <summary>Steam 游戏。</summary>
    Steam,

    /// <summary>Epic 游戏。</summary>
    Epic,

    /// <summary>Battle.net 游戏。</summary>
    BattleNet,

    /// <summary>Adobe 套件。</summary>
    Adobe,

    /// <summary>Office 套件。</summary>
    Office,

    /// <summary>Visual Studio 组件。</summary>
    VisualStudio,

    /// <summary>Docker/WSL 环境。</summary>
    DockerWsl,

    /// <summary>开发环境（Node/Python/Java）。</summary>
    DevEnvironment,
}

/// <summary>一个已安装应用记录。</summary>
/// <param name="Id">稳定标识（来源+键名/appid）。</param>
/// <param name="Name">显示名。</param>
/// <param name="Version">版本，可为 <see langword="null"/>。</param>
/// <param name="Publisher">发布者，可为 <see langword="null"/>。</param>
/// <param name="SizeBytes">估算大小（字节）。</param>
/// <param name="InstallDate">安装日期，可为 <see langword="null"/>。</param>
/// <param name="InstallLocation">安装位置，可为 <see langword="null"/>。</param>
/// <param name="UninstallString">卸载命令，可为 <see langword="null"/>。</param>
/// <param name="Source">来源类型。</param>
/// <param name="ProductCode">MSI ProductCode，可为 <see langword="null"/>。</param>
/// <param name="UpgradeCode">MSI UpgradeCode，可为 <see langword="null"/>。</param>
public sealed record InstalledApp(
    string Id,
    string Name,
    string? Version,
    string? Publisher,
    long SizeBytes,
    DateOnly? InstallDate,
    string? InstallLocation,
    string? UninstallString,
    AppSourceType Source,
    string? ProductCode,
    string? UpgradeCode);

/// <summary>单个卸载任务结果。</summary>
/// <param name="AppId">应用标识。</param>
/// <param name="AppName">应用名。</param>
/// <param name="Success">是否成功（进程退出码 0 / 协议已发起）。</param>
/// <param name="ExitCode">卸载进程退出码；协议方式/被跳过时为 <see langword="null"/>。</param>
/// <param name="Method">实际使用的卸载方式。</param>
/// <param name="TimedOut">是否超时被熔断。</param>
/// <param name="Error">失败原因。</param>
public sealed record UninstallTaskResult(
    string AppId,
    string AppName,
    bool Success,
    int? ExitCode,
    string Method,
    bool TimedOut,
    string? Error);

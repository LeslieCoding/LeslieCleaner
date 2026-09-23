namespace LeslieCleaner.Modules.Startup.Classification;

/// <summary>启动项来源分类（四级）。</summary>
public enum StartupSourceKind
{
    /// <summary>Microsoft Windows（系统与微软官方组件）。</summary>
    MicrosoftWindows,

    /// <summary>Hardware Drivers（驱动/OEM 厂商组件）。</summary>
    HardwareDriver,

    /// <summary>User Applications（用户安装的软件，主管理区）。</summary>
    UserApplication,

    /// <summary>Unknown（来源无法识别，黄色警告）。</summary>
    StartupUnknown,
}

/// <summary>分类结果。</summary>
/// <param name="Kind">来源分类。</param>
/// <param name="Reasons">判定依据（用于 UI 展示与 AI 建议，全部真实因素）。</param>
/// <param name="Confidence">置信度 0.0-1.0。</param>
public sealed record StartupClassification(StartupSourceKind Kind, IReadOnlyList<string> Reasons, double Confidence);

/// <summary>启动影响等级。</summary>
public enum StartupImpactLevel
{
    /// <summary>极高。</summary>
    Critical,

    /// <summary>高。</summary>
    High,

    /// <summary>中。</summary>
    Medium,

    /// <summary>低。</summary>
    Low,

    /// <summary>极低。</summary>
    Minimal,
}

/// <summary>启动影响评估（估计值，UI 需标注）。</summary>
/// <param name="Stars">星级 1-5。</param>
/// <param name="Level">等级。</param>
/// <param name="CpuEstimate">CPU 影响估计（%）。</param>
/// <param name="DiskEstimateMB">磁盘 IO 估计（MB）。</param>
/// <param name="MemoryEstimateMB">内存占用估计（MB）。</param>
/// <param name="TimeEstimateSeconds">启动耗时估计（秒）。</param>
public sealed record StartupImpactEstimate(
    int Stars,
    StartupImpactLevel Level,
    double CpuEstimate,
    double DiskEstimateMB,
    double MemoryEstimateMB,
    double TimeEstimateSeconds);

/// <summary>签名验证结果。</summary>
/// <param name="IsValid">签名是否有效（WinVerifyTrust）。</param>
/// <param name="SignerSubject">签名者主题（X.509 Subject）；无签名/验证失败为 <see langword="null"/>。</param>
/// <param name="VerificationState">Whether file-signature verification was performed.</param>
public sealed record SignatureInfo(
    bool IsValid,
    string? SignerSubject,
    SignatureVerificationState VerificationState = SignatureVerificationState.Verified);

/// <summary>签名验证状态。</summary>
public enum SignatureVerificationState
{
    /// <summary>尚未执行深度验证。</summary>
    NotChecked,

    /// <summary>已执行验证，签名有效或无效由 <see cref="SignatureInfo.IsValid"/> 表示。</summary>
    Verified,
}

/// <summary>禁用保护级别。</summary>
public enum DisableProtection
{
    /// <summary>允许直接禁用。</summary>
    Allowed,

    /// <summary>需"可能影响硬件功能"确认。</summary>
    ConfirmHardware,

    /// <summary>系统关键启动项：必须弹三后果警告，默认取消。</summary>
    ConfirmSystemCritical,
}

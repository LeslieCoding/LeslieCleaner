using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>统一安全评估和本地审计完整性契约。</summary>
public interface ISecurityManager
{
    /// <summary>检查应用完整性基线、审计哈希链和插件信任状态。</summary>
    Task<SecurityAssessment> AssessAsync(CancellationToken cancellationToken = default);

    /// <summary>验证本地安全审计哈希链。</summary>
    Task<bool> VerifyAuditAsync(CancellationToken cancellationToken = default);
}

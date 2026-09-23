using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>敏感操作的运行时安全闸门。</summary>
public interface ISecurityOperationGate
{
    /// <summary>当前是否允许删除、清理和粉碎等敏感操作。</summary>
    bool IsSensitiveOperationAllowed { get; }

    /// <summary>根据最新安全评估更新闸门状态。</summary>
    void Apply(SecurityAssessment assessment);

    /// <summary>若敏感操作被禁止则抛出具有原因的业务异常。</summary>
    void EnsureSensitiveOperationAllowed(string operationName);
}

using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Infrastructure.Security;

/// <summary>基于最近安全评估限制不可逆或破坏性操作的线程安全闸门。</summary>
public sealed class SecurityOperationGate : ISecurityOperationGate
{
    private int _allowed = 1;
    private string _reason = string.Empty;

    /// <inheritdoc />
    public bool IsSensitiveOperationAllowed => Volatile.Read(ref _allowed) != 0;

    /// <inheritdoc />
    public void Apply(SecurityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        bool allowed = assessment.ApplicationIntegrityValid && assessment.DatabaseIntegrityValid && assessment.AuditIntegrityValid;
        _reason = allowed ? string.Empty : assessment.Summary;
        Volatile.Write(ref _allowed, allowed ? 1 : 0);
    }

    /// <inheritdoc />
    public void EnsureSensitiveOperationAllowed(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (!IsSensitiveOperationAllowed)
        {
            throw new LeslieCleanerException($"安全检查未通过，已阻止敏感操作“{operationName}”：{_reason}");
        }
    }
}

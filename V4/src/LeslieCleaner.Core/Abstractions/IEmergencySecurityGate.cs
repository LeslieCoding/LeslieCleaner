using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>Observable emergency read-only capabilities of the sensitive-operation gate.</summary>
public interface IEmergencySecurityGate
{
    /// <summary>Current fail-safe mode, reason, and exact recovery conditions.</summary>
    EmergencySecurityState EmergencyState { get; }

    /// <summary>Immediately enters read-only mode independently of the assessment cycle.</summary>
    void EnterEmergencyReadOnly(string reason, IReadOnlyList<string> recoveryConditions);
}

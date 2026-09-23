namespace LeslieCleaner.Core.Models;

/// <summary>Derived risk and recommendation helpers for a security assessment.</summary>
public static class SecurityAssessmentExtensions
{
    /// <summary>Maps assessment evidence to the highest applicable risk level.</summary>
    public static SecurityRiskLevel GetRiskLevel(this SecurityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return !assessment.ApplicationIntegrityValid || !assessment.DatabaseIntegrityValid || !assessment.AuditIntegrityValid
            ? SecurityRiskLevel.Critical
            : assessment.RejectedPlugins > 0 ? SecurityRiskLevel.Warning
            : assessment.Score < 100 ? SecurityRiskLevel.Notice
            : SecurityRiskLevel.Safe;
    }

    /// <summary>Returns actionable recovery guidance derived from failed checks.</summary>
    public static IReadOnlyList<string> GetRecommendations(this SecurityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var recommendations = new List<string>();
        if (!assessment.ApplicationIntegrityValid) recommendations.Add("Repair or reinstall the affected application files, then run a fresh security assessment.");
        if (!assessment.DatabaseIntegrityValid) recommendations.Add("Repair or restore the application database, then run a fresh security assessment.");
        if (!assessment.AuditIntegrityValid) recommendations.Add("Preserve the audit log for review and restore a trusted audit chain, then reassess.");
        if (assessment.RejectedPlugins > 0) recommendations.Add("Review rejected external components before enabling them.");
        if (recommendations.Count == 0) recommendations.Add("No action required.");
        return recommendations;
    }
}

using System.Text.Json;

namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>Loads and validates the versioned, embedded Smart Scan Cache Rule Pack.</summary>
public static class CacheRuleCatalog
{
    private const string ResourceName = "LeslieCleaner.Modules.Cleaner.Scanning.Rules.smart-scan-rules.json";
    private static readonly Lazy<IReadOnlyList<ScanTargetDefinition>> EmbeddedRules = new(LoadEmbeddedRulesCore);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Gets the validated rule set included with the current application build.</summary>
    public static IReadOnlyList<ScanTargetDefinition> LoadEmbeddedRules() => EmbeddedRules.Value;

    /// <summary>Parses a rule pack for validation tests and future signed external packs.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the pack is malformed or unsafe.</exception>
    public static IReadOnlyList<ScanTargetDefinition> Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        List<CacheRuleDefinition>? rules = JsonSerializer.Deserialize<List<CacheRuleDefinition>>(json, JsonOptions);
        if (rules is null || rules.Count == 0)
        {
            throw new InvalidOperationException("Cache Rule Pack is empty.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<ScanTargetDefinition>(rules.Count);
        foreach (CacheRuleDefinition rule in rules)
        {
            Validate(rule, ids, ruleIds);
            targets.Add(new ScanTargetDefinition(
                rule.Id.Trim(),
                rule.DisplayName.Trim(),
                ParseSafety(rule.Safety),
                rule.CandidatePatterns.ToArray(),
                rule.FileFilters.ToArray(),
                rule.RuleId.Trim(),
                rule.Provider.Trim(),
                rule.RuleVersion.Trim()));
        }

        return targets;
    }

    private static IReadOnlyList<ScanTargetDefinition> LoadEmbeddedRulesCore()
    {
        using Stream stream = typeof(CacheRuleCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Cache Rule Pack is missing: {ResourceName}");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static void Validate(CacheRuleDefinition rule, ISet<string> ids, ISet<string> ruleIds)
    {
        if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.DisplayName))
        {
            throw new InvalidOperationException("Each Cache Rule requires an id and displayName.");
        }

        if (!ids.Add(rule.Id.Trim()))
        {
            throw new InvalidOperationException($"Duplicate Cache Rule id: {rule.Id}");
        }

        if (string.IsNullOrWhiteSpace(rule.RuleId) || !rule.RuleId.StartsWith("LCN-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cache Rule '{rule.Id}' has an invalid ruleId.");
        }

        if (!ruleIds.Add(rule.RuleId.Trim()))
        {
            throw new InvalidOperationException($"Duplicate Cache Rule ruleId: {rule.RuleId}");
        }

        _ = ParseSafety(rule.Safety);
        if (string.IsNullOrWhiteSpace(rule.Provider) || string.IsNullOrWhiteSpace(rule.RuleVersion))
        {
            throw new InvalidOperationException($"Cache Rule '{rule.Id}' requires provider and ruleVersion.");
        }

        if (rule.CandidatePatterns is null || rule.FileFilters is null)
        {
            throw new InvalidOperationException($"Cache Rule '{rule.Id}' has null path/filter collections.");
        }

        bool isRegistryRule = rule.Id.Equals(SmartScanTargets.RegistryCategoryId, StringComparison.OrdinalIgnoreCase);
        if (!isRegistryRule && rule.CandidatePatterns.Count == 0)
        {
            throw new InvalidOperationException($"Cache Rule '{rule.Id}' has no candidate path.");
        }

        foreach (string pattern in rule.CandidatePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new InvalidOperationException($"Cache Rule '{rule.Id}' contains an unsafe candidate path.");
            }

            int separatorIndex = pattern.IndexOf(':');
            string relativePath = separatorIndex >= 0 ? pattern[(separatorIndex + 1)..] : string.Empty;
            if (separatorIndex <= 0 ||
                relativePath.Split(['\\', '/']).Any(static segment => segment == ".."))
            {
                throw new InvalidOperationException($"Cache Rule '{rule.Id}' contains an unsafe candidate path.");
            }
        }
    }

    private static ScanSafetyLevel ParseSafety(string? safety) =>
        Enum.TryParse(safety, ignoreCase: true, out ScanSafetyLevel parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid Cache Rule safety level: {safety}");

    private sealed record CacheRuleDefinition(
        string Id,
        string DisplayName,
        string Safety,
        IReadOnlyList<string> CandidatePatterns,
        IReadOnlyList<string> FileFilters,
        string RuleId,
        string Provider,
        string RuleVersion);
}

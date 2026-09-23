using LeslieCleaner.Modules.Cleaner.Scanning;

namespace LeslieCleaner.Tests.Cleaner;

public sealed class CacheRuleCatalogTests
{
    [Fact]
    public void EmbeddedRules_AreVersionedAndUnique()
    {
        IReadOnlyList<ScanTargetDefinition> rules = CacheRuleCatalog.LoadEmbeddedRules();

        Assert.True(rules.Count >= 14);
        Assert.Equal(rules.Count, rules.Select(static rule => rule.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(rules.Count, rules.Select(static rule => rule.RuleId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rules, static rule =>
        {
            Assert.StartsWith("LCN-", rule.RuleId, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(rule.Provider));
            Assert.False(string.IsNullOrWhiteSpace(rule.RuleVersion));
        });
    }

    [Fact]
    public void Parse_RejectsTraversalAndDuplicateRuleIds()
    {
        const string traversal = """
            [{"id":"Unsafe","displayName":"Unsafe","safety":"Default","candidatePatterns":["Temp:..\\Outside"],"fileFilters":[],"ruleId":"LCN-CACHE-UNSAFE-001","provider":"Test","ruleVersion":"1"}]
            """;
        const string duplicate = """
            [
              {"id":"First","displayName":"First","safety":"Default","candidatePatterns":["Temp:"],"fileFilters":[],"ruleId":"LCN-CACHE-DUP-001","provider":"Test","ruleVersion":"1"},
              {"id":"Second","displayName":"Second","safety":"Default","candidatePatterns":["Temp:"],"fileFilters":[],"ruleId":"LCN-CACHE-DUP-001","provider":"Test","ruleVersion":"1"}
            ]
            """;

        Assert.Throws<InvalidOperationException>(() => CacheRuleCatalog.Parse(traversal));
        Assert.Throws<InvalidOperationException>(() => CacheRuleCatalog.Parse(duplicate));
    }
}

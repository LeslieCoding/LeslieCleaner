namespace LeslieCleaner.Modules.Cleaner.Scanning;

/// <summary>
/// Smart Scan target identities. The concrete, versioned rule definitions are
/// loaded from the embedded Cache Rule Pack rather than hard-coded here.
/// </summary>
public static class SmartScanTargets
{
    /// <summary>Registry cleanup category supplied by <c>IRegistryScanner</c>.</summary>
    public const string RegistryCategoryId = "Registry";

    /// <summary>Windows Shell recycle-bin category.</summary>
    public const string RecycleBinCategoryId = "RecycleBin";

    /// <summary>Validated, versioned Smart Scan rules included in this build.</summary>
    public static IReadOnlyList<ScanTargetDefinition> All => CacheRuleCatalog.LoadEmbeddedRules();
}

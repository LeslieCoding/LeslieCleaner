using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Security;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class IntegrityRepairServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly string _applicationRoot;
    private readonly string _backupRoot;
    private readonly IReadOnlyList<TrustedIntegrityTarget> _targets;

    public IntegrityRepairServiceTests()
    {
        _applicationRoot = Directory.CreateDirectory(
            Path.Combine(_workspace.RootDirectory, "InstalledApplication")).FullName;
        _backupRoot = Path.Combine(_workspace.RootDirectory, "TrustedBackup");
        _targets =
        [
            Target("LeslieCleaner.exe", IntegrityArtifactKind.Executable, "trusted-executable"),
            Target("LeslieCleaner.Core.dll", IntegrityArtifactKind.CoreLibrary, "trusted-core"),
            Target("settings.json", IntegrityArtifactKind.Configuration, "trusted-config"),
            Target("leslie-cleaner.db", IntegrityArtifactKind.Database, "trusted-database"),
            Target("plugins/manifest.json", IntegrityArtifactKind.PluginMetadata, "trusted-plugin"),
        ];
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task VerifyWithoutBaseline_FailsClosedAndNeverTrustsCurrentFiles()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();

        StartupIntegrityReport report = await service.VerifyStartupAsync();

        Assert.False(report.ManifestTrusted);
        Assert.Equal(1, report.HighRiskFailureCount);
        Assert.True(gate.EmergencyState.IsReadOnly);
        Assert.False(File.Exists(Path.Combine(_workspace.PathService.DataDirectory,
            "security-integrity-manifest-v4.json")));
    }

    [Fact]
    public async Task EstablishBaseline_RequiresExplicitAuthorizationAndAllFiveCategories()
    {
        (IntegrityRepairService service, _) = CreateService();
        var unauthorized = Request(_targets) with { ExplicitlyAuthorized = false };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EstablishTrustedBaselineAsync(unauthorized));

        TrustedBaselineRequest incomplete = Request(_targets.Take(4).ToArray());
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EstablishTrustedBaselineAsync(incomplete));

        TrustedBaselineResult result = await service.EstablishTrustedBaselineAsync(Request(_targets));
        StartupIntegrityReport report = await service.VerifyStartupAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.ProtectedFileCount);
        Assert.True(report.IsValid);
        Assert.Equal(5, report.Records.Count);
        Assert.All(report.Records, record => Assert.Equal(FileIntegrityStatus.Valid, record.Status));
        Assert.Equal(5, (await service.GetRecordsAsync()).Count);
    }

    [Fact]
    public async Task ConfigurationTamper_IsWarningOnlyAndDoesNotEnterEmergencyMode()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();
        await service.EstablishTrustedBaselineAsync(Request(_targets));
        gate.Apply(HealthyAssessment());
        File.WriteAllText(Path.Combine(_applicationRoot, "settings.json"), "changed-config");

        StartupIntegrityReport report = await service.VerifyStartupAsync();

        Assert.Equal(0, report.HighRiskFailureCount);
        Assert.Equal(1, report.LowRiskFailureCount);
        Assert.False(gate.EmergencyState.IsReadOnly);
        FileIntegrityRecord record = Assert.Single(report.Records,
            item => item.Kind == IntegrityArtifactKind.Configuration);
        Assert.Equal(SecurityRiskLevel.Notice, record.RiskLevel);
    }

    [Fact]
    public async Task CoreTamper_EntersEmergencyAndTrustedRepairClearsOnlyWhenAllConditionsPass()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();
        await service.EstablishTrustedBaselineAsync(Request(_targets));
        gate.Apply(HealthyAssessment());
        string corePath = Path.Combine(_applicationRoot, "LeslieCleaner.Core.dll");
        File.WriteAllText(corePath, "tampered-core");

        StartupIntegrityReport tampered = await service.VerifyStartupAsync();
        Assert.Equal(1, tampered.HighRiskFailureCount);
        Assert.True(gate.EmergencyState.IsReadOnly);

        IntegrityRecoveryPrerequisites incomplete = HealthyPrerequisites() with { AuditIntegrityValid = false };
        IntegrityRepairResult blocked = await service.RepairAsync(incomplete);
        Assert.False(blocked.EmergencyModeCleared);
        Assert.True(gate.EmergencyState.IsReadOnly);
        Assert.True(blocked.Verification.IsValid);

        IntegrityRepairResult recovered = await service.RepairAsync(HealthyPrerequisites());
        Assert.True(recovered.Succeeded);
        Assert.True(recovered.EmergencyModeCleared);
        Assert.False(gate.EmergencyState.IsReadOnly);
        Assert.Equal("trusted-core", File.ReadAllText(corePath));
    }

    [Fact]
    public async Task CorruptTrustedBackup_RejectsRepairAndKeepsEmergencyMode()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();
        await service.EstablishTrustedBaselineAsync(Request(_targets));
        string corePath = Path.Combine(_applicationRoot, "LeslieCleaner.Core.dll");
        File.WriteAllText(corePath, "tampered-core");
        _ = await service.VerifyStartupAsync();
        string backupPath = FindBackupPath(IntegrityArtifactKind.CoreLibrary);
        File.WriteAllText(backupPath, "tampered-backup");

        IntegrityRepairResult result = await service.RepairAsync(HealthyPrerequisites());

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.True(gate.EmergencyState.IsReadOnly);
        Assert.Equal("tampered-core", File.ReadAllText(corePath));
    }

    [Fact]
    public async Task CorruptManifest_IsRejectedAndCannotBecomeANewBaselineImplicitly()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();
        await service.EstablishTrustedBaselineAsync(Request(_targets));
        string manifestPath = Path.Combine(_workspace.PathService.DataDirectory,
            "security-integrity-manifest-v4.json");
        File.WriteAllText(manifestPath, "{not-valid-json");

        StartupIntegrityReport report = await service.VerifyStartupAsync();

        Assert.False(report.ManifestTrusted);
        Assert.True(gate.EmergencyState.IsReadOnly);
        Assert.Equal("{not-valid-json", File.ReadAllText(manifestPath));
    }

    [Fact]
    public async Task PathEscapeAndReparsePoint_AreRejected()
    {
        (IntegrityRepairService service, _) = CreateService();
        string outside = _workspace.CreateFile("outside.dll", "outside");
        TrustedIntegrityTarget[] escaped = [.. _targets.Take(4),
            new TrustedIntegrityTarget(outside, IntegrityArtifactKind.PluginMetadata)];
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.EstablishTrustedBaselineAsync(Request(escaped)));

        string external = _workspace.CreateFile("external/linked.dll", "linked");
        string link = Path.Combine(_applicationRoot, "linked.dll");
        try
        {
            File.CreateSymbolicLink(link, external);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return;
        }
        TrustedIntegrityTarget[] linked = [.. _targets.Take(4),
            new TrustedIntegrityTarget(link, IntegrityArtifactKind.PluginMetadata)];
        await Assert.ThrowsAsync<IOException>(() =>
            service.EstablishTrustedBaselineAsync(Request(linked)));
    }

    [Fact]
    public async Task LockedCoreFile_ProducesCriticalHashFailureAndCancellationIsObserved()
    {
        (IntegrityRepairService service, SecurityOperationGateV4 gate) = CreateService();
        await service.EstablishTrustedBaselineAsync(Request(_targets));
        string corePath = Path.Combine(_applicationRoot, "LeslieCleaner.Core.dll");
        await using (FileStream locked = new(corePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            StartupIntegrityReport report = await service.VerifyStartupAsync();
            FileIntegrityRecord record = Assert.Single(report.Records,
                item => item.Kind == IntegrityArtifactKind.CoreLibrary);
            Assert.Equal(FileIntegrityStatus.VerificationFailed, record.Status);
            Assert.True(gate.EmergencyState.IsReadOnly);
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.VerifyStartupAsync(cancellation.Token));
    }

    [Fact]
    public async Task NormalInstallLayout_AllowsConfigDataAndPluginMetadataProtectedRoots()
    {
        (IntegrityRepairService service, _) = CreateService();
        string configuration = _workspace.CreateFile("LocalAppData/LeslieCleaner/Config/settings.json", "config");
        string database = _workspace.CreateFile("ProgramData/LeslieCleaner/Data/leslie-cleaner.db", "database");
        string pluginMetadata = _workspace.CreateFile("ProgramData/LeslieCleaner/Plugins/manifest.json", "plugin");
        TrustedIntegrityTarget[] productionTargets =
        [
            _targets[0], _targets[1],
            new(configuration, IntegrityArtifactKind.Configuration),
            new(database, IntegrityArtifactKind.Database),
            new(pluginMetadata, IntegrityArtifactKind.PluginMetadata),
        ];

        Assert.True((await service.EstablishTrustedBaselineAsync(Request(productionTargets))).Succeeded);
        Assert.True((await service.VerifyStartupAsync()).IsValid);
    }

    [Fact]
    public void HealthScore_UsesExactArchitectureWeights()
    {
        (IntegrityRepairService service, _) = CreateService();

        SecurityHealthScore healthy = service.CalculateHealthScore(new(true, true, true, true, true));
        SecurityHealthScore partial = service.CalculateHealthScore(new(true, false, true, false, true));

        Assert.Equal((100, 30, 20, 20, 15, 15),
            (healthy.Score, healthy.IntegrityPoints, healthy.PermissionPoints, healthy.UpdatePoints,
                healthy.PluginPoints, healthy.EncryptionPoints));
        Assert.Equal(65, partial.Score);
        Assert.Equal(SecurityRiskLevel.Warning, partial.RiskLevel);
    }

    [Fact]
    public async Task DetachedSignature_IsVerifiedAgainstExternallyTrustedKey()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string executablePath = _targets[0].FilePath;
        byte[] hash = SHA256.HashData(await File.ReadAllBytesAsync(executablePath));
        string signature = Convert.ToBase64String(signer.SignHash(hash));
        TrustedIntegrityTarget[] signedTargets =
            [new(executablePath, IntegrityArtifactKind.Executable, signature), .. _targets.Skip(1)];
        (IntegrityRepairService service, _) = CreateService(
            new IntegrityRepairOptions(signer.ExportSubjectPublicKeyInfoPem()));

        Assert.True((await service.EstablishTrustedBaselineAsync(Request(signedTargets))).Succeeded);
        Assert.True((await service.VerifyStartupAsync()).IsValid);
    }

    private TrustedIntegrityTarget Target(string relativePath, IntegrityArtifactKind kind, string content)
    {
        string path = Path.Combine(_applicationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return new TrustedIntegrityTarget(path, kind);
    }

    private TrustedBaselineRequest Request(IReadOnlyList<TrustedIntegrityTarget> targets) =>
        new(_applicationRoot, _backupRoot, targets, true, "Verified installer payload");

    private (IntegrityRepairService Service, SecurityOperationGateV4 Gate) CreateService(
        IntegrityRepairOptions? options = null)
    {
        var gate = new SecurityOperationGateV4();
        var encryption = new AesGcmEncryptionService(_workspace.PathService, _workspace.Log);
        return (new IntegrityRepairService(_workspace.PathService, _workspace.Log,
            new SecurityAuditStore(_workspace.PathService), gate, gate, encryption, options), gate);
    }

    private string FindBackupPath(IntegrityArtifactKind kind)
    {
        string manifestPath = Path.Combine(_workspace.PathService.DataDirectory,
            "security-integrity-manifest-v4.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement entries = document.RootElement.GetProperty("Manifest").GetProperty("Entries");
        JsonElement entry = entries.EnumerateArray().Single(item =>
            item.GetProperty("Kind").GetInt32() == (int)kind);
        return Path.GetFullPath(Path.Combine(_backupRoot,
            entry.GetProperty("BackupRelativePath").GetString()!));
    }

    private static SecurityAssessment HealthyAssessment() =>
        new(true, true, true, 0, 0, 100, "healthy", DateTimeOffset.UtcNow);

    private static IntegrityRecoveryPrerequisites HealthyPrerequisites() =>
        new(true, true, true, true, true, true);
}
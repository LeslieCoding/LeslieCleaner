using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Workflow;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Uninstaller;

public sealed class UninstallWorkflowTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task FingerprintStore_NormalizesDeduplicatesAndPersistsAtomically()
    {
        string install = Path.Combine(_workspace.RootDirectory, "app");
        Directory.CreateDirectory(install);
        var store = new ApplicationFingerprintStore(Path.Combine(_workspace.RootDirectory, "fingerprints"));
        ApplicationFingerprint fingerprint = store.Create(CreateApp(install, "uninstall.exe"),
        [
            new FingerprintEntry(FingerprintResourceType.Folder, install, 90, "scanner"),
            new FingerprintEntry(FingerprintResourceType.Task, "Vendor Update", 80, "task action match"),
        ]);

        string path = await store.SaveAsync(fingerprint);
        ApplicationFingerprint? restored = await store.GetAsync(fingerprint.Id);

        Assert.True(File.Exists(path));
        Assert.NotNull(restored);
        Assert.Equal(2, restored.Entries.Count);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public async Task SafetyAnalyzer_BlocksWithoutExplicitConfirmation()
    {
        var request = new UninstallWorkflowRequest(CreateApp(null, "uninstall.exe"), UserConfirmed: false);
        UninstallSafetyAssessment result = await new UninstallSafetyAnalyzer().AnalyzeAsync(request);
        Assert.False(result.CanProceed);
        Assert.Contains(result.Findings, static finding => finding.Code == "CONFIRMATION_REQUIRED");
    }

    [Fact]
    public async Task Execute_OfficialSuccess_ScansAndCreatesVerifiedBackup()
    {
        string config = _workspace.CreateFile("app/settings.json", "safe-config");
        var residual = new FakeResidualRunner([
            new ResidualItem(config + ".old", ResidualKind.File, 42, ResidualSourceStage.KnownRules, 0.8),
        ]);
        var backup = new FakeBackupService();
        UninstallWorkflow workflow = CreateWorkflow(
            new FakeUninstallRunner(new UninstallTaskResult("app", "App", true, 0, "official", false, null)),
            residual, backup);
        var request = new UninstallWorkflowRequest(CreateApp(null, "uninstall.exe"), true, false,
        [new FingerprintEntry(FingerprintResourceType.Configuration, config, 95, "known configuration")]);

        UninstallWorkflowReport report = await workflow.ExecuteAsync(request, TimeSpan.FromSeconds(5));

        Assert.True(report.Success);
        Assert.Equal(42, report.ResidualBytes);
        Assert.Single(report.BackupIds);
        Assert.Equal(1, backup.VerifyCalls);
        Assert.Equal(0, residual.CleanCalls);
    }

    [Fact]
    public async Task Execute_ForcedCleanup_CleansOnlyConfirmedConfidence()
    {
        var residual = new FakeResidualRunner([
            new ResidualItem("C:\\Vendor\\confirmed.tmp", ResidualKind.File, 10, ResidualSourceStage.KnownRules, 0.71),
            new ResidualItem("C:\\Vendor\\uncertain.tmp", ResidualKind.File, 20, ResidualSourceStage.DeepScan, 0.70),
        ]);
        UninstallWorkflow workflow = CreateWorkflow(
            new FakeUninstallRunner(new UninstallTaskResult("app", "App", false, null, "none", false, "unused")),
            residual, new FakeBackupService());

        UninstallWorkflowReport report = await workflow.ExecuteAsync(
            new UninstallWorkflowRequest(CreateApp(null, null), true, AllowForcedCleanup: true), TimeSpan.FromSeconds(5));

        Assert.True(report.Success);
        Assert.True(report.ForcedCleanup);
        Assert.Single(residual.LastCleaned);
        Assert.Single(report.RemainingItems);
        Assert.Equal("C:\\Vendor\\uncertain.tmp", report.RemainingItems[0].Path);
    }

    [Fact]
    public void FingerprintStore_RejectsRelativeFileEvidence()
    {
        var store = new ApplicationFingerprintStore(Path.Combine(_workspace.RootDirectory, "fingerprints"));
        Assert.Throws<ArgumentException>(() => store.Create(CreateApp(null, "uninstall.exe"),
        [new FingerprintEntry(FingerprintResourceType.Configuration, "relative.json", 90, "untrusted")]));
    }

    public void Dispose() => _workspace.Dispose();

    private UninstallWorkflow CreateWorkflow(IUninstallRunner runner, IResidualCleanupRunner residual, IBackupService backup) =>
        new(new UninstallSafetyAnalyzer(),
            new ApplicationFingerprintStore(Path.Combine(_workspace.RootDirectory, "fingerprints")),
            runner, residual, backup, _workspace.Log);

    private static InstalledApp CreateApp(string? install, string? uninstall) =>
        new("app", "App", "1.0", "Vendor", 100, null, install, uninstall,
            AppSourceType.Win32, null, null);

    private sealed class FakeUninstallRunner(UninstallTaskResult result) : IUninstallRunner
    {
        public Task<UninstallTaskResult> UninstallOneAsync(
            InstalledApp application, TimeSpan timeout, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeResidualRunner(IReadOnlyList<ResidualItem> items) : IResidualCleanupRunner
    {
        public int CleanCalls { get; private set; }
        public IReadOnlyList<ResidualItem> LastCleaned { get; private set; } = [];

        public Task<IReadOnlyList<ResidualItem>> ScanAsync(ResidualQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(items);

        public Task<CleanResult> CleanAsync(IReadOnlyList<ResidualItem> cleaned, CancellationToken cancellationToken = default)
        {
            CleanCalls++;
            LastCleaned = cleaned;
            return Task.FromResult(new CleanResult
            {
                OperationId = "rollback-id",
                Items = [.. cleaned.Select(static item => new CleanedItem(new ScannedItem(item.Path, item.SizeBytes, "Residual", DateTimeOffset.UtcNow), null, null, true, null))],
                FreedBytes = cleaned.Sum(static item => item.SizeBytes),
            });
        }
    }

    private sealed class FakeBackupService : IBackupService
    {
        private readonly Dictionary<Guid, BackupRecord> _records = [];
        public int VerifyCalls { get; private set; }
        public Task<BackupRecord> BackupRegistryKeyAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Add(BackupType.Registry, path));
        public Task<BackupRecord> BackupRegistryValueAsync(string path, string valueName, CancellationToken cancellationToken = default) => Task.FromResult(Add(BackupType.Registry, path));
        public Task<BackupRecord> BackupFileAsync(string path, BackupType type = BackupType.File, string? description = null, BackupStorageMode storageMode = BackupStorageMode.FullCopy, CancellationToken cancellationToken = default) => Task.FromResult(Add(type, path));
        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreAsync(Guid id, RestoreConflictPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BackupRecord?> GetBackupAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_records.GetValueOrDefault(id));
        public Task<BackupVerificationResult> VerifyAsync(Guid id, CancellationToken cancellationToken = default) { VerifyCalls++; return Task.FromResult(new BackupVerificationResult(id, true, null, 1, "AA")); }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { _records.Remove(id); return Task.CompletedTask; }
        public Task SetLockedAsync(Guid id, bool isLocked, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BackupRetentionResult> ApplyRetentionAsync(BackupRetentionPolicy policy, CancellationToken cancellationToken = default) => Task.FromResult(new BackupRetentionResult(0, 0, []));
        public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupRecord>>([.. _records.Values]);

        private BackupRecord Add(BackupType type, string path)
        {
            Guid id = Guid.NewGuid();
            var record = new BackupRecord(id, type == BackupType.Registry ? path : string.Empty,
                path, path + ".meta", DateTimeOffset.UtcNow, 1) { Type = type, Sha256 = "AA" };
            _records.Add(id, record);
            return record;
        }
    }
}

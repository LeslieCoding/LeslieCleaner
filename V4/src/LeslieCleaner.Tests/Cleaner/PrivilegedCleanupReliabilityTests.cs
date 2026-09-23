using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Cleaner;

/// <summary>Verifies honest cleanup outcomes, elevation guidance, locked-file handling, and post-operation reads.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PrivilegedCleanupReliabilityTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly CleanEngine _engine;

    public PrivilegedCleanupReliabilityTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        var backups = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        _engine = new CleanEngine(_quarantine, backups, registryEngine: null,
            _workspace.PathService, _workspace.Log);
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Clean_VerifiedFile_UsesAllocatedBytesAndCanRollback()
    {
        string path = _workspace.CreateFile("accuracy/small.tmp", "2K");
        var item = new ScannedItem(path, SizeBytes: 2, "TempFiles", DateTimeOffset.UtcNow,
            AllocatedSizeBytes: 8192);

        CleanResult result = await _engine.CleanAsync(new CleanRequest
        {
            Items = [item],
            Reason = "allocated-size verification",
        });

        CleanedItem cleaned = Assert.Single(result.Items);
        Assert.True(cleaned.Success);
        Assert.True(cleaned.WasVerified);
        Assert.Equal(OperationFailureKind.None, cleaned.FailureKind);
        Assert.Equal(8192, result.FreedBytes);
        Assert.False(File.Exists(path));
        Assert.True(await _engine.RollbackAsync(result.OperationId));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Clean_MissingFile_IsNotReportedAsSuccess()
    {
        string path = Path.Combine(_workspace.RootDirectory, "missing.tmp");

        CleanResult result = await _engine.CleanAsync(new CleanRequest
        {
            Items = [new ScannedItem(path, 100, "TempFiles", DateTimeOffset.UtcNow)],
            Reason = "missing target verification",
        });

        CleanedItem failed = Assert.Single(result.Items);
        Assert.False(failed.Success);
        Assert.False(failed.WasVerified);
        Assert.Equal(OperationFailureKind.NotFound, failed.FailureKind);
        Assert.Equal(0, result.FreedBytes);
    }

    [Fact]
    public async Task Clean_LockedFile_ReturnsInUseAndPreservesSource()
    {
        string path = _workspace.CreateFile("locked/in-use.tmp", "locked payload");
        await using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        CleanResult result = await _engine.CleanAsync(new CleanRequest
        {
            Items = [new ScannedItem(path, new FileInfo(path).Length, "TempFiles", DateTimeOffset.UtcNow)],
            Reason = "locked target verification",
        });

        CleanedItem failed = Assert.Single(result.Items);
        Assert.False(failed.Success);
        Assert.Equal(OperationFailureKind.InUse, failed.FailureKind);
        Assert.False(failed.RequiresElevation);
        Assert.True(File.Exists(path));
        Assert.Empty(await _quarantine.ListEntriesAsync());
    }

    [Fact]
    public void Classifier_DistinguishesSharingViolationAndElevation()
    {
        var sharingViolation = new IOException("simulated lock", unchecked((int)0x80070020));
        Assert.Equal(OperationFailureKind.InUse, OperationFailureClassifier.Classify(sharingViolation));
        Assert.Equal(OperationFailureKind.RequiresElevation,
            OperationFailureClassifier.Classify(new UnauthorizedAccessException("simulated ACL denial")));

        var wrapped = new BackupException("registry denied", new UnauthorizedAccessException("ACL"));
        Assert.Equal(OperationFailureKind.RequiresElevation, wrapped.FailureKind);
        Assert.Equal(OperationFailureKind.RequiresElevation, OperationFailureClassifier.Classify(wrapped));
    }
    [Fact]
    public async Task Clean_ProtectedApplicationFile_IsBlockedBeforeQuarantine()
    {
        string applicationRoot = Path.Combine(_workspace.RootDirectory, "ApplicationCore");
        string path = _workspace.CreateFile("ApplicationCore/core.dll", "trusted core");
        var policy = new WindowsCleanupTargetPolicy(_workspace.PathService, applicationRoot);
        var backups = new RegistryBackupService(_workspace.PathService, _workspace.Log);
        var guarded = new CleanEngine(_quarantine, backups, registryEngine: null,
            _workspace.PathService, _workspace.Log, cleanupTargetPolicy: policy);

        CleanResult result = await guarded.CleanAsync(new CleanRequest
        {
            Items = [new ScannedItem(path, new FileInfo(path).Length, "TempFiles", DateTimeOffset.UtcNow)],
            Reason = "protected-boundary test",
        });

        CleanedItem blocked = Assert.Single(result.Items);
        Assert.False(blocked.Success);
        Assert.True(blocked.WasVerified);
        Assert.Equal(OperationFailureKind.ProtectedResource, blocked.FailureKind);
        Assert.True(File.Exists(path));
        Assert.Empty(await _quarantine.ListEntriesAsync());
    }

    [Fact]
    public void TargetPolicy_AllowsOrdinaryFileAndBlocksVolumeRoot()
    {
        string applicationRoot = Path.Combine(_workspace.RootDirectory, "ApplicationCore");
        Directory.CreateDirectory(applicationRoot);
        string safe = _workspace.CreateFile("ordinary/cache.tmp", "cache");
        var policy = new WindowsCleanupTargetPolicy(_workspace.PathService, applicationRoot);

        Assert.True(policy.EvaluateFile(safe).Allowed);
        var root = policy.EvaluateFile(Path.GetPathRoot(safe)!);
        Assert.False(root.Allowed);
        Assert.Equal(OperationFailureKind.ProtectedResource, root.FailureKind);
    }
}

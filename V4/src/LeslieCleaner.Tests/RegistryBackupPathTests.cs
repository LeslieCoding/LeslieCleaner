using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests;

public sealed class RegistryBackupPathTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly RegistryBackupService _service;

    public RegistryBackupPathTests() =>
        _service = new RegistryBackupService(_workspace.PathService, _workspace.Log);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void GetBackupFilePath_SanitizesBackslashes()
    {
        var id = Guid.NewGuid();

        string path = _service.GetBackupFilePath(@"HKLM\Software\Adobe", id);

        Assert.EndsWith("HKLM.Software.Adobe.reg", path);
        Assert.Contains(Path.Combine("Backups", "Registry", id.ToString("N")), path);
    }

    [Fact]
    public void GetBackupFilePath_ReplacesInvalidFileNameChars()
    {
        var id = Guid.NewGuid();
        string fileName = Path.GetFileName(_service.GetBackupFilePath("HKCU\\Software\\Bad:Name*Here?", id));

        Assert.DoesNotContain(fileName, Path.GetInvalidFileNameChars());
    }

    [Fact]
    public void GetMetadataFilePath_SharesDirectoryWithRegFile()
    {
        var id = Guid.NewGuid();

        string metadata = _service.GetMetadataFilePath(id);
        string regFile = _service.GetBackupFilePath(@"HKCU\Software\X", id);

        Assert.Equal(Path.GetDirectoryName(regFile), Path.GetDirectoryName(metadata));
        Assert.EndsWith("metadata.json", metadata);
    }

    [Fact]
    public async Task ListBackups_EmptyWhenNoBackups()
    {
        IReadOnlyList<BackupRecord> backups = await _service.ListBackupsAsync();

        Assert.Empty(backups);
    }

    [Fact]
    public async Task Restore_MissingBackup_Throws()
    {
        await Assert.ThrowsAsync<LeslieCleaner.Core.Diagnostics.BackupException>(
            () => _service.RestoreAsync(Guid.NewGuid()));
    }
}

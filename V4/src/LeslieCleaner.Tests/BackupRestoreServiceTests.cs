using System.Text.Json;
using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Backup;

public sealed class BackupRestoreServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly RegistryBackupService _service;

    public BackupRestoreServiceTests() => _service = new(_workspace.PathService, _workspace.Log);

    [Fact]
    public async Task FileBackup_IsTypedIndexedAndVerified()
    {
        string source = _workspace.CreateFile("source/settings.json", "{\"theme\":\"dark\"}");
        BackupRecord record = await _service.BackupFileAsync(source, BackupType.Configuration, "settings snapshot");

        Assert.Equal(BackupType.Configuration, record.Type);
        Assert.Equal(Path.GetFullPath(source), record.OriginalPath);
        Assert.True(File.Exists(record.FilePath));
        Assert.True(File.Exists(record.MetadataPath));
        Assert.True(File.Exists(_service.GetIndexFilePath()));
        Assert.Equal(record.Id, (await _service.GetBackupAsync(record.Id))!.Id);
        Assert.True((await _service.VerifyAsync(record.Id)).IsValid);
        Assert.Contains(await _service.ListBackupsAsync(), item => item.Id == record.Id);
    }

    [Fact]
    public async Task Restore_RejectsCorruptedPayload_AndAuditsFailure()
    {
        string source = _workspace.CreateFile("source/data.txt", "original");
        BackupRecord record = await _service.BackupFileAsync(source);
        File.WriteAllText(record.FilePath, "tampered");
        File.Delete(source);

        BackupException error = await Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(record.Id));

        Assert.Contains("integrity", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(source));
        Assert.Contains(_workspace.Log.Events, entry =>
            entry.Kind == LogEventKind.Exception && entry.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OverwriteRestore_PreservesCurrentStateBeforeReplacing()
    {
        string source = _workspace.CreateFile("source/profile.json", "version-one");
        BackupRecord original = await _service.BackupFileAsync(source, BackupType.Configuration);
        File.WriteAllText(source, "current-state");

        await _service.RestoreAsync(original.Id, RestoreConflictPolicy.Overwrite);

        Assert.Equal("version-one", File.ReadAllText(source));
        IReadOnlyList<BackupRecord> records = await _service.ListBackupsAsync();
        BackupRecord snapshot = Assert.Single(records, item => item.Id != original.Id);
        Assert.Equal("current-state", File.ReadAllText(snapshot.FilePath));
        Assert.Contains("Pre-restore", snapshot.Description);
    }

    [Fact]
    public async Task MetadataAndIndex_WritesLeaveNoTemporaryFiles()
    {
        string source = _workspace.CreateFile("source/data.db", "sqlite-like-data");
        await _service.BackupFileAsync(source, BackupType.Database);

        Assert.Empty(Directory.EnumerateFiles(_workspace.PathService.BackupDirectory, "*.tmp", SearchOption.AllDirectories));
        using JsonDocument metadata = JsonDocument.Parse(await File.ReadAllTextAsync(
            Directory.EnumerateFiles(_workspace.PathService.BackupDirectory, "metadata.json", SearchOption.AllDirectories).Single()));
        using JsonDocument index = JsonDocument.Parse(await File.ReadAllTextAsync(_service.GetIndexFilePath()));
        Assert.Equal(JsonValueKind.Object, metadata.RootElement.ValueKind);
        Assert.Equal(JsonValueKind.Array, index.RootElement.ValueKind);
    }

    [Fact]
    public async Task Retention_AppliesCountAndPreservesLockedBackup()
    {
        string a = _workspace.CreateFile("source/a.txt", "a");
        BackupRecord locked = await _service.BackupFileAsync(a);
        await _service.SetLockedAsync(locked.Id, true);
        await Task.Delay(10);
        await _service.BackupFileAsync(_workspace.CreateFile("source/b.txt", "bb"));
        await Task.Delay(10);
        BackupRecord newest = await _service.BackupFileAsync(_workspace.CreateFile("source/c.txt", "ccc"));

        BackupRetentionResult result = await _service.ApplyRetentionAsync(new BackupRetentionPolicy
            { MaxAgeDays = null, MaxCount = 1 });

        Assert.Equal(1, result.DeletedCount);
        IReadOnlyList<BackupRecord> remaining = await _service.ListBackupsAsync();
        Assert.Contains(remaining, item => item.Id == locked.Id && item.IsLocked);
        Assert.Contains(remaining, item => item.Id == newest.Id);
    }

    [Fact]
    public async Task Delete_RejectsLockedBackup_ThenRemovesUnlockedRecordAndIndexEntry()
    {
        BackupRecord record = await _service.BackupFileAsync(_workspace.CreateFile("source/file.txt"));
        await _service.SetLockedAsync(record.Id, true);
        await Assert.ThrowsAsync<BackupException>(() => _service.DeleteAsync(record.Id));

        await _service.SetLockedAsync(record.Id, false);
        await _service.DeleteAsync(record.Id);

        Assert.Null(await _service.GetBackupAsync(record.Id));
        Assert.DoesNotContain(await _service.ListBackupsAsync(), item => item.Id == record.Id);
    }

    [Fact]
    public async Task MetadataOnlyBackup_IsVerifiableButCannotRestoreContent()
    {
        string source = _workspace.CreateFile("source/large.cache", new string('x', 512));
        BackupRecord record = await _service.BackupFileAsync(source, BackupType.File,
            storageMode: BackupStorageMode.MetadataOnly);
        Assert.True((await _service.VerifyAsync(record.Id)).IsValid);
        File.Delete(source);
        await Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(record.Id));
    }

    public void Dispose() => _workspace.Dispose();
}


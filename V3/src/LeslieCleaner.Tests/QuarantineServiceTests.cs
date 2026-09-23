using LeslieCleaner.Core.Diagnostics;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests;

public sealed class QuarantineServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _service;

    public QuarantineServiceTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _service = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
    }

    public void Dispose() => _config.Dispose();

    [Fact]
    public async Task QuarantineFile_MovesFileAndWritesMetadata()
    {
        string source = _workspace.CreateFile("junk/temp1.tmp");

        QuarantineEntry entry = await _service.QuarantineFileAsync(source, "单元测试清理", "op-1");

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(entry.QuarantinedPath));
        Assert.True(File.Exists(entry.MetadataPath));
        Assert.Equal(Path.GetFullPath(source), entry.OriginalPath);
        Assert.Equal("单元测试清理", entry.Reason);
        Assert.Equal("op-1", entry.OperationId);
        Assert.Equal(new FileInfo(entry.QuarantinedPath).Length, entry.SizeBytes);
        Assert.True(entry.ExpiresUtc > entry.QuarantinedUtc);
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.DeleteFinish));
    }

    [Fact]
    public async Task Restore_ReturnsFileToOriginalPath()
    {
        string source = _workspace.CreateFile("junk/restore-me.tmp", "payload-content");
        QuarantineEntry entry = await _service.QuarantineFileAsync(source, "测试");

        string restoredPath = await _service.RestoreAsync(entry.Id);

        Assert.Equal(Path.GetFullPath(source), restoredPath);
        Assert.True(File.Exists(source));
        Assert.Equal("payload-content", await File.ReadAllTextAsync(source));
        Assert.Empty(await _service.ListEntriesAsync());
        Assert.True(_workspace.Log.HasEvent(LogEventKind.RestoreStart));
        Assert.True(_workspace.Log.HasEvent(LogEventKind.RestoreFinish));
    }

    [Fact]
    public async Task Restore_RenamePolicy_AvoidsConflict()
    {
        string source = _workspace.CreateFile("junk/conflict.tmp", "original");
        QuarantineEntry entry = await _service.QuarantineFileAsync(source, "测试");
        _workspace.CreateFile("junk/conflict.tmp", "newer-file-occupying-path");

        string restoredPath = await _service.RestoreAsync(entry.Id, RestoreConflictPolicy.Rename);

        Assert.NotEqual(source, restoredPath);
        Assert.True(File.Exists(restoredPath));
        Assert.Equal("original", await File.ReadAllTextAsync(restoredPath));
        Assert.Equal("newer-file-occupying-path", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task Restore_FailPolicy_ThrowsOnConflict()
    {
        string source = _workspace.CreateFile("junk/fail.tmp");
        QuarantineEntry entry = await _service.QuarantineFileAsync(source, "测试");
        _workspace.CreateFile("junk/fail.tmp");

        await Assert.ThrowsAsync<QuarantineException>(() => _service.RestoreAsync(entry.Id, RestoreConflictPolicy.Fail));
    }

    [Fact]
    public async Task QuarantineFile_MissingFile_Throws()
    {
        string missing = Path.Combine(_workspace.RootDirectory, "not-exists.tmp");

        await Assert.ThrowsAsync<QuarantineException>(() => _service.QuarantineFileAsync(missing, "测试"));
    }

    [Fact]
    public async Task QuarantineFiles_ContinuesOnSingleFailure()
    {
        string good1 = _workspace.CreateFile("batch/a.tmp");
        string good2 = _workspace.CreateFile("batch/b.tmp");
        string missing = Path.Combine(_workspace.RootDirectory, "batch/missing.tmp");
        var progress = new List<CleanProgress>();

        IReadOnlyList<QuarantineEntry> entries = await _service.QuarantineFilesAsync(
            [good1, missing, good2], "批量测试",
            progress: new Progress<CleanProgress>(p => progress.Add(p)));

        Assert.Equal(2, entries.Count);
        Assert.False(File.Exists(good1));
        Assert.False(File.Exists(good2));
        Assert.NotEmpty(_workspace.Log.Exceptions);
    }

    [Fact]
    public async Task ListEntries_ReturnsAllEntriesNewestFirst()
    {
        string f1 = _workspace.CreateFile("list/1.tmp");
        string f2 = _workspace.CreateFile("list/2.tmp");
        await _service.QuarantineFileAsync(f1, "测试");
        await _service.QuarantineFileAsync(f2, "测试");

        IReadOnlyList<QuarantineEntry> entries = await _service.ListEntriesAsync();

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].QuarantinedUtc >= entries[1].QuarantinedUtc);
    }

    [Fact]
    public async Task PurgeExpired_RemovesEntriesPastRetention()
    {
        // 保留期设为 0 天：隔离即过期
        await _config.SetValueAsync(QuarantineService.RetentionDaysConfigKey, 0);
        string keep = _workspace.CreateFile("purge/keep.tmp");
        await _service.QuarantineFileAsync(keep, "测试");

        int purged = await _service.PurgeExpiredAsync();

        Assert.Equal(1, purged);
        Assert.Empty(await _service.ListEntriesAsync());
    }

    [Fact]
    public async Task PurgeExpired_KeepsFreshEntries()
    {
        string fresh = _workspace.CreateFile("purge/fresh.tmp");
        await _service.QuarantineFileAsync(fresh, "测试");

        int purged = await _service.PurgeExpiredAsync();

        Assert.Equal(0, purged);
        Assert.Single(await _service.ListEntriesAsync());
    }
}

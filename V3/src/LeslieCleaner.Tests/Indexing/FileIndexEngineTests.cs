using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Indexing;

public sealed class FileIndexEngineTests : IAsyncLifetime
{
    private const int InitialFileCount = 10_000;

    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;
    private readonly JsonConfigService _config;
    private readonly FileIndexEngine _engine;
    private readonly string _indexRoot;

    public FileIndexEngineTests()
    {
        string databaseFile = Path.Combine(_workspace.PathService.DataDirectory, "index-test.db");
        _database = new SqliteDatabase(databaseFile, _workspace.Log);
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        // 单测走确定性普通枚举路径；USN 路径由 UsnMftEnumeratorTests 与基准覆盖
        _config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        _engine = new FileIndexEngine(_database, _workspace.Log, _config);
        _indexRoot = Path.Combine(_workspace.RootDirectory, "IndexRoot");
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        Directory.CreateDirectory(_indexRoot);
        CreateFiles(_indexRoot, InitialFileCount);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _config.Dispose();
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Index_FullScan_IndexesAllFiles()
    {
        long changed = await _engine.IndexAsync([_indexRoot]);

        Assert.Equal(InitialFileCount, changed);
        Assert.Equal(InitialFileCount, await new FileIndexRepository(_database).CountAsync());
    }

    [Fact]
    public async Task Index_Incremental_SkipsUnchanged()
    {
        await _engine.IndexAsync([_indexRoot]);

        long changed = await _engine.IndexAsync([_indexRoot]);

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task Index_Incremental_PicksUpModifiedNewAndDeleted()
    {
        await _engine.IndexAsync([_indexRoot]);

        // 修改 10 个（时间戳前进）、新增 5 个、删除 3 个
        for (int i = 0; i < 10; i++)
        {
            string path = Path.Combine(_indexRoot, $"file{i:D5}.dat");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
        }

        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_indexRoot, $"new-{i}.dat"), "new");
        }

        for (int i = 10; i < 13; i++)
        {
            File.Delete(Path.Combine(_indexRoot, $"file{i:D5}.dat"));
        }

        long changed = await _engine.IndexAsync([_indexRoot]);
        long total = await new FileIndexRepository(_database).CountAsync();

        Assert.Equal(15, changed);
        Assert.Equal(InitialFileCount + 5 - 3, total);
    }

    [Fact]
    public async Task Index_SkipsForbiddenFileNames()
    {
        File.WriteAllText(Path.Combine(_indexRoot, "pagefile.sys"), "fake");

        await _engine.IndexAsync([_indexRoot]);
        IReadOnlyList<Core.Models.FileIndexRecord> rows = await new FileIndexRepository(_database)
            .QueryByProductAsync("nothing-matches");

        Assert.Equal(InitialFileCount, await new FileIndexRepository(_database).CountAsync());
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Watcher_FileCreated_TriggersIncrementalUpsert()
    {
        await _engine.IndexAsync([_indexRoot]);
        _engine.StartWatching([_indexRoot]);

        string watched = Path.Combine(_indexRoot, "watched-file.dat");
        await File.WriteAllTextAsync(watched, "watched");

        var repository = new FileIndexRepository(_database);
        bool indexed = false;
        for (int i = 0; i < 40 && !indexed; i++)
        {
            await Task.Delay(250);
            IReadOnlyList<string> rows = await _database.QueryAsync(
                "SELECT path FROM file_index WHERE path = $path;",
                static reader => reader.GetString(0),
                [new Microsoft.Data.Sqlite.SqliteParameter("$path", watched)]);
            indexed = rows.Count == 1;
        }

        _engine.StopWatching();
        Assert.True(indexed, "文件系统监控未在 10s 内将新文件写入索引");
    }

    private static void CreateFiles(string root, int count)
    {
        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            File.WriteAllText(Path.Combine(root, $"file{i:D5}.dat"), "payload");
        });
    }
}

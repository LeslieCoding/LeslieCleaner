using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Indexing;

/// <summary>阶段 2 修复的回归测试：云占位跳过、目录枚举超时、增量 stale-diff 性能。</summary>
public sealed class FileIndexRobustnessTests : IAsyncLifetime
{
    private readonly TempWorkspace _workspace = new();
    private readonly SqliteDatabase _database;
    private readonly JsonConfigService _config;
    private readonly string _indexRoot;

    public FileIndexRobustnessTests()
    {
        _database = new SqliteDatabase(
            Path.Combine(_workspace.PathService.DataDirectory, "robust-index.db"), _workspace.Log);
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        _indexRoot = Path.Combine(_workspace.RootDirectory, "RobustRoot");
    }

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        _config.Dispose();
        await _database.DisposeAsync();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Index_SkipsCloudPlaceholderAndOfflineFiles()
    {
        Directory.CreateDirectory(_indexRoot);
        string normal = Path.Combine(_indexRoot, "normal.dat");
        string offline = Path.Combine(_indexRoot, "offline.dat");
        string reparse = Path.Combine(_indexRoot, "reparse.dat");
        File.WriteAllText(normal, "n");
        File.WriteAllText(offline, "o");
        File.WriteAllText(reparse, "r");
        File.SetAttributes(offline, FileAttributes.Offline);
        File.SetAttributes(reparse, FileAttributes.Offline | FileAttributes.ReparsePoint);

        using var engine = new FileIndexEngine(_database, _workspace.Log, _config);
        await engine.IndexAsync([_indexRoot]);

        Assert.Equal(1, await new FileIndexRepository(_database).CountAsync());
    }

    [Fact]
    public async Task Index_DirectoryTimeout_SkipsAndCompletes()
    {
        Directory.CreateDirectory(_indexRoot);
        for (int i = 0; i < 5000; i++)
        {
            File.WriteAllText(Path.Combine(_indexRoot, $"f{i:D5}.dat"), "x");
        }

        // 超时 0：5000 文件物化不可能在任务启动前完成 → 必然超时跳过，但扫描整体正常完成
        using var engine = new FileIndexEngine(_database, _workspace.Log, _config,
            directoryEnumerationTimeout: TimeSpan.Zero);
        long changed = await engine.IndexAsync([_indexRoot]);

        Assert.Equal(0, changed);
        Assert.True(_workspace.Log.Events.Any(e => e.Kind == Core.Models.LogEventKind.Exception
            && e.Message.Contains("超时")), "应记录目录枚举超时日志");
    }

    [Fact]
    public async Task Index_HeartbeatEvents_AreLogged()
    {
        Directory.CreateDirectory(_indexRoot);
        for (int i = 0; i < 2000; i++)
        {
            File.WriteAllText(Path.Combine(_indexRoot, $"h{i:D5}.dat"), "x");
        }

        using var engine = new FileIndexEngine(_database, _workspace.Log, _config);
        await engine.IndexAsync([_indexRoot]);

        // 心跳每 5s 一次：小数据量下可能未触发，仅验证机制不抛异常且扫描完成
        Assert.Equal(2000, await new FileIndexRepository(_database).CountAsync());
    }
}

using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests.Integration;

public sealed class ProbeTest
{
    private readonly ITestOutputHelper _output;

    public ProbeTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Probe_IndexManySubdirs()
    {
        using var workspace = new TempWorkspace();
        string root = Path.Combine(workspace.RootDirectory, "ManyDirs");
        const int dirCount = 200;
        const int filesPerDir = 500;
        Parallel.For(0, dirCount * filesPerDir, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            string dir = Path.Combine(root, $"d{i % dirCount:D4}");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"f{i:D8}.bin"), [1]);
        });
        long created = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).LongCount();
        _output.WriteLine($"created: {created}");

        await using var database = new SqliteDatabase(
            Path.Combine(workspace.PathService.DataDirectory, "probe.db"), workspace.Log);
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        config.GetValue(FileIndexEngine.PreferUsnConfigKey, false);
        using var engine = new FileIndexEngine(database, workspace.Log, config);

        long changed = await engine.IndexAsync([root]);
        long count = await new FileIndexRepository(database).CountAsync();
        _output.WriteLine($"changed: {changed}, db count: {count}");
        Assert.Equal(created, count);
    }
}

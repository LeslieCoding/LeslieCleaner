using LeslieCleaner.Core.Models;
using LeslieCleaner.Data;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Diagnostics;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using Xunit.Abstractions;

namespace LeslieCleaner.Tests;

/// <summary>
/// 阶段 1-5 性能基准：隔离区 1000 文件移动/还原、数据库 10 万行插入 + publisher 查询。
/// 真实数字输出到测试日志并写入 DEVLOG。
/// </summary>
[Trait("Category", "LongRunning")]
public sealed class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Benchmark_Quarantine_MoveAndRestore1000Files()
    {
        using var workspace = new TempWorkspace();
        using var config = new JsonConfigService(
            Path.Combine(workspace.PathService.ConfigDirectory, "settings.json"), workspace.Log);
        var service = new QuarantineService(workspace.PathService, config, workspace.Log);

        var files = new List<string>(1000);
        for (int i = 0; i < 1000; i++)
        {
            files.Add(workspace.CreateFile($"bench/file{i:D4}.tmp", new string('x', 256)));
        }

        BenchmarkResult quarantineResult = await PerformanceBenchmark.MeasureAsync(
            "Quarantine.Move1000Files", 1000,
            async ct => await service.QuarantineFilesAsync(files, "基准测试", cancellationToken: ct));

        IReadOnlyList<QuarantineEntry> entries = await service.ListEntriesAsync();
        Assert.Equal(1000, entries.Count);

        BenchmarkResult restoreResult = await PerformanceBenchmark.MeasureAsync(
            "Quarantine.Restore1000Files", 1000,
            async ct =>
            {
                foreach (QuarantineEntry entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    await service.RestoreAsync(entry.Id, cancellationToken: ct);
                }
            });

        Assert.All(files, file => Assert.True(File.Exists(file)));
        Report(quarantineResult);
        Report(restoreResult);
    }

    [Fact]
    public async Task Benchmark_Database_Insert100kRowsAndQueryByPublisher()
    {
        using var workspace = new TempWorkspace();
        string databaseFile = Path.Combine(workspace.PathService.DataDirectory, "bench.db");
        await using var database = new SqliteDatabase(databaseFile, workspace.Log);
        await database.InitializeAsync();
        var repository = new FileIndexRepository(database);

        var now = DateTimeOffset.UtcNow;
        IEnumerable<FileIndexRecord> GenerateRows()
        {
            for (int i = 0; i < 100_000; i++)
            {
                string publisher = i % 100 == 0 ? "Tencent" : $"Publisher-{i % 997}";
                yield return new FileIndexRecord(
                    $@"C:\Indexed\Dir{i % 500}\file{i:D6}.dll", $"file{i:D6}.dll",
                    publisher, $"Product-{i % 251}", 1024 + i, now, null, now);
            }
        }

        BenchmarkResult insertResult = await PerformanceBenchmark.MeasureAsync(
            "Database.Insert100kRows", 100_000,
            async ct => await repository.UpsertBatchAsync(GenerateRows(), ct));

        Assert.Equal(100_000, await repository.CountAsync());

        BenchmarkResult queryResult = await PerformanceBenchmark.MeasureAsync(
            "Database.QueryByPublisher", 1,
            async ct =>
            {
                IReadOnlyList<FileIndexRecord> rows = await repository.QueryByPublisherAsync("Tencent", ct);
                Assert.Equal(1000, rows.Count);
            });

        Report(insertResult);
        Report(queryResult);
    }

    private void Report(BenchmarkResult result)
    {
        _output.WriteLine(
            "[BENCHMARK] {0}: 总量={1}, 总耗时={2:F2}ms, 均值={3:F3}ms",
            result.Name, result.Iterations,
            result.TotalDuration.TotalMilliseconds,
            result.AveragePerIteration.TotalMilliseconds);
    }
}

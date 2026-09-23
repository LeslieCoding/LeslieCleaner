using LeslieCleaner.Modules.Cleaner.Analytics;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Analytics;

public sealed class LargeFileAnalyzerTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly LargeFileAnalyzerEngine _engine;

    public LargeFileAnalyzerTests() => _engine = new LargeFileAnalyzerEngine(_workspace.Log);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Analyze_TopFiles_OrderedBySize()
    {
        string root = BuildFixtureTree(out _);

        LargeFileAnalysisResult result = await _engine.AnalyzeAsync([root], topCount: 3);

        Assert.Equal(3, result.TopFiles.Count);
        Assert.Equal("huge.bin", Path.GetFileName(result.TopFiles[0].Path));
        Assert.True(result.TopFiles[0].SizeBytes >= result.TopFiles[1].SizeBytes);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task Analyze_TypeStats_AggregateByExtension()
    {
        string root = BuildFixtureTree(out _);

        LargeFileAnalysisResult result = await _engine.AnalyzeAsync([root]);

        FileTypeStat binStat = Assert.Single(result.TypeStats, s => s.Extension == ".bin");
        FileTypeStat logStat = Assert.Single(result.TypeStats, s => s.Extension == ".log");
        Assert.Equal(3, binStat.Count);
        Assert.Equal(2, logStat.Count);
        Assert.True(binStat.TotalBytes > logStat.TotalBytes);
        Assert.Equal(5, result.ScannedFiles);
    }

    [Fact]
    public async Task Analyze_Treemap_RollsUpSizes()
    {
        string root = BuildFixtureTree(out long totalBytes);

        LargeFileAnalysisResult result = await _engine.AnalyzeAsync([root]);

        TreemapNode rootNode = Assert.Single(result.Treemap);
        Assert.Equal(totalBytes, rootNode.SizeBytes);
        Assert.Equal(totalBytes, rootNode.Children.Sum(c => c.SizeBytes));
        Assert.Contains(rootNode.Children, c => c.Name == "sub");
    }

    [Fact]
    public async Task Analyze_ForbiddenProbe_SkipsDirectory()
    {
        string root = BuildFixtureTree(out _);
        var engine = new LargeFileAnalyzerEngine(_workspace.Log,
            forbiddenProbe: p => p.Contains("sub", StringComparison.OrdinalIgnoreCase));

        LargeFileAnalysisResult result = await engine.AnalyzeAsync([root]);

        Assert.DoesNotContain(result.TopFiles, f => f.Path.Contains("sub", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Suggestions_StaleDownloadsFile_IsSuggested()
    {
        string downloadsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "old.iso");
        var topFiles = new List<LargeFileEntry>
        {
            new(downloadsFile, 6L * 1024 * 1024 * 1024, DateTimeOffset.UtcNow.AddDays(-400)),
        };

        IReadOnlyList<string> suggestions = LargeFileAnalyzerEngine.BuildSuggestions(topFiles, DateTimeOffset.UtcNow);

        Assert.NotEmpty(suggestions);
        Assert.Contains("Downloads", suggestions[0]);
        Assert.Contains("400", suggestions[0]);
    }

    [Fact]
    public void Suggestions_HugeFile_IsSuggested()
    {
        var topFiles = new List<LargeFileEntry>
        {
            new(@"D:\Data\vm-disk.vhdx", 5L * 1024 * 1024 * 1024, DateTimeOffset.UtcNow.AddDays(-2)),
        };

        Assert.Single(LargeFileAnalyzerEngine.BuildSuggestions(topFiles, DateTimeOffset.UtcNow));
    }

    private string BuildFixtureTree(out long totalBytes)
    {
        string root = Path.Combine(_workspace.RootDirectory, "analyze");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllBytes(Path.Combine(root, "huge.bin"), new byte[10_000]);
        File.WriteAllBytes(Path.Combine(root, "mid.bin"), new byte[5_000]);
        File.WriteAllBytes(Path.Combine(root, "note.log"), new byte[500]);
        File.WriteAllBytes(Path.Combine(root, "sub", "small.bin"), new byte[1_000]);
        File.WriteAllBytes(Path.Combine(root, "sub", "trace.log"), new byte[300]);
        totalBytes = 10_000 + 5_000 + 500 + 1_000 + 300;
        return root;
    }
}

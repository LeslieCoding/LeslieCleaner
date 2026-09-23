using System.Drawing;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Modules.Cleaner.Duplicates;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Duplicates;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DuplicateFinderTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly JsonConfigService _config;
    private readonly QuarantineService _quarantine;
    private readonly DuplicateFinderEngine _engine;

    public DuplicateFinderTests()
    {
        _config = new JsonConfigService(
            Path.Combine(_workspace.PathService.ConfigDirectory, "settings.json"), _workspace.Log);
        _quarantine = new QuarantineService(_workspace.PathService, _config, _workspace.Log);
        _engine = new DuplicateFinderEngine(_quarantine, _workspace.Log);
    }

    public void Dispose()
    {
        _config.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Find_ReportsProgress_WithCounts()
    {
        // 扫描进度必须持续上报（阶段+已处理数），否则 UI 只能"干等"
        string dupDir = Path.Combine(_workspace.RootDirectory, "dups-progress");
        Directory.CreateDirectory(dupDir);
        string content = new('x', 200_000);
        File.WriteAllText(Path.Combine(dupDir, "a1.bin"), content);
        File.WriteAllText(Path.Combine(dupDir, "a2.bin"), content);

        var reports = new List<ScanProgress>();
        IReadOnlyList<DuplicateGroup> groups = await _engine.FindDuplicatesAsync(
            [dupDir], progress: new Progress<ScanProgress>(reports.Add));

        Assert.NotEmpty(groups);
        Assert.NotEmpty(reports);
        Assert.Contains(reports, r => r.Phase == "部分哈希");
    }

    [Fact]
    public async Task Find_ThreeTier_GroupsExactDuplicates()
    {
        string content = new('x', 200_000); // 超过部分哈希阈值，确保三层都被触发
        string dupDir = Path.Combine(_workspace.RootDirectory, "dups");
        Directory.CreateDirectory(dupDir);
        foreach (string name in new[] { "a1.bin", "a2.bin", "a3.bin" })
        {
            File.WriteAllText(Path.Combine(dupDir, name), content);
        }

        foreach (string name in new[] { "b1.bin", "b2.bin" })
        {
            File.WriteAllText(Path.Combine(dupDir, name), new string('y', 200_000));
        }

        File.WriteAllText(Path.Combine(dupDir, "unique.bin"), new string('z', 200_000));
        File.WriteAllText(Path.Combine(dupDir, "tiny.bin"), "small");

        IReadOnlyList<DuplicateGroup> groups = await _engine.FindDuplicatesAsync([dupDir]);

        Assert.Equal(2, groups.Count);
        DuplicateGroup groupA = groups.Single(g => g.Files.Count == 3);
        DuplicateGroup groupB = groups.Single(g => g.Files.Count == 2);
        Assert.Equal(groupA.SizeEach * 2, groupA.WastedBytes);
        Assert.Equal(groupB.SizeEach, groupB.WastedBytes);
    }

    [Fact]
    public async Task Find_MediaFiles_AreLayeredSeparately()
    {
        string content = new('m', 150_000);
        string dir = Path.Combine(_workspace.RootDirectory, "media");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "clip.mp4"), content);
        File.WriteAllText(Path.Combine(dir, "clip-copy.mp4"), content);
        File.WriteAllText(Path.Combine(dir, "song.mp3"), content); // 同内容不同层，不与视频成组

        IReadOnlyList<DuplicateGroup> groups = await _engine.FindDuplicatesAsync([dir]);

        Assert.Single(groups);
        Assert.All(groups[0].Files, f => Assert.EndsWith(".mp4", f));
        Assert.Equal(MediaKind.Video, DuplicateFinderEngine.ClassifyMedia("a.mkv"));
        Assert.Equal(MediaKind.Audio, DuplicateFinderEngine.ClassifyMedia("a.flac"));
        Assert.Equal(MediaKind.None, DuplicateFinderEngine.ClassifyMedia("a.txt"));
    }

    [Fact]
    public void Retention_Policies()
    {
        string dir = Path.Combine(_workspace.RootDirectory, "ret");
        Directory.CreateDirectory(dir);
        string old = Path.Combine(dir, "old-name-longer.bin");
        string recent = Path.Combine(dir, "n.bin");
        File.WriteAllText(old, "same");
        File.WriteAllText(recent, "same");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow);

        Assert.Equal(recent, DuplicateRetention.Select([old, recent], KeepPolicy.Newest).Keeper);
        Assert.Equal(old, DuplicateRetention.Select([old, recent], KeepPolicy.Oldest).Keeper);
        Assert.Equal(recent, DuplicateRetention.Select([old, recent], KeepPolicy.ShortestPath).Keeper);

        // PreferredDirectory：目录内文件优先（与目录外文件比较）
        string outside = Path.Combine(_workspace.RootDirectory, "outside.bin");
        File.WriteAllText(outside, "same");
        Assert.Equal(recent, DuplicateRetention.Select([outside, recent], KeepPolicy.PreferredDirectory, dir).Keeper);
    }

    [Fact]
    public void PerceptualHash_NearIdentical_IsSimilar()
    {
        string imageA = Path.Combine(_workspace.RootDirectory, "a.png");
        string imageB = Path.Combine(_workspace.RootDirectory, "b.png");
        string imageC = Path.Combine(_workspace.RootDirectory, "c.png");
        using (var bitmap = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.CornflowerBlue);
            }

            bitmap.Save(imageA, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.Save(imageB, System.Drawing.Imaging.ImageFormat.Png);
        }

        using (var bitmap = new Bitmap(32, 32))
        {
            // 左右半黑半白渐变：与纯色图的 aHash 差异显著
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bitmap.SetPixel(x, y, x < 16 ? Color.Black : Color.White);
                }
            }

            bitmap.Save(imageC, System.Drawing.Imaging.ImageFormat.Png);
        }

        ulong hashA = PerceptualHash.ComputeAverageHash(imageA);
        ulong hashB = PerceptualHash.ComputeAverageHash(imageB);
        ulong hashC = PerceptualHash.ComputeAverageHash(imageC);

        Assert.True(PerceptualHash.HammingDistance(hashA, hashB) <= PerceptualHash.SimilarityThreshold);
        Assert.True(PerceptualHash.HammingDistance(hashA, hashC) > PerceptualHash.SimilarityThreshold);
    }

    [Fact]
    public async Task DeleteRedundant_GoesToQuarantine()
    {
        string content = new('q', 50_000);
        string dir = Path.Combine(_workspace.RootDirectory, "del");
        Directory.CreateDirectory(dir);
        string keeper = Path.Combine(dir, "keep.bin");
        string redundant = Path.Combine(dir, "redundant.bin");
        File.WriteAllText(keeper, content);
        File.WriteAllText(redundant, content);
        File.SetLastWriteTimeUtc(keeper, DateTime.UtcNow);

        IReadOnlyList<DuplicateGroup> groups = await _engine.FindDuplicatesAsync([dir]);
        await _engine.DeleteRedundantAsync(groups, KeepPolicy.Newest);

        Assert.True(File.Exists(keeper));
        Assert.False(File.Exists(redundant));
        Assert.Single(await _quarantine.ListEntriesAsync());
    }
}

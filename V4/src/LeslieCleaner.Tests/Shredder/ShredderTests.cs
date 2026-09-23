using LeslieCleaner.Core.Models;
using LeslieCleaner.Infrastructure.Security;
using LeslieCleaner.Modules.Shredder;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Shredder;

public sealed class ShredderTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly ShredderEngine _engine;

    public ShredderTests() =>
        _engine = new ShredderEngine(_workspace.Log, ssdProbe: static _ => false);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void PatternTables_AreCorrect()
    {
        Assert.Equal(3, ShredPatternProvider.GetPasses(ShredMethod.DoD3Pass).Count);
        Assert.Equal(7, ShredPatternProvider.GetPasses(ShredMethod.DoD7Pass).Count);
        Assert.Equal(35, ShredPatternProvider.GetPasses(ShredMethod.Gutmann35Pass).Count);
        Assert.Single(ShredPatternProvider.GetPasses(ShredMethod.SsdSafe));

        IReadOnlyList<ShredPatternProvider.PassPattern> gutmann = ShredPatternProvider.GetPasses(ShredMethod.Gutmann35Pass);
        Assert.Equal(ShredPatternProvider.PassPatternKind.Random, gutmann[0].Kind);
        Assert.Equal(ShredPatternProvider.PassPatternKind.Fixed, gutmann[4].Kind);
        Assert.Equal(ShredPatternProvider.PassPatternKind.Random, gutmann[34].Kind);
    }

    [Fact]
    public void FillBuffer_FixedPattern_Rotates()
    {
        var pass = new ShredPatternProvider.PassPattern(ShredPatternProvider.PassPatternKind.Fixed, [0x92, 0x49, 0x24]);
        Span<byte> buffer = stackalloc byte[8];

        ShredPatternProvider.FillBuffer(pass, buffer);

        Assert.Equal(new byte[] { 0x92, 0x49, 0x24, 0x92, 0x49, 0x24, 0x92, 0x49 }, buffer.ToArray());
    }

    [Fact]
    public void FillBuffer_Random_IsNonDeterministic()
    {
        var pass = new ShredPatternProvider.PassPattern(ShredPatternProvider.PassPatternKind.Random, null);
        Span<byte> a = stackalloc byte[64];
        Span<byte> b = stackalloc byte[64];

        ShredPatternProvider.FillBuffer(pass, a);
        ShredPatternProvider.FillBuffer(pass, b);

        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public async Task Shred_DoD3_FileDisappearsWithThreePasses()
    {
        string file = _workspace.CreateFile("shred/secret.txt", new string('s', 4096));
        _engine.AddFiles([file]);
        var progress = new List<ShredProgress>();

        IReadOnlyList<ShredTask> tasks = await _engine.StartAsync(
            ShredMethod.DoD3Pass, new Progress<ShredProgress>(p => progress.Add(p)));

        Assert.Single(tasks);
        Assert.Equal(ShredTaskStatus.Completed, tasks[0].Status);
        Assert.False(File.Exists(file));
        Assert.Contains(progress, p => p.CurrentPass == 3);
    }

    [Fact]
    public async Task Shred_Folder_Recurses()
    {
        _workspace.CreateFile("tree/a.txt", "a");
        _workspace.CreateFile("tree/sub/b.txt", "b");
        _engine.AddFolder(Path.Combine(_workspace.RootDirectory, "tree"));

        IReadOnlyList<ShredTask> tasks = await _engine.StartAsync();

        Assert.Equal(ShredTaskStatus.Completed, tasks[0].Status);
        Assert.Equal(2, tasks[0].ProcessedFiles);
        Assert.False(Directory.Exists(Path.Combine(_workspace.RootDirectory, "tree")));
    }

    [Fact]
    public void Guard_ProtectedPaths_AreBlocked()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string programFiles = Environment.GetEnvironmentVariable("ProgramFiles")!;

        Assert.True(ShredderEngine.IsProtectedPath(Path.Combine(windows, "notepad.exe")));
        Assert.True(ShredderEngine.IsProtectedPath(Path.Combine(programFiles, "App", "app.exe")));
        Assert.True(ShredderEngine.IsProtectedPath(@"C:\"));
        Assert.False(ShredderEngine.IsProtectedPath(Path.Combine(_workspace.RootDirectory, "ok.txt")));
    }

    [Fact]
    public void AddFiles_ProtectedPath_MarkedBlocked()
    {
        string windowsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");

        IReadOnlyList<ShredTask> tasks = _engine.AddFiles([windowsFile]);

        Assert.Single(tasks);
        Assert.Equal(ShredTaskStatus.Blocked, tasks[0].Status);
        Assert.NotNull(tasks[0].Error);
    }

    [Fact]
    public void AddFiles_MissingFile_MarkedBlocked()
    {
        IReadOnlyList<ShredTask> tasks = _engine.AddFiles(
            [Path.Combine(_workspace.RootDirectory, "ghost.bin")]);

        Assert.Equal(ShredTaskStatus.Blocked, tasks[0].Status);
    }

    [Fact]
    public void SsdProbe_ParsesDescriptor()
    {
        byte[] ssd = new byte[64]; // IncursSeekPenalty = 0 → SSD
        byte[] hdd = new byte[64];
        hdd[8] = 1;

        Assert.True(StorageMediaDetector.ParseSeekPenaltyDescriptor(ssd));
        Assert.False(StorageMediaDetector.ParseSeekPenaltyDescriptor(hdd));
        Assert.False(StorageMediaDetector.ParseSeekPenaltyDescriptor([1, 2]));
    }

    [Fact]
    public async Task Shred_OnSsd_UsesSingleRandomPass()
    {
        var ssdEngine = new ShredderEngine(_workspace.Log, ssdProbe: static _ => true);
        string file = _workspace.CreateFile("ssd/doc.bin", new string('d', 2048));
        ssdEngine.AddFiles([file]);
        var progress = new List<ShredProgress>();

        await ssdEngine.StartAsync(ShredMethod.Gutmann35Pass, new Progress<ShredProgress>(p => progress.Add(p)));

        Assert.False(File.Exists(file));
        Assert.DoesNotContain(progress, p => p.CurrentPass > 1);
    }

    [Fact]
    public async Task RemovePendingTasks_RemovedTaskIsNeverExecuted()
    {
        string file = _workspace.CreateFile("queue/remove-me.txt", "must remain");
        ShredTask task = Assert.Single(_engine.AddFiles([file]));

        Assert.Equal(1, _engine.RemovePendingTasks([task.Id]));
        IReadOnlyList<ShredTask> tasks = await _engine.StartAsync();

        Assert.Empty(tasks);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task Shred_SecurityGateBlocked_PreservesPendingFile()
    {
        string file = _workspace.CreateFile("queue/security-gated.txt", "must remain");
        var gate = new SecurityOperationGate();
        gate.Apply(new SecurityAssessment(false, true, true, 0, 0, 60, "integrity failed", DateTimeOffset.UtcNow));
        var guarded = new ShredderEngine(_workspace.Log, ssdProbe: static _ => false, securityOperationGate: gate);
        guarded.AddFiles([file]);

        await Assert.ThrowsAsync<LeslieCleaner.Core.Diagnostics.LeslieCleanerException>(() => guarded.StartAsync());

        Assert.True(File.Exists(file));
    }
}

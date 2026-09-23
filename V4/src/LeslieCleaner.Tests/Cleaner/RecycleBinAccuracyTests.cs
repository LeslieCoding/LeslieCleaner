using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Cleaner;

/// <summary>回收站逻辑大小、磁盘占用与复验语义的回归测试。</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class RecycleBinAccuracyTests
{
    /// <summary>2 KB 逻辑载荷占用 8 KB 时，扫描不得继续错误显示为 2 KB 可释放空间。</summary>
    [Fact]
    public async Task Scan_UsesAllocatedBytesForReclaimableSpace_AndPreservesLogicalBytes()
    {
        var snapshot = new RecycleBinSnapshot(
            ItemCount: 1,
            SizeBytes: 2048,
            AllocatedSizeBytes: 8192,
            MetadataSizeBytes: 544,
            EnumeratedPayloadSizeBytes: 2048,
            InaccessibleLocationCount: 0,
            Source: RecycleBinMeasurementSource.WindowsShellAndFileSystem);
        var recycleBin = new FakeRecycleBinService(snapshot);
        using var workspace = new TempWorkspace();
        var engine = new SmartScanEngine(new TestScanEnvironment(), registryScanner: null, workspace.Log, recycleBin);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["RecycleBin"] });

        ScannedItem item = Assert.Single(result.Items);
        Assert.Equal(8192, item.SizeBytes);
        Assert.Equal(2048, item.LogicalSizeBytes);
        Assert.Equal(8192, item.AllocatedSizeBytes);
        Assert.Equal(544, item.MetadataSizeBytes);
        Assert.Equal("WindowsShellAndFileSystem", item.MeasurementSource);
        Assert.Equal(2048, result.TotalSizeBytes);
        Assert.Equal(8192, result.TotalAllocatedSizeBytes);
    }

    /// <summary>Shell 已显示空但文件系统仍不可访问时，不得伪报“已验证清空”。</summary>
    [Fact]
    public void EmptyResult_IsNotVerified_WhenAllocationMeasurementIsIncomplete()
    {
        var before = new RecycleBinSnapshot(1, 2048, 8192, Source: RecycleBinMeasurementSource.WindowsShellAndFileSystem);
        var after = new RecycleBinSnapshot(
            0,
            0,
            AllocatedSizeBytes: 0,
            InaccessibleLocationCount: 1,
            Source: RecycleBinMeasurementSource.WindowsShellAndFileSystem);

        Assert.False(new RecycleBinEmptyResult(before, after).IsEmpty);
    }

    /// <summary>Shell 无逻辑项目但仍有孤留磁盘占用时，扫描必须暴露而不能静默遗漏。</summary>
    [Fact]
    public async Task Scan_ReportsOrphanedAllocation_WhenShellCountIsZero()
    {
        var snapshot = new RecycleBinSnapshot(
            0,
            0,
            AllocatedSizeBytes: 4096,
            InaccessibleLocationCount: 1,
            Source: RecycleBinMeasurementSource.WindowsShellAndFileSystem);
        var recycleBin = new FakeRecycleBinService(snapshot);
        using var workspace = new TempWorkspace();
        var engine = new SmartScanEngine(new TestScanEnvironment(), registryScanner: null, workspace.Log, recycleBin);

        ScanResult result = await engine.ScanAsync(new ScanOptions { Targets = ["RecycleBin"] });

        ScannedItem item = Assert.Single(result.Items);
        Assert.Equal(4096, item.SizeBytes);
        Assert.Equal(1, item.InaccessibleItemCount);
    }
    /// <summary>在 Windows 实机上校验 Shell 适配器与 NTFS 测量可共同返回非负权威快照。</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SystemService_ReturnsLiveDualMetricSnapshot()
    {
        using var workspace = new TempWorkspace();
        var service = new LeslieCleaner.Infrastructure.Services.SystemRecycleBinService(workspace.Log);

        RecycleBinSnapshot snapshot = await service.GetSnapshotAsync();

        Assert.True(snapshot.ItemCount >= 0);
        Assert.True(snapshot.SizeBytes >= 0);
        Assert.True(snapshot.AllocatedSizeBytes >= 0);
        Assert.True(snapshot.MetadataSizeBytes >= 0);
        Assert.Equal(RecycleBinMeasurementSource.WindowsShellAndFileSystem, snapshot.Source);
    }
    /// <summary>普通垃圾文件也必须保留逻辑大小与 Windows 实际分配大小两种可核对口径。</summary>
    [Fact]
    public void FileMeasurement_ReturnsActualAllocation_ForRegularFile()
    {
        using var workspace = new TempWorkspace();
        string path = Path.Combine(workspace.RootDirectory, "allocation.bin");
        File.WriteAllBytes(path, System.Security.Cryptography.RandomNumberGenerator.GetBytes(2048));

        bool measured = LeslieCleaner.Modules.Cleaner.Scanning.WindowsFileSizeMeasurement.TryGetAllocatedSize(
            path,
            out long allocatedBytes);

        Assert.True(measured);
        Assert.True(allocatedBytes >= 2048);
    }}

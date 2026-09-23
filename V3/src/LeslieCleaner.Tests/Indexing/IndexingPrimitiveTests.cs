using LeslieCleaner.Infrastructure.Configuration;
using LeslieCleaner.Infrastructure.Indexing;
using LeslieCleaner.Data;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests.Indexing;

public sealed class MmfStringStoreTests
{
    [Fact]
    public void AddAndSortedDistinct_DeduplicatesAndSorts()
    {
        using var store = new MmfStringStore();
        store.Add("beta");
        store.Add("alpha");
        store.Add("beta");
        store.Add("gamma");
        store.Add("alpha");

        IReadOnlyList<string> result = store.SortedDistinct();

        Assert.Equal(3, result.Count);
        Assert.Equal("alpha", result[0]);
        Assert.Equal("beta", result[1]);
        Assert.Equal("gamma", result[2]);
    }

    [Fact]
    public void Add_HandlesLargeVolume()
    {
        using var store = new MmfStringStore();
        for (int i = 0; i < 10_000; i++)
        {
            store.Add($@"C:\Indexed\Dir{i % 100}\file{i:D6}.dll");
        }

        IReadOnlyList<string> result = store.SortedDistinct();

        Assert.Equal(10_000, result.Count);
    }

    [Fact]
    public void Add_TruncatesOversizedEntries()
    {
        using var store = new MmfStringStore();
        store.Add(new string('x', 10_000));

        IReadOnlyList<string> result = store.SortedDistinct();

        Assert.Single(result);
        Assert.True(result[0].Length <= 4096);
    }
    [Fact]
    public void Add_BeyondInitialCapacity_GrowsCorrectly()
    {
        using var store = new MmfStringStore();
        // 30 万条长路径（约 35MB，远超 16MB 初始容量），验证 2 倍扩容不丢数据
        const int count = 300_000;
        for (int i = 0; i < count; i++)
        {
            store.Add($@"C:\Users\test\AppData\Local\Temp\LeslieCleaner.Tests\abcdef0123456789\Index\d{i % 1000:D4}\file{i:D8}.bin");
        }

        IReadOnlyList<string> result = store.SortedDistinct();

        Assert.Equal(count, result.Count);
        Assert.Equal(store.Count, result.Count);
    }
}

public sealed class ScanScopeTests
{
    [Fact]
    public void IsForbidden_WindowsTree_IsExcluded()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.True(ScanScope.IsForbidden(Path.Combine(windows, "System32", "kernel32.dll")));
        Assert.True(ScanScope.IsForbidden(Path.Combine(windows, "WinSxS", "amd64_x", "f.dll")));
        Assert.True(ScanScope.IsForbidden(Path.Combine(windows, "System32", "DriverStore", "x.inf")));
    }

    [Fact]
    public void IsForbidden_SwapFiles_AreExcluded()
    {
        Assert.True(ScanScope.IsForbidden(@"C:\pagefile.sys"));
        Assert.True(ScanScope.IsForbidden(@"D:\hiberfil.sys"));
        Assert.True(ScanScope.IsForbidden(@"C:\swapfile.sys"));
    }

    [Fact]
    public void IsForbidden_AllowedAreas_Pass()
    {
        string programFiles = Environment.GetEnvironmentVariable("ProgramFiles")!;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.False(ScanScope.IsForbidden(Path.Combine(programFiles, "App", "app.exe")));
        Assert.False(ScanScope.IsForbidden(Path.Combine(localAppData, "App", "cache.bin")));
    }

    [Fact]
    public void GetAllowedRoots_ContainsExpectedLocations()
    {
        IReadOnlyList<string> roots = ScanScope.GetAllowedRoots();

        Assert.Contains(roots, r => r.Equals(Environment.GetEnvironmentVariable("ProgramFiles"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(roots, r => r.Equals(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(roots, r => r.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class UsnMftEnumeratorTests
{
    [Fact]
    public void TryCreate_ReturnsNullOrUsableEnumerator()
    {
        string systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!;

        using UsnMftEnumerator? enumerator = UsnMftEnumerator.TryCreate(systemDrive);

        // 非管理员返回 null（优雅降级信号）；管理员返回可用枚举器
        if (enumerator is not null)
        {
            IEnumerator<UsnMftEnumerator.UsnFileEntry> iterator = enumerator.EnumerateAll().GetEnumerator();
            Assert.True(iterator.MoveNext());
            Assert.False(string.IsNullOrEmpty(iterator.Current.Path));
            iterator.Dispose();
        }
    }

    [Fact]
    public void TryCreate_InvalidInput_ReturnsNull()
    {
        Assert.Null(UsnMftEnumerator.TryCreate(""));
        Assert.Null(UsnMftEnumerator.TryCreate("not-a-volume"));
    }
}

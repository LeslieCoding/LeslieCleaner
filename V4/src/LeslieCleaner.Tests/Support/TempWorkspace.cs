namespace LeslieCleaner.Tests.Support;

/// <summary>临时目录基座：每个测试用例一个独立目录，用例结束整体删除。</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "LeslieCleaner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootDirectory);
        PathService = new TestPathService(RootDirectory);
        Log = new InMemoryLogService();
    }

    public string RootDirectory { get; }

    public TestPathService PathService { get; }

    public InMemoryLogService Log { get; }

    public string CreateFile(string relativePath, string content = "leslie-cleaner-test-payload")
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录清理失败不影响测试结果，交由 OS 回收
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

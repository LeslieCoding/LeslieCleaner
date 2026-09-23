using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Cleaner.Scanning;

namespace LeslieCleaner.Tests.Support;

/// <summary>测试扫描环境：位置键映射到临时目录，通配展开逻辑与生产实现一致。</summary>
public sealed class TestScanEnvironment : IScanEnvironment
{
    private readonly Dictionary<string, string> _locations = new(StringComparer.OrdinalIgnoreCase);

    public TestScanEnvironment Map(string locationKey, string path)
    {
        _locations[locationKey] = path;
        return this;
    }

    public string? ResolveLocation(string locationKey) =>
        _locations.TryGetValue(locationKey, out string? path) ? path : null;

    public IReadOnlyList<string> ExpandDirectories(string locationKey, string relativePattern)
    {
        string? basePath = ResolveLocation(locationKey);
        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
        {
            return [];
        }

        var current = new List<string> { basePath };
        foreach (string segment in relativePattern.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = new List<string>();
            foreach (string directory in current)
            {
                if (segment == "*")
                {
                    try
                    {
                        next.AddRange(Directory.EnumerateDirectories(directory));
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                else
                {
                    string candidate = Path.Combine(directory, segment);
                    if (Directory.Exists(candidate))
                    {
                        next.Add(candidate);
                    }
                }
            }

            current = next;
            if (current.Count == 0)
            {
                break;
            }
        }

        return current;
    }
}

/// <summary>返回固定命中项的测试注册表扫描器。</summary>
public sealed class FakeRegistryScanner(IReadOnlyList<RegistryFinding> findings) : IRegistryScanner
{
    public Task<IReadOnlyList<RegistryFinding>> ScanAsync(
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(findings);
}

public sealed class FakeRecycleBinService(RecycleBinSnapshot snapshot) : ISystemRecycleBinService
{
    public RecycleBinSnapshot Snapshot { get; private set; } = snapshot;

    public Task<RecycleBinSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshot);

    public Task<RecycleBinEmptyResult> EmptyAsync(CancellationToken cancellationToken = default)
    {
        RecycleBinSnapshot before = Snapshot;
        Snapshot = new RecycleBinSnapshot(0, 0);
        return Task.FromResult(new RecycleBinEmptyResult(before, Snapshot));
    }
}

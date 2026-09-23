using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;

namespace LeslieCleaner.Tests;

public sealed class PathServiceTests
{
    [Fact]
    public void ProgramDataLayout_UsesEnvironmentSpecialFolders()
    {
        var service = new PathService();
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(programData, "LeslieCleaner"), service.ProgramDataRoot);
        Assert.StartsWith(service.ProgramDataRoot, service.QuarantineDirectory);
        Assert.StartsWith(service.ProgramDataRoot, service.BackupDirectory);
        Assert.StartsWith(service.ProgramDataRoot, service.DataDirectory);
        Assert.StartsWith(Path.Combine(localAppData, "LeslieCleaner"), service.LogDirectory);
        Assert.StartsWith(Path.Combine(localAppData, "LeslieCleaner"), service.ConfigDirectory);
    }

    [Fact]
    public void EnsureDirectory_CreatesMissingDirectory()
    {
        using var workspace = new TempWorkspace();
        string target = Path.Combine(workspace.RootDirectory, "a", "b", "c");

        string result = workspace.PathService.EnsureDirectory(target);

        Assert.Equal(target, result);
        Assert.True(Directory.Exists(target));
    }
}

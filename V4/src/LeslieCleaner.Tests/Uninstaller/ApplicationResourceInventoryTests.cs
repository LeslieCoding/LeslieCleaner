using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Workflow;

namespace LeslieCleaner.Tests.Uninstaller;

public sealed class ApplicationResourceInventoryTests
{
    [Fact]
    public void CommandBelongs_QuotedExecutableInsideRoot_ReturnsTrue()
    {
        string root = Path.Combine(Path.GetTempPath(), "Vendor App");
        string command = $"\"{Path.Combine(root, "agent.exe")}\" --background";
        Assert.True(WindowsApplicationResourceInventory.CommandBelongsToApplication(command, root));
    }

    [Fact]
    public void CommandBelongs_UnquotedExecutableWithArguments_ReturnsTrue()
    {
        string root = Path.Combine(Path.GetTempPath(), "Vendor");
        string command = $"{Path.Combine(root, "agent.exe")} --background";
        Assert.True(WindowsApplicationResourceInventory.CommandBelongsToApplication(command, root));
    }

    [Fact]
    public void CommandBelongs_PrefixSiblingEscape_ReturnsFalse()
    {
        string root = Path.Combine(Path.GetTempPath(), "Vendor");
        string sibling = root + "-Evil";
        Assert.False(WindowsApplicationResourceInventory.CommandBelongsToApplication(
            $"\"{Path.Combine(sibling, "agent.exe")}\"", root));
    }

    [Fact]
    public void CommandBelongs_RelativeExecutable_ReturnsFalse()
    {
        Assert.False(WindowsApplicationResourceInventory.CommandBelongsToApplication(
            "agent.exe --background", Path.GetTempPath()));
    }

    [Fact]
    public async Task Collect_MissingInstallLocation_ReturnsEmpty()
    {
        var app = new InstalledApp("app", "App", null, null, 0, null, null, null,
            AppSourceType.Win32, null, null);
        IReadOnlyList<FingerprintEntry> result = await new WindowsApplicationResourceInventory().CollectAsync(app);
        Assert.Empty(result);
    }
}

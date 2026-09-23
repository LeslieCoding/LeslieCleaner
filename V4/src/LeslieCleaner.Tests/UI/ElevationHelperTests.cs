using LeslieCleaner.UI.Services;

namespace LeslieCleaner.Tests.UI;

/// <summary>管理员重启不能重放会改变页面或安全状态的一次性启动参数。</summary>
public sealed class ElevationHelperTests
{
    [Fact]
    public void BuildRestartArguments_RemovesOneShotStateAndKeepsStableOptions()
    {
        string oldRegistryToken = Guid.NewGuid().ToString("N");
        string newRegistryToken = Guid.NewGuid().ToString("N");
        string[] current =
        [
            "--theme", "dark",
            "--page", "Smart Scan",
            "--autoscan",
            "--registry-resume", oldRegistryToken,
            StartupResumeTarget.ArgumentName, "Run|Old",
            "--provision-integrity-baseline",
            "--diagnostic-mode",
        ];

        IReadOnlyList<string> merged = ElevationHelper.BuildRestartArguments(
            current, ["--page", "注册表清理", "--registry-resume", newRegistryToken]);

        Assert.Equal(
            ["--theme", "dark", "--diagnostic-mode", "--page", "注册表清理", "--registry-resume", newRegistryToken],
            merged);
        Assert.DoesNotContain("--autoscan", merged);
        Assert.DoesNotContain("--provision-integrity-baseline", merged);
        Assert.DoesNotContain(oldRegistryToken, merged);
    }
}

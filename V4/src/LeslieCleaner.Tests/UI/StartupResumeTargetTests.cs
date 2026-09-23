using LeslieCleaner.UI.Services;

namespace LeslieCleaner.Tests.UI;

/// <summary>提权重启恢复参数必须保持为数据，不得接受控制字符注入。</summary>
public sealed class StartupResumeTargetTests
{
    [Theory]
    [InlineData("run:HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run:OneDrive")]
    [InlineData(" task:\\Vendor\\Agent ")]
    public void TryNormalize_AcceptsStableNonControlIdentifiers(string value)
    {
        bool accepted = StartupResumeTarget.TryNormalize(value, out string? itemId);

        Assert.True(accepted);
        Assert.NotNull(itemId);
        Assert.DoesNotContain(itemId, char.IsControl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("run:ok\r\n--page evil")]
    public void TryNormalize_RejectsEmptyOrControlCharacterInput(string? value)
    {
        Assert.False(StartupResumeTarget.TryNormalize(value, out string? itemId));
        Assert.Null(itemId);
    }
}

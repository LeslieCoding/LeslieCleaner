using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller;
using LeslieCleaner.Modules.Uninstaller.Residual;

namespace LeslieCleaner.Tests.Uninstaller;

public sealed class ResidualMatchScorerTests
{
    [Fact]
    public void ProductNameWinsOverUnrelatedPublisherMatch()
    {
        var candidate = new InstalledApp(
            "wrong", "Other Product", "1.0", "Target Publisher", 0, null, null, null,
            AppSourceType.Win32, null, null);

        int score = ResidualMatchScorer.Score(
            candidate,
            new ResidualQuery("Target Publisher", "Wanted Product", null));

        Assert.Equal(-1, score);
    }

    [Fact]
    public void ExactProductWithMissingPublisherStillMatchesAtLowerConfidence()
    {
        var candidate = new InstalledApp(
            "wanted", "Wanted Product", "1.0", null, 0, null, null, null,
            AppSourceType.Win32, null, null);

        int score = ResidualMatchScorer.Score(
            candidate,
            new ResidualQuery("Publisher From Rule", "Wanted Product", null));

        Assert.Equal(75, score);
    }

    [Fact]
    public void PublisherOnlyQueryRejectsDifferentPublisher()
    {
        var candidate = new InstalledApp(
            "other", "Other Product", "1.0", "Other Publisher", 0, null, null, null,
            AppSourceType.Win32, null, null);

        int score = ResidualMatchScorer.Score(
            candidate,
            new ResidualQuery("Target Publisher", null, null));

        Assert.Equal(-1, score);
    }

    [Fact]
    public void UnicodeAndWhitespaceNormalizationKeepsExactMatchStable()
    {
        var candidate = new InstalledApp(
            "app", "Ａｄｏｂｅ   Reader", "1.0", "Adobe", 0, null, null, null,
            AppSourceType.Win32, null, null);

        int score = ResidualMatchScorer.Score(
            candidate,
            new ResidualQuery(" adobe ", "Adobe Reader", null));

        Assert.Equal(190, score);
    }
}

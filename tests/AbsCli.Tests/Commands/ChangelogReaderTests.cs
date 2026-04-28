using AbsCli.Services;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ChangelogReaderTests
{
    private const string TwoEntries =
        "# Changelog\n" +
        "\n" +
        "All notable changes are documented here.\n" +
        "\n" +
        "## 0.2.7 — 2026-04-24\n" +
        "\n" +
        "### Highlights\n" +
        "- latest highlight\n" +
        "\n" +
        "### Fixes\n" +
        "- fix: latest fix\n" +
        "\n" +
        "## 0.2.6 — 2026-04-24\n" +
        "\n" +
        "### Highlights\n" +
        "- older highlight\n";

    [Fact]
    public void ExtractLatest_ReturnsBlockStartingAtTopmostVersionHeading()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.StartsWith("## 0.2.7 — 2026-04-24", result);
    }

    [Fact]
    public void ExtractLatest_StopsBeforeNextVersionHeading()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.DoesNotContain("## 0.2.6", result);
        Assert.DoesNotContain("older highlight", result);
    }

    [Fact]
    public void ExtractLatest_IncludesSubHeadingsWithinLatestEntry()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.Contains("### Highlights", result);
        Assert.Contains("### Fixes", result);
        Assert.Contains("- latest highlight", result);
        Assert.Contains("- fix: latest fix", result);
    }

    [Fact]
    public void ExtractLatest_TrimsTrailingBlankLines()
    {
        var input =
            "## 1.0.0 — 2026-01-01\n" +
            "\n" +
            "body\n" +
            "\n" +
            "\n";
        var result = ChangelogReader.ExtractLatest(input);
        Assert.EndsWith("body", result);
        Assert.False(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractLatest_ThrowsWhenNoVersionHeadingPresent()
    {
        var input = "# Changelog\n\nNo entries yet.\n";
        var ex = Assert.Throws<InvalidOperationException>(
            () => ChangelogReader.ExtractLatest(input));
        Assert.Equal("CHANGELOG.md has no version entries", ex.Message);
    }
}

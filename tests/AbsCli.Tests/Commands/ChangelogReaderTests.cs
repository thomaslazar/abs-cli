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
}

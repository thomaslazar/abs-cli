using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class RelPathMatcherTests
{
    // A series segment long enough (>247 UTF-16 bytes ≈ >123 chars) to be
    // truncated by ABS. 130 'a's = 260 bytes.
    private static string Long(char c, int n) => new string(c, n);

    [Fact]
    public void IsMatch_ShortSegments_RequireExactEquality()
    {
        Assert.True(RelPathMatcher.IsMatch("Yone/Series/Title", "Yone/Series/Title"));
        Assert.False(RelPathMatcher.IsMatch("Yone/Series/Title", "Yone/Series/Other"));
    }

    [Fact]
    public void IsMatch_DifferentSegmentCount_NoMatch()
    {
        Assert.False(RelPathMatcher.IsMatch("Author/Title", "Author/Series/Title"));
    }

    [Fact]
    public void IsMatch_LongSegment_TruncatedTailsMatchOnPrefix()
    {
        // Predicted (untruncated) vs server (cut + re-appended ".«") share a
        // >100-char common prefix → same segment.
        var predicted = "Yone/" + Long('x', 130);
        var actual = "Yone/" + Long('x', 123) + ".«";
        Assert.True(RelPathMatcher.IsMatch(predicted, actual));
    }

    [Fact]
    public void IsMatch_LongSegments_DifferingBeforePrefixFloor_NoMatch()
    {
        // Two different long segments that diverge early (here at char 0).
        var predicted = "Yone/" + Long('x', 130);
        var actual = "Yone/" + Long('y', 130);
        Assert.False(RelPathMatcher.IsMatch(predicted, actual));
    }

    [Fact]
    public void IsMatch_LongSharedSeries_DistinguishesByTitleSequencePrefix()
    {
        var series = Long('s', 130);
        var predictedVol1 = $"Yone/{series}/1. - {Long('t', 130)}";
        var actualVol1 = $"Yone/{series[..123]}.«/1. - {Long('t', 123)}.«";
        var actualVol2 = $"Yone/{series[..123]}.«/2. - {Long('t', 123)}.«";
        Assert.True(RelPathMatcher.IsMatch(predictedVol1, actualVol1));
        Assert.False(RelPathMatcher.IsMatch(predictedVol1, actualVol2));
    }

    [Fact]
    public void Matches_ReturnsAllMatchingCandidates()
    {
        var predicted = "Yone/Series/Title";
        var candidates = new[] { "Other/X/Y", "Yone/Series/Title", "Yone/Series/Title" };
        Assert.Equal(2, RelPathMatcher.Matches(predicted, candidates).Count);
    }

    [Fact]
    public void Matches_NoCandidate_ReturnsEmpty()
    {
        var matches = RelPathMatcher.Matches("Yone/Series/Title", new[] { "A/B/C" });
        Assert.Empty(matches);
    }
}

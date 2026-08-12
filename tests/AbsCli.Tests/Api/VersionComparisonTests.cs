using AbsCli.Api;

namespace AbsCli.Tests.Api;

// Kept in the NLog collection: this class exercises version comparison, and any
// future test here that makes production code log must not run in parallel with
// the log-asserting tests. See PR #74.
[Collection("NLog")]
public class VersionComparisonTests
{
    [Theory]
    [InlineData("2.36.0", "2.36.0", 0)]
    [InlineData("2.36.1", "2.36.0", 1)]
    [InlineData("2.35.0", "2.36.0", -1)]
    [InlineData("2.36", "2.36.0", 0)]
    [InlineData("2.36.0.1", "2.36.0", 1)]
    public void CompareVersions_OrdersNumericVersions(string a, string b, int expected)
    {
        Assert.Equal(expected, Math.Sign(AbsApiClient.CompareVersions(a, b)));
    }

    [Theory]
    [InlineData("2.36.0-beta", "2.36.0", 0)]
    [InlineData("2.37.0-rc1", "2.36.0", 1)]
    [InlineData("v2.36.0", "2.36.0", 0)]
    [InlineData("V2.35.0", "2.36.0", -1)]
    [InlineData("nightly", "2.36.0", -1)]
    [InlineData("", "2.36.0", -1)]
    public void CompareVersions_TreatsNonNumericSegmentsAsZero(string a, string b, int expected)
    {
        Assert.Equal(expected, Math.Sign(AbsApiClient.CompareVersions(a, b)));
    }
}

using AbsCli.Api;

namespace AbsCli.Tests.Api;

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

    [Fact]
    public void CheckServerVersion_DoesNotThrow_OnNonNumericVersion()
    {
        AbsApiClient.CheckServerVersion("2.36.0-beta");
        AbsApiClient.CheckServerVersion("nightly");
    }
}

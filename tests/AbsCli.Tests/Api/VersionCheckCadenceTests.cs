using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class VersionCheckCadenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldCheckVersion_NeverChecked_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(null, Now));
    }

    [Fact]
    public void ShouldCheckVersion_InsideWindow_ReturnsFalse()
    {
        Assert.False(AbsApiClient.ShouldCheckVersion(Now.AddHours(-23), Now));
    }

    [Fact]
    public void ShouldCheckVersion_AtBoundary_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(-24), Now));
    }

    [Fact]
    public void ShouldCheckVersion_OutsideWindow_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(-25), Now));
    }

    [Fact]
    public void ShouldCheckVersion_TimestampInFuture_ReturnsTrue()
    {
        // Clock moved backwards — treat as stale rather than trusting it.
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(1), Now));
    }
}

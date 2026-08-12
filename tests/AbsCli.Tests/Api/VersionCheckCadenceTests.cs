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

    [Fact]
    public void VersionWarning_InRange_ReturnsNull()
    {
        Assert.Null(AbsApiClient.VersionWarning("2.36.0", previous: null));
    }

    [Fact]
    public void VersionWarning_AboveCeiling_NamesBothVersions()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: null);
        Assert.NotNull(warning);
        Assert.Contains("2.38.0", warning);
        Assert.Contains("2.36.0", warning);
        Assert.Contains("Check for a newer abs-cli", warning);
    }

    [Fact]
    public void VersionWarning_AboveCeilingAfterChange_NamesTheChange()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: "2.36.0");
        Assert.NotNull(warning);
        Assert.Contains("moved from ABS 2.36.0 to 2.38.0", warning);
    }

    [Fact]
    public void VersionWarning_SameVersionAsBefore_DoesNotClaimAChange()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: "2.38.0");
        Assert.NotNull(warning);
        Assert.DoesNotContain("moved from", warning);
    }

    [Fact]
    public void VersionWarning_BelowFloor_MentionsMinimum()
    {
        var warning = AbsApiClient.VersionWarning("2.30.0", previous: null);
        Assert.NotNull(warning);
        Assert.Contains("2.30.0", warning);
        Assert.Contains("older than the minimum supported version", warning);
    }

    [Fact]
    public void VersionWarning_NonNumericVersion_DoesNotThrow()
    {
        AbsApiClient.VersionWarning("2.36.0-beta", previous: null);
        AbsApiClient.VersionWarning("v2.36.0", previous: null);
        AbsApiClient.VersionWarning("nightly", previous: null);
    }
}

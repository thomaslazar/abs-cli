using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class TokenHelperTests
{
    // JWT with exp = 1775928439 (2026-04-11T17:07:19Z)
    private const string ExpiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJ0ZXN0IiwidHlwZSI6ImFjY2VzcyIsImlhdCI6MTc3NTkyNDgzOSwiZXhwIjoxNzc1OTI4NDM5fQ.fakesig";

    // JWT without exp field (legacy token)
    private const string NoExpToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJ0ZXN0IiwiaWF0IjoxNzc1OTI0ODAyfQ.fakesig";

    [Fact]
    public void GetExpiration_ReturnsExpTime_WhenPresent()
    {
        var exp = TokenHelper.GetExpiration(ExpiredToken);

        Assert.NotNull(exp);
        Assert.Equal(1775928439, exp.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void GetExpiration_ReturnsNull_WhenNoExp()
    {
        var exp = TokenHelper.GetExpiration(NoExpToken);

        Assert.Null(exp);
    }

    [Fact]
    public void IsExpiringSoon_ReturnsTrue_WhenWithinThreshold()
    {
        // Token expired in the past — definitely expiring soon
        var result = TokenHelper.IsExpiringSoon(ExpiredToken, thresholdSeconds: 60);

        Assert.True(result);
    }

    [Fact]
    public void IsExpiringSoon_ReturnsFalse_WhenNoExp()
    {
        var result = TokenHelper.IsExpiringSoon(NoExpToken, thresholdSeconds: 60);

        Assert.False(result);
    }

    [Fact]
    public void GetExpiration_ReturnsNull_ForGarbageInput()
    {
        var exp = TokenHelper.GetExpiration("not-a-jwt");

        Assert.Null(exp);
    }
}

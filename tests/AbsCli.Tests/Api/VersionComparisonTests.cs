using AbsCli.Api;
using AbsCli.Configuration;

namespace AbsCli.Tests.Api;

// Kept in the NLog collection: this class exercises version comparison, and any
// future test here that makes production code log must not run in parallel with
// the log-asserting tests. See PR #74.
[Collection("NLog")]
public class VersionComparisonTests
{
    [Fact]
    public void RecordServerVersion_UpdatesInMemoryConfigSoALaterSaveDoesNotRevertIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"abs-cli-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "config.json");
        var manager = new ConfigManager(path);
        manager.Save(new AppConfig { Server = "https://abs.example.com", AccessToken = "t" });
        var config = manager.Resolve(envLookup: _ => null);
        var client = new AbsApiClient(config, manager);

        client.RecordServerVersion("2.36.0");

        // In-memory copy must reflect the observed version...
        Assert.Equal("2.36.0", config.LastServerVersion);
        Assert.NotNull(config.LastVersionCheck);

        // ...so a later whole-object Save (what RefreshTokenAsync does) preserves it.
        manager.Save(config);
        var reloaded = manager.Load();
        Assert.Equal("2.36.0", reloaded.LastServerVersion);
        Assert.NotNull(reloaded.LastVersionCheck);
    }

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

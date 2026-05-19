using AbsCli.Api;
using AbsCli.Configuration;
using Xunit;

namespace AbsCli.Tests.Api;

[Collection("NLog")]
public class AbsApiClientUrlCompositionTests
{
    private static AbsApiClient BuildClient(string server)
    {
        var config = new AppConfig { Server = server, AccessToken = null, RefreshToken = null };
        var configManager = new ConfigManager(Path.GetTempFileName());
        return new AbsApiClient(config, configManager);
    }

    private static Uri Resolve(AbsApiClient client, string relativeEndpoint)
    {
        var httpField = typeof(AbsApiClient)
            .GetField("_http", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var http = (System.Net.Http.HttpClient)httpField.GetValue(client)!;
        return new Uri(http.BaseAddress!, relativeEndpoint);
    }

    [Fact]
    public void SubPathServer_NoTrailingSlash_PreservesPath()
    {
        var client = BuildClient("https://example.com/audiobookshelf");
        var resolved = Resolve(client, ApiEndpoints.Login);
        Assert.Equal("https://example.com/audiobookshelf/login", resolved.AbsoluteUri);
    }

    [Fact]
    public void SubPathServer_WithTrailingSlash_PreservesPath_NoDoubleSlash()
    {
        var client = BuildClient("https://example.com/audiobookshelf/");
        var resolved = Resolve(client, ApiEndpoints.Login);
        Assert.Equal("https://example.com/audiobookshelf/login", resolved.AbsoluteUri);
    }

    [Fact]
    public void RootDomainServer_NoPath_StillResolves()
    {
        var client = BuildClient("https://example.com");
        var resolved = Resolve(client, ApiEndpoints.Login);
        Assert.Equal("https://example.com/login", resolved.AbsoluteUri);
    }

    [Fact]
    public void SubPathServer_InterpolatedHelper_PreservesPath()
    {
        var client = BuildClient("https://example.com/audiobookshelf");
        var resolved = Resolve(client, ApiEndpoints.Item("li_abc"));
        Assert.Equal("https://example.com/audiobookshelf/api/items/li_abc", resolved.AbsoluteUri);
    }

    [Fact]
    public void SubPathServer_BatchConstant_PreservesPath()
    {
        var client = BuildClient("https://example.com/audiobookshelf");
        var resolved = Resolve(client, ApiEndpoints.ItemsBatchUpdate);
        Assert.Equal("https://example.com/audiobookshelf/api/items/batch/update", resolved.AbsoluteUri);
    }
}

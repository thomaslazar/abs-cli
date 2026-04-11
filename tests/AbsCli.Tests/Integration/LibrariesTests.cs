namespace AbsCli.Tests.Integration;

public class LibrariesTests : IntegrationTestBase
{
    [Fact(Skip = "Requires running ABS instance")]
    public async Task LibrariesList_ReturnsValidJson()
    {
        var env = new Dictionary<string, string>
        {
            ["ABS_SERVER"] = AbsUrl,
            ["ABS_TOKEN"] = await GetTestToken()
        };

        var (stdout, stderr, exitCode) = await RunCliAsync("libraries list", env: env);

        Assert.Equal(0, exitCode);
        var json = ParseJson(stdout);
        Assert.True(json.TryGetProperty("libraries", out var libraries));
        Assert.True(libraries.GetArrayLength() > 0);
    }

    private async Task<string> GetTestToken()
    {
        using var http = new HttpClient();
        var response = await http.PostAsync($"{AbsUrl}/login",
            new StringContent(
                """{"username":"root","password":"root"}""",
                System.Text.Encoding.UTF8, "application/json"));
        var json = await response.Content.ReadAsStringAsync();
        return ParseJson(json).GetProperty("user").GetProperty("token").GetString()!;
    }
}

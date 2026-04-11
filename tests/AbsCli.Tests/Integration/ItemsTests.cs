namespace AbsCli.Tests.Integration;

public class ItemsTests : IntegrationTestBase
{
    [Fact(Skip = "Requires running ABS instance")]
    public async Task ItemsList_ReturnsValidPaginatedJson()
    {
        var env = new Dictionary<string, string>
        {
            ["ABS_SERVER"] = AbsUrl,
            ["ABS_TOKEN"] = await GetTestToken(),
            ["ABS_LIBRARY"] = await GetFirstLibraryId()
        };

        var (stdout, stderr, exitCode) = await RunCliAsync("items list --limit 5", env: env);

        Assert.Equal(0, exitCode);
        var json = ParseJson(stdout);
        Assert.True(json.TryGetProperty("results", out _));
        Assert.True(json.TryGetProperty("total", out _));
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

    private async Task<string> GetFirstLibraryId()
    {
        var token = await GetTestToken();
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var response = await http.GetAsync($"{AbsUrl}/api/libraries");
        var json = await response.Content.ReadAsStringAsync();
        return ParseJson(json).GetProperty("libraries")[0].GetProperty("id").GetString()!;
    }
}

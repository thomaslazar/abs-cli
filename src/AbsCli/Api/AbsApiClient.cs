using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AbsCli.Configuration;
using AbsCli.Models;
using AbsCli.Output;

namespace AbsCli.Api;

public class AbsApiClient
{
    private readonly HttpClient _http;
    private readonly ConfigManager _configManager;
    private AppConfig _config;

    public AbsApiClient(AppConfig config, ConfigManager configManager)
    {
        _config = config;
        _configManager = configManager;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Server!.TrimEnd('/'))
        };

        if (config.AccessToken != null)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AccessToken);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { username, password }),
            Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.Login)
        {
            Content = content
        };
        request.Headers.Add("X-Return-Tokens", "true");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResponse>(json)!;
    }

    public async Task<string> GetAsync(string endpoint)
    {
        await EnsureValidTokenAsync();
        var response = await _http.GetAsync(endpoint);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> PatchAsync(string endpoint, string jsonBody)
    {
        await EnsureValidTokenAsync();
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync(endpoint, content);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Patch, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> PostAsync(string endpoint, string jsonBody)
    {
        await EnsureValidTokenAsync();
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task EnsureValidTokenAsync()
    {
        if (_config.AccessToken == null) return;

        if (TokenHelper.IsExpiringSoon(_config.AccessToken, thresholdSeconds: 60))
        {
            await RefreshTokenAsync();
        }
    }

    private async Task RefreshTokenAsync()
    {
        if (_config.RefreshToken == null)
        {
            ConsoleOutput.WriteError("Session expired. Run: abs-cli login");
            Environment.Exit(2);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.AuthRefresh);
        request.Headers.Add("X-Refresh-Token", _config.RefreshToken);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            ConsoleOutput.WriteError("Session expired. Run: abs-cli login");
            Environment.Exit(2);
        }

        var json = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(json)!;

        _config.AccessToken = loginResponse.User.AccessToken;
        _config.RefreshToken = loginResponse.User.RefreshToken;
        _configManager.Save(_config);

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AccessToken);
    }

    private async Task EnsureSuccessOrHandleAuthAsync(
        HttpResponseMessage response, HttpMethod method, string endpoint)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Try one refresh
            await RefreshTokenAsync();

            // Retry the original request
            var retryRequest = new HttpRequestMessage(method, endpoint);
            var retryResponse = await _http.SendAsync(retryRequest);

            if (!retryResponse.IsSuccessStatusCode)
            {
                ConsoleOutput.WriteError($"API request failed: {(int)retryResponse.StatusCode} {retryResponse.ReasonPhrase}");
                Environment.Exit(2);
            }
        }
        else if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            ConsoleOutput.WriteError($"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
            Environment.Exit(2);
        }
    }
}

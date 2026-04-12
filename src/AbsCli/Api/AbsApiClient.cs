using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("abs-cli/0.1.0");

        if (config.AccessToken != null)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AccessToken);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest, AppJsonContext.Default.LoginRequest),
            Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.Login)
        {
            Content = content
        };
        request.Headers.Add("X-Return-Tokens", "true");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.LoginResponse)!;
    }

    public async Task<string> GetAsync(string endpoint)
    {
        await EnsureValidTokenAsync();
        var response = await _http.GetAsync(endpoint);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> GetAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo)
    {
        var json = await GetAsync(endpoint);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PatchAsync(string endpoint, string jsonBody)
    {
        await EnsureValidTokenAsync();
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync(endpoint, content);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Patch, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PatchAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo)
    {
        var json = await PatchAsync(endpoint, jsonBody);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PostAsync(string endpoint, string jsonBody)
    {
        await EnsureValidTokenAsync();
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PostAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo)
    {
        var json = await PostAsync(endpoint, jsonBody);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
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
        var loginResponse = JsonSerializer.Deserialize(json, AppJsonContext.Default.LoginResponse)!;

        _config.AccessToken = loginResponse.User.AccessToken;
        _config.RefreshToken = loginResponse.User.RefreshToken;
        _configManager.Save(_config);

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AccessToken);
    }

    private static readonly string MinSupportedVersion = "2.33.1";
    private static readonly string MaxTestedVersion = "2.33.1";

    public static void CheckServerVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return;

        if (CompareVersions(version, MinSupportedVersion) < 0)
        {
            ConsoleOutput.WriteWarning(
                $"ABS server version {version} is older than the minimum supported version ({MinSupportedVersion}). Some features may not work.");
        }
        else if (CompareVersions(version, MaxTestedVersion) > 0)
        {
            ConsoleOutput.WriteWarning(
                $"ABS server version {version} has not been tested with this version of abs-cli. Proceed with caution.");
        }
    }

    private static int CompareVersions(string a, string b)
    {
        var aParts = a.Split('.').Select(int.Parse).ToArray();
        var bParts = b.Split('.').Select(int.Parse).ToArray();
        var len = Math.Max(aParts.Length, bParts.Length);
        for (int i = 0; i < len; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
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

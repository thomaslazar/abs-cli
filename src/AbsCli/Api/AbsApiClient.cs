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

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(100);

    public AbsApiClient(AppConfig config, ConfigManager configManager)
    {
        _config = config;
        _configManager = configManager;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Server!.TrimEnd('/')),
            // We manage timeouts per-request via CancellationTokenSource so that
            // long operations (backup create/apply/download/upload) can opt into
            // longer timeouts. Setting this to Infinite disables the global cap.
            Timeout = Timeout.InfiniteTimeSpan
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"abs-cli/{AssemblyVersion}");

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

    public async Task<string> GetAsync(string endpoint, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> GetAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        var json = await GetAsync(endpoint, permissionHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PatchAsync(string endpoint, string jsonBody, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Patch, endpoint, permissionHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PatchAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        var json = await PatchAsync(endpoint, jsonBody, permissionHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PostAsync(string endpoint, string jsonBody, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PostAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        var json = await PostAsync(endpoint, jsonBody, permissionHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> DeleteAsync(string endpoint, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.DeleteAsync(endpoint, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Delete, endpoint, permissionHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> DeleteAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        var json = await DeleteAsync(endpoint, permissionHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task PostMultipartAsync(string endpoint, MultipartFormDataContent content,
        string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint);
    }

    public async Task<T> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content,
        JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint);
        var json = await response.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task DownloadFileAsync(string endpoint, string outputPath, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream, cts.Token);
    }

    /// <summary>
    /// GET that returns the response body as a Stream the caller can consume
    /// (e.g. copy to a FileStream or to Console.OpenStandardOutput()). The
    /// caller MUST dispose the returned stream.
    /// </summary>
    public async Task<Stream> GetStreamAsync(string endpoint, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint);
        return await response.Content.ReadAsStreamAsync(cts.Token);
    }

    public async Task<string> PostEmptyAsync(string endpoint, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, null, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PostEmptyAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        var json = await PostEmptyAsync(endpoint, permissionHint, timeout);
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
    private static readonly string MaxTestedVersion = "2.33.2";

    private static readonly string AssemblyVersion =
        typeof(AbsApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

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
        HttpResponseMessage response, HttpMethod method, string endpoint,
        string? permissionHint = null)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await RefreshTokenAsync();
            var retryRequest = new HttpRequestMessage(method, endpoint);
            var retryResponse = await _http.SendAsync(retryRequest);
            if (!retryResponse.IsSuccessStatusCode)
            {
                ConsoleOutput.WriteError($"API request failed after token refresh: {(int)retryResponse.StatusCode} {retryResponse.ReasonPhrase}");
                Environment.Exit(2);
            }
        }
        else if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;
            var message = status switch
            {
                403 when permissionHint != null =>
                    $"Permission denied. This operation requires {permissionHint}.",
                403 => $"Permission denied.{(string.IsNullOrWhiteSpace(body) ? "" : $" {body.Trim()}")}",
                400 => $"Bad request.{(string.IsNullOrWhiteSpace(body) ? "" : $" {body.Trim()}")}",
                404 => $"Not found.{(string.IsNullOrWhiteSpace(body) ? "" : $" {body.Trim()}")}",
                _ => $"API request failed: {status} {response.ReasonPhrase}{(string.IsNullOrWhiteSpace(body) ? "" : $"\n{body.Trim()}")}"
            };
            ConsoleOutput.WriteError(message);
            Environment.Exit(2);
        }
    }
}

using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AbsCli.Configuration;
using AbsCli.Models;

namespace AbsCli.Api;

public class AbsApiClient
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly HttpClient _http;
    private readonly ConfigManager _configManager;
    private AppConfig _config;

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(100);

    public AbsApiClient(AppConfig config, ConfigManager configManager)
    {
        _config = config;
        _configManager = configManager;
        var debugHandler = new DebugHttpHandler(new HttpClientHandler());
        _http = new HttpClient(debugHandler)
        {
            BaseAddress = new Uri(config.Server!.TrimEnd('/') + "/"),
            // We manage timeouts per-request via CancellationTokenSource so that
            // long operations (backup create/apply/download/upload) can opt into
            // longer timeouts. Setting this to Infinite disables the global cap.
            Timeout = Timeout.InfiniteTimeSpan
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"abs-cli/{ClientVersion}");

        if (config.AccessToken != null)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AccessToken);
        _logger.Debug($"client base address: {_http.BaseAddress}");
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

    public async Task<string> GetAsync(string endpoint, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> GetAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        var json = await GetAsync(endpoint, permissionHint, notFoundHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PatchAsync(string endpoint, string jsonBody, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Patch, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PatchAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        var json = await PatchAsync(endpoint, jsonBody, permissionHint, notFoundHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> PostAsync(string endpoint, string jsonBody, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PostAsync<T>(string endpoint, string jsonBody, JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        var json = await PostAsync(endpoint, jsonBody, permissionHint, notFoundHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task<string> DeleteAsync(string endpoint, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.DeleteAsync(endpoint, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Delete, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> DeleteAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        var json = await DeleteAsync(endpoint, permissionHint, notFoundHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task PostMultipartAsync(string endpoint, MultipartFormDataContent content,
        string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint);
    }

    public async Task<T> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content,
        JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint);
        var json = await response.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    public async Task DownloadFileAsync(string endpoint, string outputPath, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint, notFoundHint);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream, cts.Token);
    }

    /// <summary>
    /// GET that returns the response body as a Stream the caller can consume
    /// (e.g. copy to a FileStream or to Console.OpenStandardOutput()). The
    /// caller MUST dispose the returned stream.
    /// </summary>
    public async Task<Stream> GetStreamAsync(string endpoint, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStreamAsync(cts.Token);
    }

    public async Task<string> PostEmptyAsync(string endpoint, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, null, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T> PostEmptyAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, string? permissionHint = null, string? notFoundHint = null, TimeSpan? timeout = null)
    {
        var json = await PostEmptyAsync(endpoint, permissionHint, notFoundHint, timeout);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    private async Task EnsureValidTokenAsync()
    {
        if (_config.AccessToken == null) return;

        var secondsLeft = TokenHelper.SecondsUntilExpiry(_config.AccessToken);
        if (TokenHelper.IsExpiringSoon(_config.AccessToken, thresholdSeconds: 60))
        {
            _logger.Debug($"access token expiring in {secondsLeft}s, refreshing");
            await RefreshTokenAsync();
        }
        else
        {
            _logger.Debug($"access token valid ({secondsLeft}s remaining)");
        }
    }

    private async Task RefreshTokenAsync()
    {
        if (_config.RefreshToken == null)
        {
            _logger.Error("Session expired. Run: abs-cli login");
            Environment.Exit(2);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.AuthRefresh);
        request.Headers.Add("X-Refresh-Token", _config.RefreshToken);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Debug($"token refresh failed: {(int)response.StatusCode}");
            _logger.Error("Session expired. Run: abs-cli login");
            Environment.Exit(2);
        }

        var json = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize(json, AppJsonContext.Default.LoginResponse)!;

        _config.AccessToken = loginResponse.User.AccessToken;
        _config.RefreshToken = loginResponse.User.RefreshToken;
        _configManager.Save(_config);

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AccessToken);
        _logger.Debug("token refresh succeeded");
    }

    private static readonly string MinSupportedVersion = "2.33.1";
    private static readonly string MaxTestedVersion = "2.36.0";

    internal static readonly TimeSpan VersionCheckInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether the server version is due for a re-check. A timestamp in the
    /// future means the clock moved backwards, which counts as stale.
    /// </summary>
    internal static bool ShouldCheckVersion(DateTimeOffset? lastCheck, DateTimeOffset now)
        => lastCheck is null
           || now - lastCheck.Value >= VersionCheckInterval
           || lastCheck.Value > now;

    // The informational version carries CI's build stamp ("1.0.2+pr-1.a1b2c3d") so
    // --version and server logs identify which build this is. It lives in an
    // assembly-level attribute, which Native AOT can trim — self-test asserts it
    // still resolves.
    internal static readonly string ClientVersion =
        typeof(AbsApiClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AbsApiClient).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>
    /// The warning to show for an observed server version, or null when it sits
    /// inside the tested range. Pure so the wording is unit-testable; the caller
    /// decides whether to log it. <paramref name="previous"/> is the last version
    /// this install saw, used to name the change when the server has moved.
    /// </summary>
    internal static string? VersionWarning(string observed, string? previous)
    {
        var moved = previous != null && previous != observed
            ? $"This server moved from ABS {previous} to {observed} since the last check. "
            : "";

        if (CompareVersions(observed, MinSupportedVersion) < 0)
        {
            return $"{moved}ABS server version {observed} is older than the minimum supported version ({MinSupportedVersion}). Some features may not work.";
        }
        if (CompareVersions(observed, MaxTestedVersion) > 0)
        {
            return $"{moved}abs-cli {ClientVersion} was tested up to ABS {MaxTestedVersion}; this server is {observed}. Check for a newer abs-cli.";
        }
        return null;
    }

    /// <summary>
    /// Compare two dotted version strings. Tolerant by design: a leading "v" is
    /// dropped and each segment contributes only its leading digits, so
    /// prerelease forms ("2.36.0-beta") and junk ("nightly") compare as 0 for
    /// that segment instead of throwing. This runs on the login path — an
    /// unparseable version must not take down an otherwise working command.
    /// </summary>
    internal static int CompareVersions(string a, string b)
    {
        var aParts = ParseVersion(a);
        var bParts = ParseVersion(b);
        var len = Math.Max(aParts.Length, bParts.Length);
        for (int i = 0; i < len; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }

    private static int[] ParseVersion(string version) =>
        version.TrimStart('v', 'V')
            .Split('.')
            .Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToArray();

    private async Task EnsureSuccessOrHandleAuthAsync(
        HttpResponseMessage response, HttpMethod method, string endpoint,
        string? permissionHint = null,
        string? notFoundHint = null)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await RefreshTokenAsync();
            var retryRequest = new HttpRequestMessage(method, endpoint);
            var retryResponse = await _http.SendAsync(retryRequest);
            if (!retryResponse.IsSuccessStatusCode)
            {
                _logger.Error($"API request failed after token refresh: {(int)retryResponse.StatusCode} {retryResponse.ReasonPhrase}");
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
                404 when notFoundHint != null => $"Not found. {notFoundHint}",
                404 => $"Not found.{(string.IsNullOrWhiteSpace(body) ? "" : $" {body.Trim()}")}",
                _ => $"API request failed: {status} {response.ReasonPhrase}{(string.IsNullOrWhiteSpace(body) ? "" : $"\n{body.Trim()}")}"
            };
            _logger.Error(message);
            Environment.Exit(2);
        }
    }
}

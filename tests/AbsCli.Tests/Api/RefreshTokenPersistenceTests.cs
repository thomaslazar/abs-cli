using System.Net;
using System.Net.Sockets;
using System.Text;
using AbsCli.Api;
using AbsCli.Configuration;
using Xunit;

namespace AbsCli.Tests.Api;

[Collection("NLog")]
public class RefreshTokenPersistenceTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _server;
    private readonly string _tempDir;

    public RefreshTokenPersistenceTests()
    {
        _server = $"http://127.0.0.1:{FreePort()}/";
        _listener.Prefixes.Add(_server);
        _listener.Start();
        _tempDir = Path.Combine(Path.GetTempPath(), $"abs-cli-refresh-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task ServeOneRefreshAsync()
    {
        var context = await _listener.GetContextAsync();
        var body = Encoding.UTF8.GetBytes(
            """{"user":{"id":"u1","username":"tester","accessToken":"new-access","refreshToken":"new-refresh"}}""");
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    [Fact]
    public async Task RefreshToken_PersistsRotatedTokens_WithoutWritingEnvValues()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = _server, AccessToken = "old-access", RefreshToken = "old-refresh" });
        // Resolve() merges env into memory. A refresh must persist only the rotated
        // tokens, never the env-supplied values that the operator kept out of the file.
        var resolved = manager.Resolve(envLookup: key => key == "ABS_LIBRARY" ? "env-lib" : null);
        Assert.Equal("env-lib", resolved.DefaultLibrary);

        var serving = ServeOneRefreshAsync();
        await new AbsApiClient(resolved, manager).RefreshTokenAsync();
        await serving;

        var onDisk = manager.Load();
        Assert.Equal("new-access", onDisk.AccessToken);
        Assert.Equal("new-refresh", onDisk.RefreshToken);
        Assert.Null(onDisk.DefaultLibrary);
        Assert.Equal(_server, onDisk.Server);
    }

    [Fact]
    public async Task RefreshToken_UpdatesInMemoryConfig_SoTheSameProcessUsesTheNewToken()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = _server, AccessToken = "old-access", RefreshToken = "old-refresh" });
        var config = manager.Resolve(envLookup: _ => null);

        var serving = ServeOneRefreshAsync();
        await new AbsApiClient(config, manager).RefreshTokenAsync();
        await serving;

        Assert.Equal("new-access", config.AccessToken);
        Assert.Equal("new-refresh", config.RefreshToken);
    }

    public void Dispose()
    {
        _listener.Close();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

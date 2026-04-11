using AbsCli.Configuration;

namespace AbsCli.Tests.Configuration;

public class ConfigManagerTests
{
    private readonly string _tempDir;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"abs-cli-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void LoadConfig_ReturnsEmpty_WhenNoConfigFile()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        var config = manager.Load();

        Assert.Null(config.Server);
        Assert.Null(config.AccessToken);
        Assert.Null(config.RefreshToken);
        Assert.Null(config.DefaultLibrary);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        var config = new AppConfig
        {
            Server = "https://example.com",
            AccessToken = "access123",
            RefreshToken = "refresh456",
            DefaultLibrary = "lib-id-1"
        };

        manager.Save(config);
        var loaded = manager.Load();

        Assert.Equal("https://example.com", loaded.Server);
        Assert.Equal("access123", loaded.AccessToken);
        Assert.Equal("refresh456", loaded.RefreshToken);
        Assert.Equal("lib-id-1", loaded.DefaultLibrary);
    }

    [Fact]
    public void Resolve_FlagsTakePrecedenceOverEnvOverConfig()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        manager.Save(new AppConfig
        {
            Server = "https://config.com",
            AccessToken = "config-token",
            DefaultLibrary = "config-lib"
        });

        var env = new Dictionary<string, string?>
        {
            ["ABS_SERVER"] = "https://env.com",
            ["ABS_TOKEN"] = "env-token",
            ["ABS_LIBRARY"] = "env-lib"
        };

        var resolved = manager.Resolve(
            flagServer: "https://flag.com",
            flagToken: null,
            flagLibrary: null,
            envLookup: key => env.GetValueOrDefault(key));

        Assert.Equal("https://flag.com", resolved.Server);
        Assert.Equal("env-token", resolved.AccessToken);
        Assert.Equal("env-lib", resolved.DefaultLibrary);
    }

    [Fact]
    public void Resolve_EnvTakesPrecedenceOverConfig()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        manager.Save(new AppConfig
        {
            Server = "https://config.com",
            AccessToken = "config-token",
            DefaultLibrary = "config-lib"
        });

        var env = new Dictionary<string, string?>
        {
            ["ABS_SERVER"] = "https://env.com"
        };

        var resolved = manager.Resolve(
            flagServer: null,
            flagToken: null,
            flagLibrary: null,
            envLookup: key => env.GetValueOrDefault(key));

        Assert.Equal("https://env.com", resolved.Server);
        Assert.Equal("config-token", resolved.AccessToken);
        Assert.Equal("config-lib", resolved.DefaultLibrary);
    }
}

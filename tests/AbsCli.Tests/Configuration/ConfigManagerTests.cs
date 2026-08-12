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

    [Fact]
    public void UpdateVersionCheck_WritesBothFields()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        manager.UpdateVersionCheck("2.38.0", checkedAt);
        var reloaded = manager.Load();
        Assert.Equal("2.38.0", reloaded.LastServerVersion);
        Assert.Equal(checkedAt, reloaded.LastVersionCheck);
    }

    [Fact]
    public void UpdateVersionCheck_PreservesExistingFields()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig
        {
            Server = "https://file.example.com",
            AccessToken = "file-token",
            RefreshToken = "file-refresh",
            DefaultLibrary = "lib-1"
        });
        manager.UpdateVersionCheck("2.38.0", DateTimeOffset.UtcNow);
        var reloaded = manager.Load();
        Assert.Equal("https://file.example.com", reloaded.Server);
        Assert.Equal("file-token", reloaded.AccessToken);
        Assert.Equal("file-refresh", reloaded.RefreshToken);
        Assert.Equal("lib-1", reloaded.DefaultLibrary);
    }

    [Fact]
    public void UpdateVersionCheck_DoesNotPersistEnvValues()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://file.example.com" });
        // Resolve() merges env into memory; UpdateVersionCheck must ignore that and
        // rewrite only what is on disk, so an env token never reaches the file.
        var resolved = manager.Resolve(envLookup: key => key switch
        {
            "ABS_TOKEN" => "env-secret",
            "ABS_SERVER" => "https://env.example.com",
            _ => null
        });
        Assert.Equal("env-secret", resolved.AccessToken);
        manager.UpdateVersionCheck("2.38.0", DateTimeOffset.UtcNow);
        var reloaded = manager.Load();
        Assert.Null(reloaded.AccessToken);
        Assert.Equal("https://file.example.com", reloaded.Server);
    }

    [Fact]
    public void Resolve_CarriesVersionCheckStateFromFile()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        manager.UpdateVersionCheck("2.36.0", checkedAt);
        var resolved = manager.Resolve(envLookup: _ => null);
        Assert.Equal("2.36.0", resolved.LastServerVersion);
        Assert.Equal(checkedAt, resolved.LastVersionCheck);
    }

    [Fact]
    public void Resolve_VersionCheckStateSurvivesResolveAndSave()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        manager.UpdateVersionCheck("2.36.0", checkedAt);
        var resolved = manager.Resolve(envLookup: _ => null);
        resolved.AccessToken = "new-token";
        resolved.RefreshToken = "new-refresh";
        manager.Save(resolved);
        var reloaded = manager.Load();
        Assert.Equal("2.36.0", reloaded.LastServerVersion);
        Assert.Equal(checkedAt, reloaded.LastVersionCheck);
        Assert.Equal("new-token", reloaded.AccessToken);
        Assert.Equal("new-refresh", reloaded.RefreshToken);
    }
}

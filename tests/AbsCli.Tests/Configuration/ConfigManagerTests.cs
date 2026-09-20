using System.Text.Json;
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
    public void Load_ThrowsActionableError_WhenFileIsNotJson()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(configPath, "not json{");
        var manager = new ConfigManager(configPath);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.Load());

        Assert.Contains(configPath, ex.Message);
        Assert.Contains("abs-cli login", ex.Message);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void Save_ReplacesAtomically_SoAnOpenReaderStillSeesWholeOldFile()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://old.example.com", RefreshToken = "old-refresh" });
        var before = File.ReadAllText(configPath);
        // A handle opened before the write must still read the complete old
        // document: an atomic replace swaps the directory entry, it never
        // truncates the bytes under someone else's handle.
        using var reader = new FileStream(
            configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        manager.Save(new AppConfig { Server = "https://new.example.com", RefreshToken = "new-refresh" });
        Assert.Equal(before, new StreamReader(reader).ReadToEnd());
        Assert.Equal("https://new.example.com", manager.Load().Server);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://example.com" });
        Assert.Equal(new[] { "config.json" }, Directory.GetFiles(_tempDir).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Save_PreservesRestrictiveFileMode()
    {
        if (OperatingSystem.IsWindows()) return;
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://example.com" });
        File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        manager.Save(new AppConfig { Server = "https://example.com", RefreshToken = "secret" });
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(configPath));
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
    public void UpdateTokens_DoesNotPersistEnvValues()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://file.example.com" });
        var resolved = manager.Resolve(envLookup: key => key switch
        {
            "ABS_TOKEN" => "env-secret",
            "ABS_LIBRARY" => "env-lib",
            _ => null
        });
        Assert.Equal("env-secret", resolved.AccessToken);
        manager.UpdateTokens("rotated-access", "rotated-refresh");
        var reloaded = manager.Load();
        Assert.Equal("rotated-access", reloaded.AccessToken);
        Assert.Equal("rotated-refresh", reloaded.RefreshToken);
        Assert.Null(reloaded.DefaultLibrary);
        Assert.Equal("https://file.example.com", reloaded.Server);
    }

    [Fact]
    public void UpdateTokens_PreservesVersionCheckState()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        manager.UpdateVersionCheck("2.36.0", checkedAt);
        manager.UpdateTokens("rotated-access", "rotated-refresh");
        var reloaded = manager.Load();
        Assert.Equal("2.36.0", reloaded.LastServerVersion);
        Assert.Equal(checkedAt, reloaded.LastVersionCheck);
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

    [Fact]
    public void Save_ConcurrentWriters_DoNotCorruptTheConfig()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        // Two symptoms of one defect, a staging path shared by every writer. Across
        // processes (#88) it tore the JSON: a short write over a long one left the
        // long document's tail behind. In-process the rename race fires first —
        // one thread's File.Move consumes the shared tmp out from under another,
        // throwing FileNotFoundException. The assertions below cover both: Load()
        // throws on a torn file, and Save() throws if the staging file vanishes.
        // Parallel.For(0, 16, ...) provokes less interleaving on CI's 2-4 vCPU
        // runners than on a dev box — a false-negative risk there, not a flake risk.
        var longConfig = new AppConfig
        {
            Server = "https://long.example.com",
            AccessToken = new string('a', 2000),
            RefreshToken = new string('r', 2000),
            DefaultLibrary = new string('l', 2000)
        };
        var shortConfig = new AppConfig { Server = "https://s.co" };
        for (int round = 0; round < 20; round++)
        {
            Parallel.For(0, 16, i => manager.Save(i % 2 == 0 ? longConfig : shortConfig));
            // Load() throws InvalidOperationException on a torn file.
            var loaded = manager.Load();
            if (loaded.Server == longConfig.Server)
                Assert.Equal(2000, loaded.AccessToken!.Length);
            else
                Assert.Equal(shortConfig.Server, loaded.Server);
        }
    }

    [Fact]
    public void Save_RemovesStagingFile_WhenTheRenameFails()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        // A directory sitting at the config path makes File.Move onto it fail,
        // exercising the catch block's cleanup without depending on timing.
        Directory.CreateDirectory(configPath);
        var manager = new ConfigManager(configPath);
        Assert.ThrowsAny<Exception>(() => manager.Save(new AppConfig { Server = "https://example.com" }));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public async Task UpdateVersionCheck_ConcurrentWithUpdateTokens_NeverRevertsTokens()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        // The read-modify-write hazard: a version-check writer that Load()s before a
        // token refresh lands and Save()s after it writes the stale tokens back over
        // the fresh ones. Undetected until the next refresh, which the server then
        // rejects (the superseded refresh token outlives its 10-minute grace window
        // long before the ~1h access token expires) — a forced re-login, #88's symptom.
        for (int round = 0; round < 30; round++)
        {
            manager.Save(new AppConfig
            {
                Server = "https://example.com",
                AccessToken = "old-access",
                RefreshToken = "old-refresh"
            });
            var start = new Barrier(5);
            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    start.SignalAndWait();
                    manager.UpdateTokens("new-access", "new-refresh");
                })
            };
            for (int t = 0; t < 4; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    start.SignalAndWait();
                    for (int i = 0; i < 5; i++)
                        manager.UpdateVersionCheck("2.36.0", DateTimeOffset.UtcNow);
                }));
            }
            await Task.WhenAll(tasks);
            var loaded = manager.Load();
            Assert.Equal("new-access", loaded.AccessToken);
            Assert.Equal("new-refresh", loaded.RefreshToken);
        }
    }

    [Fact]
    public async Task Update_ConcurrentWithUpdateTokens_NeverRevertsTokens()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        // The `config set server <url>` shape: it names one key but carries the
        // other five fields forward from its own Load(), so a token refresh landing
        // between that Load and the Save is reverted just as a version check would
        // revert it. Narrower window, identical breakage.
        for (int round = 0; round < 30; round++)
        {
            manager.Save(new AppConfig
            {
                Server = "https://old.example.com",
                AccessToken = "old-access",
                RefreshToken = "old-refresh"
            });
            var start = new Barrier(5);
            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    start.SignalAndWait();
                    manager.UpdateTokens("new-access", "new-refresh");
                })
            };
            for (int t = 0; t < 4; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    start.SignalAndWait();
                    for (int i = 0; i < 5; i++)
                        manager.Update(c => c.Server = "https://new.example.com");
                }));
            }
            await Task.WhenAll(tasks);
            var loaded = manager.Load();
            Assert.Equal("new-access", loaded.AccessToken);
            Assert.Equal("new-refresh", loaded.RefreshToken);
            Assert.Equal("https://new.example.com", loaded.Server);
        }
    }

    [Fact]
    public void UpdateVersionCheck_UnderHeavyContention_CompletesAndLeavesAReadableConfig()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://example.com", RefreshToken = "keep-me" });
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        // The lock must not deadlock or starve: every caller returns, and the config
        // still parses afterwards.
        Parallel.For(0, 32, i => manager.UpdateVersionCheck("2.36.0", checkedAt));
        var loaded = manager.Load();
        Assert.Equal("2.36.0", loaded.LastServerVersion);
        Assert.Equal(checkedAt, loaded.LastVersionCheck);
        Assert.Equal("https://example.com", loaded.Server);
        Assert.Equal("keep-me", loaded.RefreshToken);
    }

    [Fact]
    public void UpdateVersionCheck_LeavesTheLockFileInPlace()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.UpdateVersionCheck("2.36.0", DateTimeOffset.UtcNow);
        // The lock is the open handle, not the file's existence, so the empty file
        // is kept: deleting it on release would let a departing writer unlink the
        // inode a waiting writer is blocked on, and the next arrival would create a
        // fresh one and hold "the" lock at the same time. It holds no secrets.
        Assert.Equal(
            new[] { "config.json", "config.json.lock" },
            Directory.GetFiles(_tempDir).Select(Path.GetFileName).Order());
        if (!OperatingSystem.IsWindows())
        {
            // Owner-only like the rest of ~/.abs-cli, though it holds nothing.
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(configPath + ".lock"));
        }
        // Save (unlocked by design) still works beside it and still cleans up after
        // itself — the leftover lock file is inert.
        manager.Save(new AppConfig { Server = "https://example.com" });
        Assert.Equal("https://example.com", manager.Load().Server);
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public void Save_CreatesConfigOwnerOnly_OnFirstEverSave()
    {
        if (OperatingSystem.IsWindows()) return;
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://example.com", RefreshToken = "secret" });
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(configPath));
    }
}

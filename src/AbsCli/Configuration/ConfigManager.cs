using System.Text.Json;
using AbsCli.Models;

namespace AbsCli.Configuration;

public class ConfigManager
{
    private readonly string _configPath;

    public ConfigManager(string configPath)
    {
        _configPath = configPath;
    }

    public ConfigManager() : this(DefaultConfigPath()) { }

    public static string DefaultConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".abs-cli", "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        var json = File.ReadAllText(_configPath);
        try
        {
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            // Every command loads the config, so a raw parser message here is the
            // only thing the operator ever sees. Name the file and the way out.
            throw new InvalidOperationException(
                $"Config file is not valid JSON: {_configPath} — delete it and run 'abs-cli login'. ({ex.Message})",
                ex);
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(_configPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
        // Write-then-rename. A truncated config.json costs a re-login: it holds the
        // only copy of the refresh token, and the server rotates the old one away
        // as soon as it is used.
        var tmpPath = _configPath + ".tmp";
        File.WriteAllText(tmpPath, json);
        if (!OperatingSystem.IsWindows() && File.Exists(_configPath))
            File.SetUnixFileMode(tmpPath, File.GetUnixFileMode(_configPath));
        File.Move(tmpPath, _configPath, overwrite: true);
    }

    /// <summary>
    /// Persist the version-check state by rewriting only the on-disk config.
    /// Deliberately re-reads from disk instead of taking a resolved
    /// <see cref="AppConfig"/>: <see cref="Resolve"/> merges environment
    /// variables into memory, so saving that would write an ABS_TOKEN the
    /// operator kept out of the file.
    /// </summary>
    public void UpdateVersionCheck(string? serverVersion, DateTimeOffset checkedAt)
    {
        var onDisk = Load();
        onDisk.LastServerVersion = serverVersion;
        onDisk.LastVersionCheck = checkedAt;
        Save(onDisk);
    }

    /// <summary>
    /// Persist rotated tokens by rewriting only the on-disk config. Same reason
    /// as <see cref="UpdateVersionCheck"/>: saving a resolved <see cref="AppConfig"/>
    /// would write an ABS_TOKEN or ABS_LIBRARY the operator kept out of the file.
    /// </summary>
    public void UpdateTokens(string? accessToken, string? refreshToken)
    {
        var onDisk = Load();
        onDisk.AccessToken = accessToken;
        onDisk.RefreshToken = refreshToken;
        Save(onDisk);
    }

    public AppConfig Resolve(
        string? flagServer = null,
        string? flagToken = null,
        string? flagLibrary = null,
        Func<string, string?>? envLookup = null)
    {
        envLookup ??= Environment.GetEnvironmentVariable;
        var fileConfig = Load();

        return new AppConfig
        {
            Server = flagServer
                ?? envLookup("ABS_SERVER")
                ?? fileConfig.Server,
            AccessToken = flagToken
                ?? envLookup("ABS_TOKEN")
                ?? fileConfig.AccessToken,
            RefreshToken = fileConfig.RefreshToken,
            DefaultLibrary = flagLibrary
                ?? envLookup("ABS_LIBRARY")
                ?? fileConfig.DefaultLibrary,
            LastVersionCheck = fileConfig.LastVersionCheck,
            LastServerVersion = fileConfig.LastServerVersion
        };
    }
}

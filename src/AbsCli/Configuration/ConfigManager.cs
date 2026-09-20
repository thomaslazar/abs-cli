using System.Text.Json;
using AbsCli.Models;

namespace AbsCli.Configuration;

public class ConfigManager
{
    // ~2s total: long enough to outlast a competing read-modify-write, short
    // enough that giving up is invisible next to a CLI command's own runtime.
    private const int LockAttempts = 200;
    private const int LockRetryDelayMs = 10;
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
            // only thing the operator ever sees. Lead with repair, not deletion:
            // this file holds the only copy of the refresh token.
            throw new InvalidOperationException(
                $"Config file is not valid JSON: {_configPath} ({ex.Message}). " +
                "It holds the only copy of your refresh token — repair it if you can. " +
                "Otherwise delete it and run: abs-cli login.",
                ex);
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(_configPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
        // Write-then-rename through a staging file unique to this writer. A truncated
        // config.json costs a re-login: it holds the only copy of the refresh token.
        // The name must be unique — concurrent invocations sharing one .tmp truncate
        // and overwrite each other's bytes there, and the rename then publishes the
        // damage as a valid-looking replace (#88).
        var tmpPath = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Created owner-only from the first byte, not chmod'd after: a kill
            // between an open-world-readable create and a later chmod would leave
            // the refresh token exposed in the staging file.
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(tmpPath, options))
            using (var writer = new StreamWriter(stream))
                writer.Write(json);
            if (!OperatingSystem.IsWindows() && File.Exists(_configPath))
                File.SetUnixFileMode(tmpPath, File.GetUnixFileMode(_configPath));
            File.Move(tmpPath, _configPath, overwrite: true);
        }
        catch
        {
            // A killed process — not just a thrown exception — can still leave this
            // file behind; creating it owner-only above is what keeps that orphan
            // from being world-readable rather than what prevents it existing.
            try { File.Delete(tmpPath); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Read-modify-write the on-disk config under an exclusive lock.
    /// Deliberately re-reads from disk instead of taking a resolved
    /// <see cref="AppConfig"/>: <see cref="Resolve"/> merges environment
    /// variables into memory, so saving that would write an ABS_TOKEN or
    /// ABS_LIBRARY the operator kept out of the file. The lock spans
    /// Load -> mutate -> Save, so a writer holding a stale copy cannot put its
    /// tokens back over ones another process refreshed in between (#88).
    /// </summary>
    public void Update(Action<AppConfig> mutate)
    {
        using var configLock = TryAcquireLock();
        var onDisk = Load();
        mutate(onDisk);
        Save(onDisk);
    }

    /// <summary>Persist the version-check state. See <see cref="Update"/>.</summary>
    public void UpdateVersionCheck(string? serverVersion, DateTimeOffset checkedAt)
    {
        Update(onDisk =>
        {
            onDisk.LastServerVersion = serverVersion;
            onDisk.LastVersionCheck = checkedAt;
        });
    }

    /// <summary>Persist rotated tokens. See <see cref="Update"/>.</summary>
    public void UpdateTokens(string? accessToken, string? refreshToken)
    {
        Update(onDisk =>
        {
            onDisk.AccessToken = accessToken;
            onDisk.RefreshToken = refreshToken;
        });
    }

    /// <summary>
    /// Exclusive handle on &lt;config&gt;.lock, held for the caller's whole
    /// read-modify-write. Returns null if it cannot be taken in time: the caller
    /// then proceeds unlocked, because the race is rare and wedging a CLI command
    /// is the worse failure.
    /// </summary>
    private FileStream? TryAcquireLock()
    {
        var lockPath = _configPath + ".lock";
        var dir = Path.GetDirectoryName(lockPath);
        try
        {
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        // The lock is the open handle, not the file's existence: the OS drops it
        // when the holder dies, so a killed process cannot wedge every later run.
        // Deleting the file afterwards would break exactly that — a departing
        // writer would unlink the inode a waiting writer is about to block on, and
        // the next arrival would create a fresh one and hold "the" lock at the same
        // time. So the empty file stays; only Save's staging files are cleaned up.
        // FileAccess.Read, not ReadWrite: the file is never written, and asking for
        // write access would need write permission on a pre-existing lock file. One
        // `sudo abs-cli ...` leaves a root-owned one, and every later non-root run
        // would then fall back to unlocked forever. Measured on .NET 10: OpenOrCreate
        // still creates the file with Read access, and FileShare.None still excludes.
        var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.Read, Share = FileShare.None };
        // Owner-only like everything else in the config directory, though this file
        // is inert: zero bytes, never written, never read.
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        for (var attempt = 0; attempt < LockAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockPath, options);
            }
            catch (IOException)
            {
                // Sharing violation: someone else holds it. Bounded wait, then
                // give up and run unlocked rather than block the command.
                Thread.Sleep(LockRetryDelayMs);
            }
            catch (UnauthorizedAccessException) { return null; }
        }
        // ponytail: every fallback here returns null silently, so a degraded lock
        // looks exactly like a working one and #88 comes back with no diagnostic.
        // The fix if it ever bites is a debug hook (an Action<string>? the caller
        // wires to its logger); not worth a dependency on NLog for a rare path.
        return null;
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

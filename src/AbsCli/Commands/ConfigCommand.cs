using System.CommandLine;
using AbsCli.Configuration;
using AbsCli.Output;

namespace AbsCli.Commands;

public static class ConfigCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static Command Create()
    {
        var command = new Command("config", "Manage abs-cli configuration");
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateSetCommand());
        return command;
    }

    private static Command CreateGetCommand()
    {
        var command = new Command("get", "Show current configuration");
        command.AddExamples(
            "abs-cli config get");
        command.SetAction(parseResult =>
        {
            var configManager = new ConfigManager();
            var config = configManager.Load();
            var display = new Dictionary<string, string>
            {
                ["server"] = config.Server ?? "(not set)",
                ["accessToken"] = config.AccessToken != null ? "***" : "(not set)",
                ["refreshToken"] = config.RefreshToken != null ? "***" : "(not set)",
                ["defaultLibrary"] = config.DefaultLibrary ?? "(not set)",
                ["lastVersionCheck"] = config.LastVersionCheck?.ToString("u") ?? "(never)",
                ["lastServerVersion"] = config.LastServerVersion ?? "(unknown)",
                ["configPath"] = ConfigManager.DefaultConfigPath()
            };
            ConsoleOutput.WriteJson(display);
            return 0;
        });
        return command;
    }

    private static Command CreateSetCommand()
    {
        var keyArg = new Argument<string>("key") { Description = "Configuration key (server, defaultLibrary)" };
        var valueArg = new Argument<string>("value") { Description = "Configuration value" };
        var command = new Command("set", "Set a configuration value")
        {
            keyArg,
            valueArg
        };
        command.AddExamples(
            "abs-cli config set server https://abs.example.com",
            "abs-cli config set defaultLibrary \"lib_abc123\"");
        command.SetAction(parseResult =>
        {
            var key = parseResult.GetValue(keyArg)!;
            var value = parseResult.GetValue(valueArg)!;
            var configManager = new ConfigManager();
            // Validate against a real Load() first: Update has no way to abort its
            // Save, and an unknown key must not rewrite the file. Loading here also
            // keeps a corrupt config reporting itself ahead of a bad key, as before.
            var error = ApplyConfigSet(configManager.Load(), key, value);
            if (error != null)
            {
                _logger.Error(error);
                Environment.Exit(1);
                return 1;
            }
            // Update, not Load/Save: this names one key but carries the other five
            // fields forward, so an unlocked round trip reverts tokens a concurrent
            // refresh just wrote (#88).
            configManager.Update(config => ApplyConfigSet(config, key, value));
            Console.Error.WriteLine($"Set {key} = {value}");
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Applies a config key/value onto <paramref name="config"/>. Returns null
    /// on success, otherwise the error message for an unknown key.
    /// </summary>
    internal static string? ApplyConfigSet(AppConfig config, string key, string value)
    {
        switch (key)
        {
            case "server":
                config.Server = value;
                return null;
            case "defaultLibrary":
                config.DefaultLibrary = value;
                return null;
            default:
                return $"Unknown config key: '{key}'. Valid keys: server, defaultLibrary";
        }
    }
}

using System.CommandLine;
using AbsCli.Configuration;
using AbsCli.Output;

namespace AbsCli.Commands;

public static class ConfigCommand
{
    public static Command Create()
    {
        var command = new Command("config", "Manage abs-cli configuration");
        command.AddCommand(CreateGetCommand());
        command.AddCommand(CreateSetCommand());
        return command;
    }

    private static Command CreateGetCommand()
    {
        var command = new Command("get", "Show current configuration");
        command.AddExamples(
            "abs-cli config get",
            "abs-cli config get | jq '.server'");

        command.SetHandler(() =>
        {
            var configManager = new ConfigManager();
            var config = configManager.Load();

            var display = new Dictionary<string, string>
            {
                ["server"] = config.Server ?? "(not set)",
                ["accessToken"] = config.AccessToken != null ? "***" : "(not set)",
                ["refreshToken"] = config.RefreshToken != null ? "***" : "(not set)",
                ["defaultLibrary"] = config.DefaultLibrary ?? "(not set)",
                ["configPath"] = ConfigManager.DefaultConfigPath()
            };

            ConsoleOutput.WriteJson(display);
        });

        return command;
    }

    private static Command CreateSetCommand()
    {
        var keyArg = new Argument<string>("key", "Configuration key (server, defaultLibrary)");
        var valueArg = new Argument<string>("value", "Configuration value");

        var command = new Command("set", "Set a configuration value")
        {
            keyArg,
            valueArg
        };
        command.AddExamples(
            "abs-cli config set server https://abs.example.com",
            "abs-cli config set defaultLibrary \"lib_abc123\"");

        command.SetHandler((string key, string value) =>
        {
            var configManager = new ConfigManager();
            var config = configManager.Load();

            switch (key)
            {
                case "server":
                    config.Server = value;
                    break;
                case "defaultLibrary":
                    config.DefaultLibrary = value;
                    break;
                default:
                    ConsoleOutput.WriteError($"Unknown config key: '{key}'. Valid keys: server, defaultLibrary");
                    Environment.Exit(1);
                    return;
            }

            configManager.Save(config);
            Console.Error.WriteLine($"Set {key} = {value}");
        }, keyArg, valueArg);

        return command;
    }
}

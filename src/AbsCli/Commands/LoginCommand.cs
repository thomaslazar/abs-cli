using System.CommandLine;
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Configuration;
using AbsCli.Models;
using AbsCli.Output;

namespace AbsCli.Commands;

public static class LoginCommand
{
    public static Command Create()
    {
        var serverOption = new Option<string?>(
            "--server",
            "Audiobookshelf server URL");

        var command = new Command("login", "Authenticate with an Audiobookshelf server")
        {
            serverOption
        };

        command.SetHandler(async (string? server) =>
        {
            var configManager = new ConfigManager();

            if (server == null)
            {
                Console.Error.Write("Server URL: ");
                server = Console.ReadLine()?.Trim();
            }

            if (string.IsNullOrEmpty(server))
            {
                ConsoleOutput.WriteError("Server URL is required.");
                Environment.Exit(1);
            }

            Console.Error.Write("Username: ");
            var username = Console.ReadLine()?.Trim();

            Console.Error.Write("Password: ");
            var password = ReadPassword();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ConsoleOutput.WriteError("Username and password are required.");
                Environment.Exit(1);
            }

            var tempConfig = new AppConfig { Server = server };
            var client = new AbsApiClient(tempConfig, configManager);

            try
            {
                var loginResponse = await client.LoginAsync(username, password);

                var config = configManager.Load();
                config.Server = server;
                config.AccessToken = loginResponse.User.AccessToken;
                config.RefreshToken = loginResponse.User.RefreshToken;

                if (config.DefaultLibrary == null && loginResponse.UserDefaultLibraryId != null)
                    config.DefaultLibrary = loginResponse.UserDefaultLibraryId;

                configManager.Save(config);

                var version = loginResponse.ServerSettings?.Version ?? "unknown";
                Console.Error.WriteLine($"Logged in as {loginResponse.User.Username} to {server} (ABS {version})");

                if (config.DefaultLibrary != null)
                    Console.Error.WriteLine($"Default library: {config.DefaultLibrary}");
                else
                    Console.Error.WriteLine("No default library set. Use: abs-cli config set default-library <id|name>");
            }
            catch (HttpRequestException ex)
            {
                ConsoleOutput.WriteError($"Login failed: {ex.Message}");
                Environment.Exit(2);
            }
        }, serverOption);

        return command;
    }

    private static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Remove(password.Length - 1, 1);
            else if (key.Key != ConsoleKey.Backspace)
                password.Append(key.KeyChar);
        }
        Console.Error.WriteLine();
        return password.ToString();
    }
}

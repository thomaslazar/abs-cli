using System.CommandLine;
using AbsCli.Api;
using AbsCli.Configuration;

namespace AbsCli.Commands;

public static class LoginCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static Command Create()
    {
        var serverOption = new Option<string?>("--server")
        {
            Description = "Audiobookshelf server URL"
        };
        var usernameOption = new Option<string?>("--username") { Description = "Username (prompts if omitted)" };
        var passwordOption = new Option<string?>("--password") { Description = "Password — visible in process list / shell history; prefer --password-stdin" };
        var passwordStdinOption = new Option<bool>("--password-stdin") { Description = "Read the password from the first line of stdin" };
        var command = new Command("login", "Authenticate with an Audiobookshelf server")
        {
            serverOption, usernameOption, passwordOption, passwordStdinOption
        };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "--password is visible in the process list and shell history. Prefer",
            "--password-stdin (reads the first line of stdin) for scripted use.",
            "Any credential not supplied via flag is prompted for interactively",
            "(username plain, password hidden).");
        command.AddExamples(
            "abs-cli login --server https://abs.example.com",
            "abs-cli login --server https://abs.example.com --username agent --password-stdin <<<\"$ABS_PW\"",
            "abs-cli login --server https://abs.example.com --username agent --password \"$ABS_PW\"");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var server = parseResult.GetValue(serverOption);
            var configManager = new ConfigManager();
            if (server == null)
            {
                Console.Error.Write("Server URL: ");
                server = Console.ReadLine()?.Trim();
            }
            if (string.IsNullOrEmpty(server))
            {
                _logger.Error("Server URL is required.");
                Environment.Exit(1);
            }
            var usernameFlag = parseResult.GetValue(usernameOption);
            var passwordFlag = parseResult.GetValue(passwordOption);
            var passwordStdin = parseResult.GetValue(passwordStdinOption);
            if (passwordFlag != null && passwordStdin)
            {
                _logger.Error("Provide --password or --password-stdin, not both.");
                Environment.Exit(1);
            }
            var username = usernameFlag;
            if (string.IsNullOrEmpty(username))
            {
                Console.Error.Write("Username: ");
                username = Console.ReadLine()?.Trim();
            }
            string? password;
            if (passwordFlag != null)
            {
                password = passwordFlag;
            }
            else if (passwordStdin)
            {
                password = ReadPasswordFromStdin(Console.In);
                if (string.IsNullOrEmpty(password))
                {
                    _logger.Error("No password on stdin.");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.Error.Write("Password: ");
                password = ReadPassword();
            }
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.Error("Username and password are required.");
                Environment.Exit(1);
            }
            var tempConfig = new AppConfig { Server = server };
            var client = new AbsApiClient(tempConfig, configManager);
            try
            {
                var loginResponse = await client.LoginAsync(username!, password!);
                var config = configManager.Load();
                // Server changed — drop the previous defaultLibrary so we don't
                // carry a stale ID that doesn't exist on the new server.
                var serverChanged = !string.Equals(config.Server, server, StringComparison.Ordinal);
                if (serverChanged)
                    config.DefaultLibrary = null;
                config.Server = server;
                config.AccessToken = loginResponse.User.AccessToken;
                config.RefreshToken = loginResponse.User.RefreshToken;
                if (config.DefaultLibrary == null && loginResponse.UserDefaultLibraryId != null)
                    config.DefaultLibrary = loginResponse.UserDefaultLibraryId;
                configManager.Save(config);
                AbsApiClient.CheckServerVersion(loginResponse.ServerSettings?.Version);
                var version = loginResponse.ServerSettings?.Version ?? "unknown";
                Console.Error.WriteLine($"Logged in as {loginResponse.User.Username} to {server} (ABS {version})");
                if (config.DefaultLibrary != null)
                    Console.Error.WriteLine($"Default library: {config.DefaultLibrary}");
                else
                    Console.Error.WriteLine("No default library set. Use: abs-cli config set defaultLibrary <id>");
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Login failed: {ex.Message}");
                Environment.Exit(2);
            }
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Read a password from stdin: the first line, stripped of a single
    /// trailing CRLF/LF. Returns "" if stdin is empty. A password with an
    /// embedded newline is not supportable via this path.
    /// </summary>
    internal static string ReadPasswordFromStdin(TextReader reader)
    {
        var line = reader.ReadLine();
        return line ?? "";
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

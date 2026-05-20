using AbsCli.Api;
using AbsCli.Configuration;

namespace AbsCli.Commands;

public static class CommandHelper
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static (AbsApiClient client, AppConfig config) BuildClient(
        string? serverOverride = null, string? tokenOverride = null,
        string? libraryOverride = null)
    {
        var configManager = new ConfigManager();
        var config = configManager.Resolve(
            flagServer: serverOverride,
            flagToken: tokenOverride,
            flagLibrary: libraryOverride);

        if (string.IsNullOrEmpty(config.Server))
        {
            _logger.Error("No server configured. Run: abs-cli login");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(config.AccessToken))
        {
            _logger.Error("Not authenticated. Run: abs-cli login");
            Environment.Exit(1);
        }

        return (new AbsApiClient(config, configManager), config);
    }

    public static string RequireLibrary(AppConfig config)
    {
        if (string.IsNullOrEmpty(config.DefaultLibrary))
        {
            _logger.Error(
                "No library specified. Use --library <id> or set a default with: abs-cli config set defaultLibrary <id>");
            Environment.Exit(1);
        }
        return config.DefaultLibrary!;
    }

    public static string ReadJsonInput(string input)
    {
        if (File.Exists(input))
            return File.ReadAllText(input);
        return input;
    }
}

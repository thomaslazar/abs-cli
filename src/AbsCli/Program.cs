using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Output;

var _logger = NLog.LogManager.GetLogger("AbsCli.Program");

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

var debugOption = new Option<bool>("--debug")
{
    Description = "Enable debug-level logging (HTTP requests, token refresh, version check) to stderr."
};
var logJsonOption = new Option<bool>("--log-json")
{
    Description = "Emit stderr log lines as single-line JSON instead of text."
};
rootCommand.Options.Add(debugOption);
rootCommand.Options.Add(logJsonOption);

rootCommand.Subcommands.Add(LoginCommand.Create());
rootCommand.Subcommands.Add(ConfigCommand.Create());
rootCommand.Subcommands.Add(LibrariesCommand.Create());
rootCommand.Subcommands.Add(ItemsCommand.Create());
rootCommand.Subcommands.Add(SeriesCommand.Create());
rootCommand.Subcommands.Add(AuthorsCommand.Create());
rootCommand.Subcommands.Add(SearchCommand.Create());
rootCommand.Subcommands.Add(BackupCommand.Create());
rootCommand.Subcommands.Add(CacheCommand.Create());
rootCommand.Subcommands.Add(UploadCommand.Create());
rootCommand.Subcommands.Add(TasksCommand.Create());
rootCommand.Subcommands.Add(MetadataCommand.Create());
rootCommand.Subcommands.Add(SelfTestCommand.Create());
rootCommand.Subcommands.Add(ChangelogCommand.Create());

rootCommand.AddHelpSection("Environment variables",
    "ABS_DEBUG=1   Same as --debug. Enables debug-level logging to stderr.");

rootCommand.UseCustomHelpSections();

var parseResult = rootCommand.Parse(args);
var debugEnabled = parseResult.GetValue(debugOption)
                   || Environment.GetEnvironmentVariable("ABS_DEBUG") == "1";
var logJson = parseResult.GetValue(logJsonOption);
LogSetup.Configure(debugEnabled, logJson);

try
{
    return await parseResult.InvokeAsync();
}
catch (Exception ex)
{
    _logger.Error(ex.Message);
    _logger.Debug(ex.ToString());
    return 2;
}

using System.CommandLine;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.AddCommand(LoginCommand.Create());
rootCommand.AddCommand(ConfigCommand.Create());
rootCommand.AddCommand(LibrariesCommand.Create());
rootCommand.AddCommand(ItemsCommand.Create());
rootCommand.AddCommand(SeriesCommand.Create());

return await rootCommand.InvokeAsync(args);

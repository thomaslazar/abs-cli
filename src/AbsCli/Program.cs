using System.CommandLine;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.AddCommand(LoginCommand.Create());
rootCommand.AddCommand(ConfigCommand.Create());
rootCommand.AddCommand(LibrariesCommand.Create());
rootCommand.AddCommand(ItemsCommand.Create());
rootCommand.AddCommand(SeriesCommand.Create());
rootCommand.AddCommand(AuthorsCommand.Create());
rootCommand.AddCommand(SearchCommand.Create());
rootCommand.AddCommand(SelfTestCommand.Create());

return await rootCommand.InvokeAsync(args);

using System.CommandLine;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.AddCommand(LoginCommand.Create());

return await rootCommand.InvokeAsync(args);

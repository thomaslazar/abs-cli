using System.CommandLine;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");
rootCommand.SetHandler(() =>
{
    Console.Error.WriteLine("Use --help to see available commands.");
});

return await rootCommand.InvokeAsync(args);

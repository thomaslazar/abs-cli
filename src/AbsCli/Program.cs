using System.CommandLine;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.Subcommands.Add(LoginCommand.Create());
rootCommand.Subcommands.Add(ConfigCommand.Create());
rootCommand.Subcommands.Add(LibrariesCommand.Create());
rootCommand.Subcommands.Add(ItemsCommand.Create());
rootCommand.Subcommands.Add(SeriesCommand.Create());
rootCommand.Subcommands.Add(AuthorsCommand.Create());
rootCommand.Subcommands.Add(SearchCommand.Create());
rootCommand.Subcommands.Add(BackupCommand.Create());
rootCommand.Subcommands.Add(UploadCommand.Create());
rootCommand.Subcommands.Add(TasksCommand.Create());
rootCommand.Subcommands.Add(MetadataCommand.Create());
rootCommand.Subcommands.Add(SelfTestCommand.Create());
rootCommand.Subcommands.Add(ChangelogCommand.Create());

rootCommand.UseCustomHelpSections();

return await rootCommand.Parse(args).InvokeAsync();

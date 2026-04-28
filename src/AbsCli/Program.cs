using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.AddCommand(LoginCommand.Create());
rootCommand.AddCommand(ConfigCommand.Create());
rootCommand.AddCommand(LibrariesCommand.Create());
rootCommand.AddCommand(ItemsCommand.Create());
rootCommand.AddCommand(SeriesCommand.Create());
rootCommand.AddCommand(AuthorsCommand.Create());
rootCommand.AddCommand(SearchCommand.Create());
rootCommand.AddCommand(BackupCommand.Create());
rootCommand.AddCommand(UploadCommand.Create());
rootCommand.AddCommand(TasksCommand.Create());
rootCommand.AddCommand(MetadataCommand.Create());
rootCommand.AddCommand(SelfTestCommand.Create());
rootCommand.AddCommand(ChangelogCommand.Create());

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseCustomHelpSections()
    .Build();

return await parser.InvokeAsync(args);

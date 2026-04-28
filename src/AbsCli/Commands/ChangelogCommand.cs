using System.CommandLine;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class ChangelogCommand
{
    public static Command Create()
    {
        var allOption = new Option<bool>(
            "--all",
            "Print the entire CHANGELOG.md instead of just the latest entry");
        var command = new Command(
            "changelog",
            "Print release notes from the bundled CHANGELOG.md") { allOption };
        command.AddExamples(
            "abs-cli changelog",
            "abs-cli changelog --all");
        command.SetHandler((context) =>
        {
            var all = context.ParseResult.GetValueForOption(allOption);
            var output = all ? ChangelogReader.ReadAll() : ChangelogReader.ReadLatest();
            context.Console.Out.Write(output + "\n");
        });
        return command;
    }
}

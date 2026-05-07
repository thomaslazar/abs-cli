using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class SeriesCommand
{
    public static Command Create()
    {
        var command = new Command("series", "Manage series");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Series are derived from book metadata. The scanner removes orphaned",
            "series on its next run. To remove a series, retag the books that",
            "reference it.",
            "",
            "'series list' and 'series get' return series entities only. To list",
            "books in a series:",
            "  abs-cli items list --filter \"series=<series-id>\" --sort sequence");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID or name" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50, pass higher value to retrieve more)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var command = new Command("list",
            "List series in a library (defaults to 50 results)")
        { libraryOption, limitOption, pageOption };
        command.AddExamples(
            "abs-cli series list",
            "abs-cli series list --limit 10 --page 0");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(SeriesItem));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SeriesService(client);
            var result = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Series ID", Required = true };
        var command = new Command("get", "Get a single series") { idOption };
        command.AddExamples(
            "abs-cli series get --id \"se_abc123\"");
        command.AddResponseExample<SeriesItem>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SeriesItem);
            return 0;
        });
        return command;
    }
}

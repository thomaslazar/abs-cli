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
            "Series are derived from book metadata. A series exists while at least one",
            "library item references it. When the last referencing item is removed or",
            "re-tagged, the scanner deletes the series on its next run. To remove a",
            "series, update the books that reference it.");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var limitOption = new Option<int>("--limit", () => 50, "Results per page (default 50, pass higher value to retrieve more)");
        var pageOption = new Option<int?>("--page", "Page number (0-indexed)");

        var command = new Command("list",
            "List series in a library (defaults to 50 results)")
        { libraryOption, limitOption, pageOption };
        command.AddExamples(
            "abs-cli series list",
            "abs-cli series list --limit 10 --page 0",
            "abs-cli series list --limit 100 | jq '.results[].name'");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(SeriesItem));

        command.SetHandler(async (string? library, int limit, int? page) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SeriesService(client);
            var result = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
        }, libraryOption, limitOption, pageOption);

        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id", "Series ID") { IsRequired = true };
        var command = new Command("get", "Get a single series") { idOption };
        command.AddExamples(
            "abs-cli series get --id \"se_abc123\"",
            "abs-cli series get --id \"se_abc123\" | jq '.name'");
        command.AddResponseExample<SeriesItem>();

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SeriesItem);
        }, idOption);

        return command;
    }
}

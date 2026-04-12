using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class SearchCommand
{
    public static Command Create()
    {
        var queryOption = new Option<string>("--query", "Search text") { IsRequired = true };
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var limitOption = new Option<int?>("--limit", "Max results");

        var command = new Command("search", "Search across a library")
        {
            queryOption, libraryOption, limitOption
        };
        command.AddExamples(
            "abs-cli search --query \"Brandon Sanderson\"",
            "abs-cli search --query \"Mistborn\" --limit 5",
            "abs-cli search --query \"Fantasy\" | jq '.book[].libraryItem.media.metadata.title'");

        command.SetHandler(async (string query, string? library, int? limit) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SearchService(client);
            var json = await service.SearchAsync(libraryId, query, limit);
            ConsoleOutput.WriteRawJson(json);
        }, queryOption, libraryOption, limitOption);

        return command;
    }
}

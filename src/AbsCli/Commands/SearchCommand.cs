using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class SearchCommand
{
    public static Command Create()
    {
        var queryOption = new Option<string>("--query") { Description = "Search text", Required = true };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID or name" };
        var limitOption = new Option<int>("--limit") { Description = "Max results (default 50, pass higher value to retrieve more)", DefaultValueFactory = _ => 50 };
        var command = new Command("search", "Search across a library (defaults to 50 results)")
        {
            queryOption, libraryOption, limitOption
        };
        command.AddHelpSection("Search behavior",
            "Substring match, case-insensitive. No operators or wildcards.",
            "Multi-word queries match as a single phrase (\"brandon sanderson\"",
            "matches but \"sanderson brandon\" does not).",
            "Accent-insensitive when the server supports it.");
        command.AddHelpSection("Fields searched",
            "Books: title, subtitle, ASIN, ISBN",
            "Authors: name",
            "Series: name",
            "Narrators: name",
            "Tags: name",
            "Genres: name",
            "NOT searched: description, publisher");
        command.AddExamples(
            "abs-cli search --query \"Brandon Sanderson\"",
            "abs-cli search --query \"Mistborn\" --limit 5",
            "abs-cli search --query \"978-0\" --limit 20    # search by ISBN prefix",
            "abs-cli search --query \"Fantasy\" | jq '.book[].libraryItem.media.metadata.title'");
        command.AddResponseExample<SearchResult>();
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var query = parseResult.GetValue(queryOption)!;
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SearchService(client);
            var result = await service.SearchAsync(libraryId, query, limit);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SearchResult);
            return 0;
        });
        return command;
    }
}

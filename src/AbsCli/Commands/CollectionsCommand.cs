using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class CollectionsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static Command Create()
    {
        var command = new Command("collections", "Manage collections (curated lists of book library items)");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Collections are flat, manually-curated, library-scoped ordered lists",
            "of book library items. ABS has no smart-collection / saved-filter",
            "concept — membership is yours to maintain. See `collections create",
            "--help` for sharp edges; `update` edits metadata, `reorder` shuffles",
            "order, `add` / `remove` / `batch-*` change membership.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var includeOption = new Option<string?>("--include") { Description = "Comma-separated include flags (only 'rssfeed' is honoured today)" };
        var command = new Command("list", "List collections in a library (paginated)")
        { libraryOption, limitOption, pageOption, includeOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "ABS echoes `sortBy` / `sortDesc` / `filterBy` / `minified` back in",
            "the response payload, but these reflect no applied behavior today",
            "(server-side TODO). Treat them as inert.");
        command.AddExamples(
            "abs-cli collections list",
            "abs-cli collections list --limit 100 --page 0",
            "abs-cli collections list --include rssfeed");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(Collection));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var include = parseResult.GetValue(includeOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new CollectionsService(client);
            var result = await service.ListAsync(libraryId, limit, page, include);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var includeOption = new Option<string?>("--include") { Description = "Comma-separated include flags (only 'rssfeed' is honoured today)" };
        var command = new Command("get", "Get a single collection (expanded)")
        { idOption, includeOption };
        command.AddExamples(
            "abs-cli collections get --id \"col_abc\"",
            "abs-cli collections get --id \"col_abc\" --include rssfeed");
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var include = parseResult.GetValue(includeOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.GetAsync(id, include);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body honouring tri-state semantics: null = field
    /// absent (omit from JSON), empty string = clear (send JSON null),
    /// non-empty = set value. Exposed internally for unit testing.
    /// Mirrors <c>AuthorsCommand.BuildUpdateBodyForTesting</c>.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBodyForTesting(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description == "" ? null! : description;
        return body;
    }
}

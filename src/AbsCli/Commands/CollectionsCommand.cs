using System.CommandLine;
using System.Text.Json;
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
        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateUpdateCommand());
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

    private static Command CreateCreateCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var nameOption = new Option<string>("--name") { Description = "Collection name", Required = true };
        var descriptionOption = new Option<string?>("--description") { Description = "Optional description" };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("create", "Create a collection (requires at least one book)")
        { libraryOption, nameOption, descriptionOption, inputOption, stdinOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "ABS requires at least one book; cannot create empty.",
            "HTML in --name is stripped silently server-side.");
        command.AddExamples(
            "abs-cli collections create --library \"lib_1\" --name \"Light Novels\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli collections create --name \"My set\" --stdin");
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var name = parseResult.GetValue(nameOption)!;
            var description = parseResult.GetValue(descriptionOption);
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);

            string booksJson;
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            if (stdin) booksJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) booksJson = CommandHelper.ReadJsonInput(input);
            else
            {
                _logger.Error("Provide --input <file> or --stdin.");
                Environment.Exit(1);
                return 1;
            }

            List<string> books;
            try
            {
                var parsed = JsonSerializer.Deserialize(booksJson, AppJsonContext.Default.CollectionBooksRequest);
                books = parsed?.Books ?? new List<string>();
            }
            catch (JsonException ex)
            {
                _logger.Error($"Invalid JSON input: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }

            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new CollectionsService(client);
            var result = await service.CreateAsync(libraryId, name, description, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (empty string is rejected)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description; empty string clears the field" };
        var command = new Command("update", "Edit a collection's name and/or description")
        { idOption, nameOption, descriptionOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Edits metadata only. Use `reorder` to change book order; use `add`",
            "/ `remove` / `batch-add` / `batch-remove` to change membership.",
            "",
            "Empty string for --description clears the field; omit to leave",
            "unchanged. Empty --name is rejected. Same convention as `authors",
            "update`.");
        command.AddExamples(
            "abs-cli collections update --id \"col_abc\" --name \"Renamed\"",
            "abs-cli collections update --id \"col_abc\" --description \"\"");
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
                return 1;
            }
            var body = BuildUpdateBodyForTesting(name, description);
            if (body.Count == 0)
            {
                _logger.Error("Specify at least one of --name, --description");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.UpdateAsync(id, body);
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

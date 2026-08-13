using System.CommandLine;
using System.Text.Json;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class CollectionsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parses a {"books":[...]} body and returns the entries ABS itself
    /// would keep — non-null, non-empty strings (CollectionController
    /// filters with `.filter((b) => !!b && typeof b == 'string')` in
    /// create/addBatch/removeBatch).
    /// </summary>
    private static List<string> ParseValidBooks(string jsonBody)
    {
        var parsed = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.BooksRequest);
        return (parsed?.Books ?? new List<string>()).Where(b => !string.IsNullOrEmpty(b)).ToList();
    }

    /// <summary>
    /// Validates the books array supplied to `create` and returns the
    /// filtered entries. ABS 400s "Invalid collection data. No books" when
    /// no valid string id survives the filter (CollectionController.js:44-50)
    /// — the plan's "books optional" is wrong; it is required here.
    /// </summary>
    internal static List<string> PrepareCreateBooks(string jsonBody)
    {
        var books = ParseValidBooks(jsonBody);
        if (books.Count == 0)
            throw new ArgumentException("create requires at least one book id in \"books\"");
        return books;
    }

    /// <summary>
    /// Validates a reorder body and returns it unchanged. ABS applies no
    /// non-empty requirement here — CollectionController.update (the
    /// endpoint reorder actually calls) treats an empty/absent "books" as a
    /// no-op, not an error (CollectionController.js:168-170) — so only
    /// structural JSON validity is checked.
    /// </summary>
    internal static string PrepareReorderBody(string jsonBody)
    {
        JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.BooksRequest);
        return jsonBody;
    }

    /// <summary>
    /// Validates a batch-add body and returns it unchanged. ABS requires at
    /// least one valid string book id after filtering, 400 "Invalid request
    /// body" otherwise (CollectionController.addBatch, js:320-323).
    /// </summary>
    internal static string PrepareBatchAddBody(string jsonBody)
    {
        if (ParseValidBooks(jsonBody).Count == 0)
            throw new ArgumentException("batch-add requires at least one book id in \"books\"");
        return jsonBody;
    }

    /// <summary>
    /// Validates a batch-remove body and returns it unchanged. Same
    /// non-empty-after-filter requirement as batch-add
    /// (CollectionController.removeBatch, js:381-384 — that path actually
    /// 500s rather than 400s on empty, but the requirement is the same).
    /// </summary>
    internal static string PrepareBatchRemoveBody(string jsonBody)
    {
        if (ParseValidBooks(jsonBody).Count == 0)
            throw new ArgumentException("batch-remove requires at least one book id in \"books\"");
        return jsonBody;
    }

    public static Command Create()
    {
        var command = new Command("collections", "Manage collections (curated lists of book library items)");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Collections are flat, manually-curated, library-scoped ordered lists",
            "of book library items. ABS has no smart-collection / saved-filter",
            "concept — membership is yours to maintain.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateReorderCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateAddCommand());
        command.Subcommands.Add(CreateRemoveCommand());
        command.Subcommands.Add(CreateBatchAddCommand());
        command.Subcommands.Add(CreateBatchRemoveCommand());
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
        var inputOption = new Option<string?>("--input") { Description = "JSON file with the request body (see --help-full)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from stdin" };
        var command = new Command("create", "Create a collection (requires at least one book)")
        { libraryOption, nameOption, descriptionOption, inputOption, stdinOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "HTML in --name is stripped silently server-side.");
        command.AddExamples(
            "abs-cli collections create --library \"lib_1\" --name \"Light Novels\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli collections create --name \"My set\" --stdin");
        command.AddRequestExample<CollectionCreateRequest>();
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var name = parseResult.GetValue(nameOption)!;
            var description = parseResult.GetValue(descriptionOption);
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            if (string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
                return 1;
            }

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
                books = PrepareCreateBooks(booksJson);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
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
            var body = BuildUpdateBody(name, description);
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

    private static Command CreateReorderCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with the request body (see --help-full)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from stdin" };
        var command = new Command("reorder", "Reorder existing books in a collection")
        { idOption, inputOption, stdinOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Pass the FULL current membership in the desired order; partial",
            "lists shuffle missing members to undefined positions.");
        command.AddExamples(
            "abs-cli collections reorder --id \"col_abc\" --input order.json",
            "echo '{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}' | abs-cli collections reorder --id \"col_abc\" --stdin");
        command.AddRequestExample<BooksRequest>();
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string booksJson;
            if (stdin && input != null) { _logger.Error("Provide --input or --stdin, not both."); Environment.Exit(1); return 1; }
            if (stdin) booksJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) booksJson = CommandHelper.ReadJsonInput(input);
            else { _logger.Error("Provide --input <file> or --stdin."); Environment.Exit(1); return 1; }
            string validated;
            try
            {
                validated = PrepareReorderBody(booksJson);
            }
            catch (JsonException ex)
            {
                _logger.Error($"Invalid JSON input: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.ReorderAsync(id, validated);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var command = new Command("delete", "Delete a collection") { idOption };
        command.AddPermissionRequired("delete");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Hard delete — no undo.");
        command.AddExamples(
            "abs-cli collections delete --id \"col_abc\"");
        command.AddShapeSection("Response shape",
            "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to add", Required = true };
        var command = new Command("add", "Add a single book to a collection")
        { idOption, bookOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Errors with 400 if the book is already in the collection, or",
            "if it is from a different library.");
        command.AddExamples(
            "abs-cli collections add --id \"col_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.AddBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to remove", Required = true };
        var command = new Command("remove", "Remove a single book from a collection")
        { idOption, bookOption };
        command.AddPermissionRequired("update");
        command.AddExamples(
            "abs-cli collections remove --id \"col_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.RemoveBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with the request body (see --help-full)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from stdin" };
        var command = new Command("batch-add", "Add multiple books to a collection")
        { idOption, inputOption, stdinOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Silently skips books already in the collection. Books from a",
            "different library are rejected.");
        command.AddExamples(
            "abs-cli collections batch-add --id \"col_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli collections batch-add --id \"col_abc\" --stdin");
        command.AddRequestExample<BooksRequest>();
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string booksJson;
            if (stdin && input != null) { _logger.Error("Provide --input or --stdin, not both."); Environment.Exit(1); return 1; }
            if (stdin) booksJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) booksJson = CommandHelper.ReadJsonInput(input);
            else { _logger.Error("Provide --input <file> or --stdin."); Environment.Exit(1); return 1; }
            string validated;
            try
            {
                validated = PrepareBatchAddBody(booksJson);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                _logger.Error($"Invalid JSON input: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.BatchAddAsync(id, validated);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Collection ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with the request body (see --help-full)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from stdin" };
        var command = new Command("batch-remove", "Remove multiple books from a collection")
        { idOption, inputOption, stdinOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Tolerates books not in the collection (no-op for those).");
        command.AddExamples(
            "abs-cli collections batch-remove --id \"col_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli collections batch-remove --id \"col_abc\" --stdin");
        command.AddRequestExample<BooksRequest>();
        command.AddResponseExample<Collection>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string booksJson;
            if (stdin && input != null) { _logger.Error("Provide --input or --stdin, not both."); Environment.Exit(1); return 1; }
            if (stdin) booksJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) booksJson = CommandHelper.ReadJsonInput(input);
            else { _logger.Error("Provide --input <file> or --stdin."); Environment.Exit(1); return 1; }
            string validated;
            try
            {
                validated = PrepareBatchRemoveBody(booksJson);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                _logger.Error($"Invalid JSON input: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CollectionsService(client);
            var result = await service.BatchRemoveAsync(id, validated);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Collection);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body honouring tri-state semantics: null = field
    /// absent (omit from JSON), empty string = clear (send JSON null),
    /// non-empty = set value. Exposed internally for unit testing.
    /// Mirrors <c>AuthorsCommand.BuildUpdateBody</c>.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBody(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description == "" ? null! : description;
        return body;
    }
}

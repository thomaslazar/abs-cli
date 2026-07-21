using System.CommandLine;
using System.Text.Json;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class PlaylistsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static Command Create()
    {
        var command = new Command("playlists", "Manage playlists (your personal ordered lists of book library items)");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Per-user, library-scoped ordered lists of book library items,",
            "private to you. Books only; podcast episodes are not supported.");
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
        command.Subcommands.Add(CreateFromCollectionCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var command = new Command("list", "List your playlists in a library")
        { libraryOption, limitOption, pageOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Lists only your own playlists. --library falls back to the",
            "configured defaultLibrary.");
        command.AddExamples(
            "abs-cli playlists list --library \"lib_1\"",
            "abs-cli playlists list --library \"lib_1\" --limit 20 --page 0");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new PlaylistsService(client);
            var result = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var command = new Command("get", "Get a single playlist (expanded)") { idOption };
        command.AddExamples("abs-cli playlists get --id \"pl_abc\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateCreateCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var nameOption = new Option<string>("--name") { Description = "Playlist name", Required = true };
        var descriptionOption = new Option<string?>("--description") { Description = "Optional description" };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("create", "Create a playlist")
        { libraryOption, nameOption, descriptionOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Books are optional — omit --input/--stdin to create an",
            "empty playlist. --library falls back to the configured",
            "defaultLibrary. Input shape: `{\"books\":[\"lid\",...]}`.");
        command.AddExamples(
            "abs-cli playlists create --library \"lib_1\" --name \"Roadtrip\"",
            "abs-cli playlists create --library \"lib_1\" --name \"Roadtrip\" --input books.json");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var name = parseResult.GetValue(nameOption)!;
            var description = parseResult.GetValue(descriptionOption);
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            List<string> books;
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            if (stdin || input != null)
            {
                var booksJson = stdin
                    ? await Console.In.ReadToEndAsync(cancellationToken)
                    : CommandHelper.ReadJsonInput(input!);
                if (!TryParseBooks(booksJson, out books)) { Environment.Exit(1); return 1; }
            }
            else
            {
                books = new List<string>();
            }
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new PlaylistsService(client);
            var result = await service.CreateAsync(libraryId, name, description, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (empty string is rejected)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description (cannot be cleared — empty string is ignored by ABS)" };
        var command = new Command("update", "Edit a playlist's name and/or description")
        { idOption, nameOption, descriptionOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Empty --name is rejected. An empty --description does not clear",
            "the field — ABS ignores it, so a description cannot be removed.");
        command.AddExamples(
            "abs-cli playlists update --id \"pl_abc\" --name \"Renamed\"",
            "abs-cli playlists update --id \"pl_abc\" --description \"New notes\"");
        command.AddResponseExample<Playlist>();
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
            var service = new PlaylistsService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateReorderCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("reorder", "Reorder existing items in a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Pass the FULL current membership in the desired order; a",
            "non-empty list whose length differs from the playlist's is",
            "rejected with 400 (an empty list is a no-op).",
            "",
            "Example for a 3-item playlist: `{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}`",
            "moves li_c to position 1.");
        command.AddExamples(
            "abs-cli playlists reorder --id \"pl_abc\" --input order.json",
            "echo '{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}' | abs-cli playlists reorder --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var books = await ReadBooksAsync(parseResult, inputOption, stdinOption, cancellationToken);
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.ReorderAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var command = new Command("delete", "Delete a playlist") { idOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Hard delete. No confirmation prompt.");
        command.AddExamples("abs-cli playlists delete --id \"pl_abc\"");
        command.AddShapeSection("Response shape", "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to add", Required = true };
        var command = new Command("add", "Add a single book to a playlist")
        { idOption, bookOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "The book must be in the same library as the playlist, and not",
            "already in it (both 400).");
        command.AddExamples("abs-cli playlists add --id \"pl_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.AddBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to remove", Required = true };
        var command = new Command("remove", "Remove a single book from a playlist")
        { idOption, bookOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removing the last item deletes the playlist.");
        command.AddExamples("abs-cli playlists remove --id \"pl_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.RemoveBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("batch-add", "Add multiple books to a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Silently skips books already in the playlist. Books must be in",
            "the same library as the playlist.");
        command.AddExamples(
            "abs-cli playlists batch-add --id \"pl_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli playlists batch-add --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var books = await ReadBooksAsync(parseResult, inputOption, stdinOption, cancellationToken);
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.BatchAddAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("batch-remove", "Remove multiple books from a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Tolerates books not in the playlist (no-op for those). Removing",
            "the playlist's last item deletes the playlist.");
        command.AddExamples(
            "abs-cli playlists batch-remove --id \"pl_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli playlists batch-remove --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var books = await ReadBooksAsync(parseResult, inputOption, stdinOption, cancellationToken);
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.BatchRemoveAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateFromCollectionCommand()
    {
        var collectionOption = new Option<string>("--collection") { Description = "Source collection ID", Required = true };
        var command = new Command("create-from-collection", "Create a playlist from a collection")
        { collectionOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Copies the collection's name, description, and books into a new",
            "playlist. A one-time snapshot — later changes to the collection",
            "do not propagate. 400 if the collection has no books.");
        command.AddExamples("abs-cli playlists create-from-collection --collection \"col_abc\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var collectionId = parseResult.GetValue(collectionOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.CreateFromCollectionAsync(collectionId);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Resolve --input/--stdin into a parsed books list. Returns null (after
    /// logging) on argument-usage error or invalid JSON; the caller then exits 1.
    /// </summary>
    private static async Task<List<string>?> ReadBooksAsync(
        System.CommandLine.ParseResult parseResult,
        Option<string?> inputOption,
        Option<bool> stdinOption,
        CancellationToken cancellationToken)
    {
        var input = parseResult.GetValue(inputOption);
        var stdin = parseResult.GetValue(stdinOption);
        if (stdin && input != null)
        {
            _logger.Error("Provide --input or --stdin, not both.");
            return null;
        }
        if (!stdin && input == null)
        {
            _logger.Error("Provide --input <file> or --stdin.");
            return null;
        }
        var booksJson = stdin
            ? await Console.In.ReadToEndAsync(cancellationToken)
            : CommandHelper.ReadJsonInput(input!);
        return TryParseBooks(booksJson, out var books) ? books : null;
    }

    private static bool TryParseBooks(string booksJson, out List<string> books)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize(booksJson, AppJsonContext.Default.CollectionBooksRequest);
            books = parsed?.Books ?? new List<string>();
            return true;
        }
        catch (JsonException ex)
        {
            _logger.Error($"Invalid JSON input: {ex.Message}");
            books = new List<string>();
            return false;
        }
    }

    /// <summary>
    /// Builds the PATCH body. Unlike <c>CollectionsCommand.BuildUpdateBody</c>,
    /// an empty description is passed through (ABS ignores it) rather than
    /// clearing the field — playlists have no clear-description semantics.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBody(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (name is not null) body["name"] = name;
        if (description is not null) body["description"] = description;
        return body;
    }
}

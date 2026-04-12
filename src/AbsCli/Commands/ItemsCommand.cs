using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class ItemsCommand
{
    public static Command Create()
    {
        var command = new Command("items", "Manage library items");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        command.AddCommand(CreateSearchCommand());
        command.AddCommand(CreateUpdateCommand());
        command.AddCommand(CreateBatchUpdateCommand());
        command.AddCommand(CreateBatchGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var filterOption = new Option<string?>("--filter", "Filter expression (e.g. 'genres=Sci Fi')");
        var sortOption = new Option<string?>("--sort", "Sort field (e.g. 'media.metadata.title')");
        var descOption = new Option<bool>("--desc", "Sort descending");
        var limitOption = new Option<int?>("--limit", "Results per page");
        var pageOption = new Option<int?>("--page", "Page number (0-indexed)");

        var command = new Command("list", """
            List library items with optional filtering, sorting, and pagination

            Filter groups:
              authors, genres, tags, series, narrators, languages, progress, issues

            Sort fields:
              media.metadata.title          — title (default)
              media.metadata.authorName     — author name (first last)
              media.metadata.authorNameLF   — author name (last, first)
              media.metadata.publishedYear  — publication year
              media.duration                — total duration
              addedAt                       — date added to library
              size                          — file size
              birthtimeMs                   — file creation time
              mtimeMs                       — file modification time
              random                        — random order
            """)
        {
            libraryOption, filterOption, sortOption, descOption, limitOption, pageOption
        };
        command.AddExamples(
            "abs-cli items list",
            "abs-cli items list --filter \"languages=English\" --sort \"media.metadata.title\"",
            "abs-cli items list --filter \"genres=Fantasy\" --desc",
            "abs-cli items list --sort \"addedAt\" --desc --limit 10",
            "abs-cli items list | jq '.results[] | select(.media.metadata.isbn == null)'");

        command.SetHandler(async (string? library, string? filter, string? sort,
            bool desc, int? limit, int? page) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var json = await service.ListAsync(libraryId, filter, sort, desc, limit, page);
            ConsoleOutput.WriteRawJson(json);
        }, libraryOption, filterOption, sortOption, descOption, limitOption, pageOption);

        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id", "Item ID") { IsRequired = true };
        var command = new Command("get", "Get a single library item by ID") { idOption };
        command.AddExamples(
            "abs-cli items get --id \"li_abc123\"",
            "abs-cli items get --id \"li_abc123\" | jq '.media.metadata'");

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var json = await service.GetAsync(id);
            ConsoleOutput.WriteRawJson(json);
        }, idOption);

        return command;
    }

    private static Command CreateSearchCommand()
    {
        var queryOption = new Option<string>("--query", "Search text") { IsRequired = true };
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var limitOption = new Option<int?>("--limit", "Max results");

        var command = new Command("search", "Search items in a library")
        {
            queryOption, libraryOption, limitOption
        };
        command.AddExamples(
            "abs-cli items search --query \"Brandon Sanderson\"",
            "abs-cli items search --query \"Mistborn\" --limit 5");

        command.SetHandler(async (string query, string? library, int? limit) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var json = await service.SearchAsync(libraryId, query, limit);
            ConsoleOutput.WriteRawJson(json);
        }, queryOption, libraryOption, limitOption);

        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id", "Item ID") { IsRequired = true };
        var inputOption = new Option<string>("--input", "JSON input (string or file path)") { IsRequired = true };

        var command = new Command("update", "Update a single item's metadata") { idOption, inputOption };
        command.AddExamples(
            "abs-cli items update --id \"li_abc123\" --input '{\"metadata\":{\"title\":\"New Title\"}}'",
            "abs-cli items update --id \"li_abc123\" --input payload.json",
            "abs-cli items update --id \"li_abc123\" --input '{\"metadata\":{\"genres\":[\"Fantasy\",\"Epic\"]}}'");

        command.SetHandler(async (string id, string input) =>
        {
            var jsonBody = CommandHelper.ReadJsonInput(input);
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var json = await service.UpdateMediaAsync(id, jsonBody);
            ConsoleOutput.WriteRawJson(json);
        }, idOption, inputOption);

        return command;
    }

    private static Command CreateBatchUpdateCommand()
    {
        var inputOption = new Option<string?>("--input", "JSON file path");
        var stdinOption = new Option<bool>("--stdin", "Read JSON from stdin");

        var command = new Command("batch-update", "Batch update multiple items") { inputOption, stdinOption };
        command.AddExamples(
            "abs-cli items batch-update --input updates.json",
            "cat updates.json | abs-cli items batch-update --stdin");

        command.SetHandler(async (string? input, bool stdin) =>
        {
            string jsonBody;
            if (stdin) jsonBody = await Console.In.ReadToEndAsync();
            else if (input != null) jsonBody = CommandHelper.ReadJsonInput(input);
            else
            {
                ConsoleOutput.WriteError("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return;
            }

            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var json = await service.BatchUpdateAsync(jsonBody);
            ConsoleOutput.WriteRawJson(json);
        }, inputOption, stdinOption);

        return command;
    }

    private static Command CreateBatchGetCommand()
    {
        var inputOption = new Option<string?>("--input", "JSON file with libraryItemIds");
        var stdinOption = new Option<bool>("--stdin", "Read JSON from stdin");

        var command = new Command("batch-get", "Batch get multiple items by ID") { inputOption, stdinOption };
        command.AddExamples(
            "abs-cli items batch-get --input ids.json",
            "echo '{\"libraryItemIds\":[\"li_abc\",\"li_def\"]}' | abs-cli items batch-get --stdin");

        command.SetHandler(async (string? input, bool stdin) =>
        {
            string jsonBody;
            if (stdin) jsonBody = await Console.In.ReadToEndAsync();
            else if (input != null) jsonBody = CommandHelper.ReadJsonInput(input);
            else
            {
                ConsoleOutput.WriteError("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return;
            }

            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var json = await service.BatchGetAsync(jsonBody);
            ConsoleOutput.WriteRawJson(json);
        }, inputOption, stdinOption);

        return command;
    }
}

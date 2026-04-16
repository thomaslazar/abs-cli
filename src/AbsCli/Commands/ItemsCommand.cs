using System.CommandLine;
using AbsCli.Models;
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
        command.AddCommand(CreateScanCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var filterOption = new Option<string?>("--filter", "Filter expression (e.g. 'genres=Sci Fi')");
        var sortOption = new Option<string?>("--sort", "Sort field (e.g. 'media.metadata.title')");
        var descOption = new Option<bool>("--desc", "Sort descending");
        var limitOption = new Option<int>("--limit", () => 50, "Results per page (default 50, pass higher value to retrieve more)");
        var pageOption = new Option<int?>("--page", "Page number (0-indexed)");

        var command = new Command("list",
            "List library items with optional filtering, sorting, and pagination (defaults to 50 results)")
        {
            libraryOption, filterOption, sortOption, descOption, limitOption, pageOption
        };
        command.AddHelpSection("Filter groups",
            "authors, genres, tags, series, narrators, publishers, publishedDecades,",
            "missing, languages, progress, tracks, ebooks, issues",
            "",
            "The 'missing' group finds items with empty fields:",
            "  --filter \"missing=language\"   — items without a language set",
            "  --filter \"missing=cover\"      — items without cover art",
            "  Valid: asin, isbn, subtitle, publishedYear, description,",
            "         publisher, language, cover, genres, tags, narrators,",
            "         chapters, authors, series");
        command.AddHelpSection("Sort fields",
            "media.metadata.title          — title (default)",
            "media.metadata.authorName     — author name (first last)",
            "media.metadata.authorNameLF   — author name (last, first)",
            "media.metadata.publishedYear  — publication year",
            "media.duration                — total duration",
            "addedAt                       — date added to library",
            "size                          — file size",
            "birthtimeMs                   — file creation time",
            "mtimeMs                       — file modification time",
            "random                        — random order");
        command.AddExamples(
            "abs-cli items list",
            "abs-cli items list --filter \"languages=English\" --sort \"media.metadata.title\"",
            "abs-cli items list --filter \"genres=Fantasy\" --desc",
            "abs-cli items list --sort \"addedAt\" --desc --limit 10",
            "abs-cli items list | jq '.results[] | select(.media.metadata.isbn == null)'");

        command.SetHandler(async (string? library, string? filter, string? sort,
            bool desc, int limit, int? page) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var result = await service.ListAsync(libraryId, filter, sort, desc, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
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
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryItemMinified);
        }, idOption);

        return command;
    }

    private static Command CreateSearchCommand()
    {
        var queryOption = new Option<string>("--query", "Search text") { IsRequired = true };
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var limitOption = new Option<int>("--limit", () => 50, "Max results (default 50, pass higher value to retrieve more)");

        var command = new Command("search",
            "Search items in a library (substring match, case-insensitive, searches title/subtitle/ASIN/ISBN, defaults to 50 results)")
        {
            queryOption, libraryOption, limitOption
        };
        command.AddExamples(
            "abs-cli items search --query \"Mistborn\"",
            "abs-cli items search --query \"978-0\" --limit 20");

        command.SetHandler(async (string query, string? library, int limit) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var result = await service.SearchAsync(libraryId, query, limit);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SearchResult);
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
            var result = await service.UpdateMediaAsync(id, jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.UpdateMediaResponse);
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
            var result = await service.BatchUpdateAsync(jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BatchUpdateResponse);
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
            var result = await service.BatchGetAsync(jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BatchGetResponse);
        }, inputOption, stdinOption);

        return command;
    }

    private static Command CreateScanCommand()
    {
        var idOption = new Option<string>("--id", "Item ID") { IsRequired = true };
        var command = new Command("scan", "Scan a single library item (admin-only, sync)") { idOption };
        command.AddExamples(
            "abs-cli items scan --id \"li_abc123\"",
            "abs-cli items scan --id \"li_abc123\" | jq '.result'");
        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.ScanAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.ScanResult);
        }, idOption);
        return command;
    }
}

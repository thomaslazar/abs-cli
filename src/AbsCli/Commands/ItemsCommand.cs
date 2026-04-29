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
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateSearchCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateBatchUpdateCommand());
        command.Subcommands.Add(CreateBatchGetCommand());
        command.Subcommands.Add(CreateScanCommand());
        command.Subcommands.Add(CreateCoverCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID or name" };
        var filterOption = new Option<string?>("--filter") { Description = "Filter expression (e.g. 'genres=Sci Fi')" };
        var sortOption = new Option<string?>("--sort") { Description = "Sort field (e.g. 'media.metadata.title')" };
        var descOption = new Option<bool>("--desc") { Description = "Sort descending" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50, pass higher value to retrieve more)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
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
            "abs-cli items list --filter \"series=se_abc123\" --sort sequence",
            "abs-cli items list --sort \"addedAt\" --desc --limit 10",
            "abs-cli items list | jq '.results[] | select(.media.metadata.isbn == null)'");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(LibraryItemMinified));
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var filter = parseResult.GetValue(filterOption);
            var sort = parseResult.GetValue(sortOption);
            var desc = parseResult.GetValue(descOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var result = await service.ListAsync(libraryId, filter, sort, desc, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Item ID", Required = true };
        var command = new Command("get", "Get a single library item by ID") { idOption };
        command.AddExamples(
            "abs-cli items get --id \"li_abc123\"",
            "abs-cli items get --id \"li_abc123\" | jq '.media.metadata'");
        command.AddResponseExample<LibraryItemMinified>();
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryItemMinified);
            return 0;
        });
        return command;
    }

    private static Command CreateSearchCommand()
    {
        var queryOption = new Option<string>("--query") { Description = "Search text", Required = true };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID or name" };
        var limitOption = new Option<int>("--limit") { Description = "Max results (default 50, pass higher value to retrieve more)", DefaultValueFactory = _ => 50 };
        var command = new Command("search",
            "Search across a library (substring match, case-insensitive, defaults to 50 results). Alias for 'abs-cli search' — same endpoint, same response shape.")
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
        command.AddHelpSection("Note",
            "The response populates book, podcast, authors, series, narrators,",
            "tags and genres arrays — not just books. This command is kept as",
            "an alias; prefer top-level 'abs-cli search'. See roadmap for removal.");
        command.AddExamples(
            "abs-cli items search --query \"Mistborn\"",
            "abs-cli items search --query \"Brandon Sanderson\" | jq '.authors[].name'",
            "abs-cli items search --query \"978-0\" --limit 20");
        command.AddResponseExample<SearchResult>();
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var query = parseResult.GetValue(queryOption)!;
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new ItemsService(client);
            var result = await service.SearchAsync(libraryId, query, limit);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SearchResult);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Item ID", Required = true };
        var inputOption = new Option<string>("--input") { Description = "JSON input (string or file path)", Required = true };
        var command = new Command("update", "Update a single item's metadata") { idOption, inputOption };
        command.AddExamples(
            "abs-cli items update --id \"li_abc123\" --input '{\"metadata\":{\"title\":\"New Title\"}}'",
            "abs-cli items update --id \"li_abc123\" --input payload.json",
            "abs-cli items update --id \"li_abc123\" --input '{\"metadata\":{\"genres\":[\"Fantasy\",\"Epic\"]}}'");
        command.AddResponseExample<UpdateMediaResponse>();
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var input = parseResult.GetValue(inputOption)!;
            var jsonBody = CommandHelper.ReadJsonInput(input);
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.UpdateMediaAsync(id, jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.UpdateMediaResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchUpdateCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file path" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON from stdin" };
        var command = new Command("batch-update", "Batch update multiple items") { inputOption, stdinOption };
        command.AddExamples(
            "abs-cli items batch-update --input updates.json",
            "cat updates.json | abs-cli items batch-update --stdin");
        command.AddResponseExample<BatchUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string jsonBody;
            if (stdin) jsonBody = await Console.In.ReadToEndAsync();
            else if (input != null) jsonBody = CommandHelper.ReadJsonInput(input);
            else
            {
                ConsoleOutput.WriteError("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.BatchUpdateAsync(jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BatchUpdateResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchGetCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file with libraryItemIds" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON from stdin" };
        var command = new Command("batch-get", "Batch get multiple items by ID") { inputOption, stdinOption };
        command.AddExamples(
            "abs-cli items batch-get --input ids.json",
            "echo '{\"libraryItemIds\":[\"li_abc\",\"li_def\"]}' | abs-cli items batch-get --stdin");
        command.AddResponseExample<BatchGetResponse>();
        command.AddMediaUnionShapes();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string jsonBody;
            if (stdin) jsonBody = await Console.In.ReadToEndAsync();
            else if (input != null) jsonBody = CommandHelper.ReadJsonInput(input);
            else
            {
                ConsoleOutput.WriteError("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.BatchGetAsync(jsonBody);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BatchGetResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateScanCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Item ID", Required = true };
        var command = new Command("scan", "Scan a single library item (admin-only, sync)") { idOption };
        command.AddExamples(
            "abs-cli items scan --id \"li_abc123\"",
            "abs-cli items scan --id \"li_abc123\" | jq '.result'");
        command.AddResponseExample<ScanResult>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var result = await service.ScanAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.ScanResult);
            return 0;
        });
        return command;
    }

    private static Command CreateCoverCommand()
    {
        var command = new Command("cover", "Manage book covers (apply, fetch, remove)");
        command.Subcommands.Add(CreateCoverSetCommand());
        command.Subcommands.Add(CreateCoverGetCommand());
        command.Subcommands.Add(CreateCoverRemoveCommand());
        return command;
    }

    private static Command CreateCoverSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var urlOption = new Option<string?>("--url") { Description = "Cover image URL — ABS server downloads it" };
        var fileOption = new Option<string?>("--file") { Description = "Local cover image file to upload" };
        var serverPathOption = new Option<string?>("--server-path") { Description = "Path to a file already on the ABS server's filesystem" };
        var command = new Command("set", "Apply a cover to a book by URL, local file, or existing server-side path") { idOption, urlOption, fileOption, serverPathOption };
        command.AddExamples(
            "abs-cli items cover set --id \"li_abc123\" --url \"https://example.com/cover.jpg\"",
            "abs-cli items cover set --id \"li_abc123\" --file ./cover.jpg",
            "abs-cli items cover set --id \"li_abc123\" --server-path /srv/abs/library/foo/cover.jpg");
        command.AddResponseExample<CoverApplyResponse>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var url = parseResult.GetValue(urlOption);
            var file = parseResult.GetValue(fileOption);
            var serverPath = parseResult.GetValue(serverPathOption);
            var sources = new[] { url, file, serverPath }.Count(s => !string.IsNullOrEmpty(s));
            if (sources != 1)
            {
                ConsoleOutput.WriteError("Specify exactly one of --url, --file, --server-path");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            CoverApplyResponse result;
            if (!string.IsNullOrEmpty(url))
            {
                result = await service.SetByUrlAsync(id, url);
            }
            else if (!string.IsNullOrEmpty(file))
            {
                if (!File.Exists(file))
                {
                    ConsoleOutput.WriteError($"File not found: {file}");
                    Environment.Exit(1);
                }
                result = await service.UploadFromFileAsync(id, file);
            }
            else
            {
                result = await service.LinkExistingAsync(id, serverPath!);
            }
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverApplyResponse);
        });
        return command;
    }

    private static Command CreateCoverGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var outputOption = new Option<string>("--output") { Description = "Output file path, or '-' for binary to stdout", Required = true };
        var rawOption = new Option<bool>("--raw") { Description = "Fetch the original unprocessed image (default: ABS-resized)" };
        var command = new Command("get", "Download the cover image for a book") { idOption, outputOption, rawOption };
        command.AddExamples(
            "abs-cli items cover get --id \"li_abc123\" --output cover.jpg",
            "abs-cli items cover get --id \"li_abc123\" --output cover.jpg --raw",
            "abs-cli items cover get --id \"li_abc123\" --output - > cover.jpg");
        command.AddResponseExample<CoverFileSavedDescriptor>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var raw = parseResult.GetValue(rawOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            await using var stream = await service.GetStreamAsync(id, raw);
            if (output == "-")
            {
                await using var stdout = Console.OpenStandardOutput();
                await stream.CopyToAsync(stdout);
                return;
            }
            long bytes;
            await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
                bytes = fileStream.Length;
            }
            var descriptor = new CoverFileSavedDescriptor { Path = output, Bytes = bytes };
            ConsoleOutput.WriteJson(descriptor, AppJsonContext.Default.CoverFileSavedDescriptor);
        });
        return command;
    }

    private static Command CreateCoverRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var command = new Command("remove", "Remove the cover from a book") { idOption };
        command.AddExamples(
            "abs-cli items cover remove --id \"li_abc123\"");
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            await service.RemoveAsync(id);
            ConsoleOutput.WriteJson(
                new Dictionary<string, string> { ["success"] = "true" });
        });
        return command;
    }
}

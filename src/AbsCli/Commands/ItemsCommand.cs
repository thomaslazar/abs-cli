using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class ItemsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static Command Create()
    {
        var command = new Command("items", "Manage library items");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateBatchUpdateCommand());
        command.Subcommands.Add(CreateBatchUpdateProgressCommand());
        command.Subcommands.Add(CreateBatchGetCommand());
        command.Subcommands.Add(CreateScanCommand());
        command.Subcommands.Add(CreateCoverCommand());
        command.Subcommands.Add(CreateEncodeM4bCommand());
        command.Subcommands.Add(CreateChaptersCommand());
        command.Subcommands.Add(CreateProgressCommand());
        command.Subcommands.Add(CreateEmbedMetadataCommand());
        command.Subcommands.Add(CreateBatchEmbedMetadataCommand());
        command.Subcommands.Add(CreateToggleEbookStatusCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
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
            "random                        — random order",
            "sequence                      — series sequence (only with --filter \"series=<id>\")");
        command.AddExamples(
            "abs-cli items list",
            "abs-cli items list --filter \"languages=English\" --sort \"media.metadata.title\"",
            "abs-cli items list --filter \"genres=Fantasy\" --desc",
            "abs-cli items list --filter \"series=se_abc123\" --sort sequence",
            "abs-cli items list --sort \"addedAt\" --desc --limit 10");
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
        var expandedOption = new Option<bool>("--expanded") { Description = "Return the expanded shape" };
        var includeOption = new Option<string?>("--include") { Description = "Comma-separated include flags (progress, rssfeed, share, downloads). Auto-implies --expanded." };
        var command = new Command("get", "Get a single library item by ID") { idOption, expandedOption, includeOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "--include automatically implies --expanded (server's include",
            "parser only fires under expanded=1). Values: progress (your",
            "media progress for this item), rssfeed (open RSS feed if any),",
            "share (admin and book-only; silently skipped otherwise),",
            "downloads (podcast-only; silently skipped for books).");
        command.AddExamples(
            "abs-cli items get --id \"li_abc123\"",
            "abs-cli items get --id \"li_abc123\" --expanded",
            "abs-cli items get --id \"li_abc123\" --include progress",
            "abs-cli items get --id \"li_abc123\" --include progress,rssfeed");
        command.AddResponseExample<LibraryItemMinified>();
        command.AddMediaUnionShapes();
        command.AddHelpSection("Response shape (--expanded)", HelpSectionPosition.Bottom,
            ResponseExamples.For(typeof(LibraryItemExpanded)).Split('\n'));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var explicitExpanded = parseResult.GetValue(expandedOption);
            var include = parseResult.GetValue(includeOption);
            var expanded = explicitExpanded || !string.IsNullOrEmpty(include);
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            if (expanded)
            {
                var result = await service.GetExpandedAsync(id, include);
                ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryItemExpanded);
            }
            else
            {
                var result = await service.GetAsync(id);
                ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryItemMinified);
            }
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Item ID", Required = true };
        var inputOption = new Option<string>("--input") { Description = "JSON input (string or file path)", Required = true };
        var command = new Command("update", "Update a single item's metadata") { idOption, inputOption };
        command.AddPermissionRequired("update");
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
        command.AddPermissionRequired("update");
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
                _logger.Error("Provide --input <file> or --stdin");
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
                _logger.Error("Provide --input <file> or --stdin");
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

    private static Command CreateBatchUpdateProgressCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file with array body" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON array from stdin" };
        var command = new Command("batch-update-progress", "Bulk update media progress from a JSON array")
            { inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Server returns 200 even when individual entries fail (errors",
            "only logged server-side). No per-entry feedback. Recommend",
            "pre-validating client-side or following up with `items",
            "progress get` for entries that matter.");
        command.AddExamples(
            "abs-cli items batch-update-progress --input updates.json",
            "echo '[{\"libraryItemId\":\"li_a\",\"isFinished\":true}]' | abs-cli items batch-update-progress --stdin");
        command.AddHelpSection("Response shape", HelpSectionPosition.Bottom,
            "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            string jsonBody;
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            if (stdin) jsonBody = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) jsonBody = CommandHelper.ReadJsonInput(input);
            else
            {
                _logger.Error("Provide --input <file> or --stdin.");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new ProgressService(client);
            await service.BatchUpdateAsync(jsonBody);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateScanCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Item ID", Required = true };
        var command = new Command("scan", "Scan a single library item (admin-only, sync)") { idOption };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli items scan --id \"li_abc123\"");
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
        var command = new Command("set", "Apply a cover to a library item by URL, local file, or existing server-side path") { idOption, urlOption, fileOption, serverPathOption };
        command.AddPermissionRequired("upload");
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
                _logger.Error("Specify exactly one of --url, --file, --server-path");
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
                    _logger.Error($"File not found: {file}");
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
        var command = new Command("get", "Download the cover image for a library item") { idOption, outputOption, rawOption };
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
        var command = new Command("remove", "Remove the cover from a library item") { idOption };
        command.AddPermissionRequired("delete");
        command.AddExamples(
            "abs-cli items cover remove --id \"li_abc123\"");
        command.AddHelpSection("Response shape", HelpSectionPosition.Bottom,
            "{ \"success\": \"true\" }");
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

    private static Command CreateEncodeM4bCommand()
    {
        var command = new Command("encode-m4b", "Merge multi-file audiobook into a single tagged .m4b (admin-only)");
        command.Subcommands.Add(CreateEncodeM4bStartCommand());
        command.Subcommands.Add(CreateEncodeM4bCancelCommand());
        return command;
    }

    private static readonly string[] ValidEncodeM4bCodecs = { "copy", "aac", "opus" };
    private static readonly string[] ValidEncodeM4bBitrates = { "32k", "64k", "128k", "192k" };

    private static Command CreateEncodeM4bStartCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var codecOption = new Option<string?>("--codec") { Description = "Audio codec: copy | aac | opus (copy = remux, source must be AAC)" };
        var bitrateOption = new Option<string?>("--bitrate") { Description = "Audio bitrate: 32k | 64k | 128k | 192k" };
        var channelsOption = new Option<int?>("--channels") { Description = "Audio channels: 1 | 2" };
        var command = new Command("start", "Start an encode-m4b task on a library item")
        {
            idOption, codecOption, bitrateOption, channelsOption
        };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli items encode-m4b start --id \"li_abc123\"",
            "abs-cli items encode-m4b start --id \"li_abc123\" --codec copy",
            "abs-cli items encode-m4b start --id \"li_abc123\" --codec aac --bitrate 128k --channels 2");
        command.AddHelpSection("Caveats",
            "Fire-and-forget; tasks vanish from 'tasks list' on completion (success or failure).",
            "Confirm result by re-fetching the item with 'items get'.",
            "No concurrency guardrail across items — caller serialises for batch.",
            "Originals moved (not deleted) to ABS metadata cache as backup.");
        command.AddResponseExample<EncodeM4bStartReceipt>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var codec = parseResult.GetValue(codecOption);
            var bitrate = parseResult.GetValue(bitrateOption);
            var channels = parseResult.GetValue(channelsOption);
            if (codec != null && !ValidEncodeM4bCodecs.Contains(codec))
            {
                _logger.Error($"--codec must be one of: copy, aac, opus (got '{codec}')");
                Environment.Exit(1);
            }
            if (bitrate != null && !ValidEncodeM4bBitrates.Contains(bitrate))
            {
                _logger.Error($"--bitrate must be one of: 32k, 64k, 128k, 192k (got '{bitrate}')");
                Environment.Exit(1);
            }
            if (channels.HasValue && channels.Value != 1 && channels.Value != 2)
            {
                _logger.Error($"--channels must be 1 or 2 (got {channels.Value})");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new EncodeM4bService(client);
            var receipt = await service.StartAsync(id, new EncodeM4bOptions
            {
                Codec = codec,
                Bitrate = bitrate,
                Channels = channels
            });
            ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.EncodeM4bStartReceipt);
            return 0;
        });
        return command;
    }

    private static Command CreateEncodeM4bCancelCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var command = new Command("cancel", "Cancel a pending encode-m4b task on a library item") { idOption };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli items encode-m4b cancel --id \"li_abc123\"");
        command.AddHelpSection("Caveats",
            "404 means either no task is pending or the item does not exist (ABS does not distinguish).");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new EncodeM4bService(client);
            await service.CancelAsync(id);
            return 0;
        });
        return command;
    }

    private static Command CreateChaptersCommand()
    {
        var command = new Command("chapters", "Look up or write chapter metadata for a library item");
        command.Subcommands.Add(CreateChaptersLookupCommand());
        command.Subcommands.Add(CreateChaptersSetCommand());
        return command;
    }

    private static Command CreateChaptersLookupCommand()
    {
        var asinOption = new Option<string>("--asin") { Description = "Audible/Audnexus ASIN", Required = true };
        var regionOption = new Option<string?>("--region") { Description = "Audnexus region (defaults to 'us' server-side)" };
        var command = new Command("lookup", "Look up chapter timings on Audnexus by ASIN")
        {
            asinOption, regionOption
        };
        command.AddExamples(
            "abs-cli items chapters lookup --asin \"B07TEST1\"",
            "abs-cli items chapters lookup --asin \"B07TEST1\" --region \"uk\"");
        command.AddHelpSection("Caveats",
            "Audnexus-backed (same source as 'authors lookup'/'match').",
            "Returns ms-based shape; 'items chapters set' takes seconds. No CLI conversion.",
            "No upstream match / invalid ASIN / invalid region → exit 2 with the error string.");
        command.AddResponseExample<ChaptersLookupResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var asin = parseResult.GetValue(asinOption)!;
            var region = parseResult.GetValue(regionOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new ChaptersService(client);
            var result = await service.LookupAsync(asin, region);
            if (result.Error != null)
            {
                var msg = result.Error.StringKey == "MessageChaptersNotFound"
                    ? $"Not found. {result.Error.Error}"
                    : result.Error.Error ?? "Unknown lookup error";
                _logger.Error(msg);
                return 2;
            }
            ConsoleOutput.WriteJson(result.Success!, AppJsonContext.Default.ChaptersLookupResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateChaptersSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file path" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON from stdin" };
        var command = new Command("set", "Write chapters onto a library item (DB + sidecar; does NOT touch the audio file)")
        {
            idOption, inputOption, stdinOption
        };
        command.AddPermissionRequired("update");
        command.AddExamples(
            "abs-cli items chapters set --id \"li_abc123\" --input chapters.json",
            "cat chapters.json | abs-cli items chapters set --id \"li_abc123\" --stdin");
        command.AddHelpSection("Caveats",
            "Body must be {chapters:[{title,start,end}]} in seconds.",
            "Writes ABS DB + sidecar only — use 'items embed-metadata' to bake into the file.",
            "ABS returns 500 (not 400/404) when item is missing, not a book, or has no audio tracks.",
            "No semantic validation: end>start and non-overlap are not checked.");
        command.AddResponseExample<ChaptersSetResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);

            string jsonBody;
            if (stdin && input != null)
            {
                _logger.Error("Provide exactly one of --input or --stdin (got both)");
                Environment.Exit(1);
                return 1;
            }
            else if (stdin)
            {
                jsonBody = await Console.In.ReadToEndAsync();
            }
            else if (input != null)
            {
                jsonBody = CommandHelper.ReadJsonInput(input);
            }
            else
            {
                _logger.Error("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return 1;
            }

            ChaptersSetRequest parsed;
            try
            {
                parsed = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ChaptersSetRequest)!;
            }
            catch (JsonException ex)
            {
                _logger.Error($"Invalid chapters JSON: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            var canonical = JsonSerializer.Serialize(parsed, AppJsonContext.Default.ChaptersSetRequest);

            var (client, _) = CommandHelper.BuildClient();
            var service = new ChaptersService(client);
            var result = await service.SetAsync(id, canonical);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.ChaptersSetResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateProgressCommand()
    {
        var command = new Command("progress", "Read and write your media progress on library items");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Single record per library item covers both audio and ebook",
            "state. Books only — podcast episodes are out of scope.");
        command.Subcommands.Add(CreateProgressGetCommand());
        command.Subcommands.Add(CreateProgressSetCommand());
        command.Subcommands.Add(CreateProgressRemoveCommand());
        return command;
    }

    private static Command CreateProgressGetCommand()
    {
        var libraryItemOption = new Option<string>("--library-item") { Description = "Library item ID", Required = true };
        var command = new Command("get", "Read your progress on a library item") { libraryItemOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Single record covers both audio and ebook state. 404 if no",
            "progress recorded yet. Books only.");
        command.AddExamples(
            "abs-cli items progress get --library-item \"li_abc\"");
        command.AddResponseExample<MediaProgress>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var lid = parseResult.GetValue(libraryItemOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ProgressService(client);
            var result = await service.GetAsync(lid);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.MediaProgress);
            return 0;
        });
        return command;
    }

    private static Command CreateProgressSetCommand()
    {
        var libraryItemOption = new Option<string>("--library-item") { Description = "Library item ID", Required = true };
        var currentTimeOption = new Option<double?>("--current-time") { Description = "Audio position in seconds" };
        var isFinishedOption = new Option<bool?>("--is-finished") { Description = "true|false; explicit value required (omit to leave unchanged)" };
        var ebookLocationOption = new Option<string?>("--ebook-location") { Description = "EPUB CFI string; pass \"\" to clear" };
        var ebookProgressOption = new Option<double?>("--ebook-progress") { Description = "Ebook completion fraction (0..1)" };
        var hideFromContinueOption = new Option<bool?>("--hide-from-continue-listening") { Description = "true|false; explicit value required" };
        var finishedAtOption = new Option<string?>("--finished-at") { Description = "ISO 8601 finish timestamp; requires --is-finished true (backdating)" };
        var command = new Command("set", "Set / update / clear progress fields for a library item")
        {
            libraryItemOption, currentTimeOption, isFinishedOption,
            ebookLocationOption, ebookProgressOption, hideFromContinueOption, finishedAtOption
        };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Booleans take explicit true|false (omit to leave unchanged). At",
            "least one body flag is required. --finished-at is only honored",
            "when paired with --is-finished true (use case: backdating a",
            "completion timestamp). Echoes the post-update progress via a",
            "follow-up GET.");
        command.AddExamples(
            "abs-cli items progress set --library-item \"li_abc\" --is-finished true",
            "abs-cli items progress set --library-item \"li_abc\" --current-time 1234.5",
            "abs-cli items progress set --library-item \"li_abc\" --ebook-location \"\" --ebook-progress 0",
            "abs-cli items progress set --library-item \"li_abc\" --is-finished true --finished-at \"2026-05-28T12:00:00Z\"");
        command.AddResponseExample<MediaProgress>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var lid = parseResult.GetValue(libraryItemOption)!;
            var currentTime = parseResult.GetValue(currentTimeOption);
            var isFinished = parseResult.GetValue(isFinishedOption);
            var ebookLocation = parseResult.GetValue(ebookLocationOption);
            var ebookProgress = parseResult.GetValue(ebookProgressOption);
            var hideFromContinue = parseResult.GetValue(hideFromContinueOption);
            var finishedAtRaw = parseResult.GetValue(finishedAtOption);

            var body = new ProgressUpdateRequest
            {
                CurrentTime = currentTime,
                IsFinished = isFinished,
                EbookLocation = ebookLocation,
                EbookProgress = ebookProgress,
                HideFromContinueListening = hideFromContinue
            };

            // --finished-at requires --is-finished true (server only honors
            // user-supplied finishedAt during the unfinished→finished
            // transition; see MediaProgress.js:192-197).
            if (finishedAtRaw != null)
            {
                if (isFinished != true)
                {
                    _logger.Error("--finished-at only applies with --is-finished true (server ignores it otherwise).");
                    Environment.Exit(1);
                    return 1;
                }
                if (!DateTimeOffset.TryParse(finishedAtRaw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var dto))
                {
                    _logger.Error($"--finished-at value '{finishedAtRaw}' is not valid ISO 8601.");
                    Environment.Exit(1);
                    return 1;
                }
                body.FinishedAt = dto.ToUnixTimeMilliseconds();
            }

            // At least one body flag required.
            if (currentTime == null && isFinished == null && ebookLocation == null
                && ebookProgress == null && hideFromContinue == null && body.FinishedAt == null)
            {
                _logger.Error("Specify at least one of --current-time, --is-finished, --ebook-location, --ebook-progress, --hide-from-continue-listening, --finished-at");
                Environment.Exit(1);
                return 1;
            }

            var (client, _) = CommandHelper.BuildClient();
            var service = new ProgressService(client);
            var result = await service.SetAsync(lid, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.MediaProgress);
            return 0;
        });
        return command;
    }

    private static Command CreateProgressRemoveCommand()
    {
        var libraryItemOption = new Option<string>("--library-item") { Description = "Library item ID", Required = true };
        var command = new Command("remove", "Clear all progress for a library item") { libraryItemOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes both audio and ebook progress in one shot — server has",
            "no per-half delete. To reset only one half, use `items progress",
            "set` with cleared values for the half you want zeroed (e.g.",
            "--ebook-location \"\" --ebook-progress 0).");
        command.AddExamples(
            "abs-cli items progress remove --library-item \"li_abc\"");
        command.AddHelpSection("Response shape", HelpSectionPosition.Bottom,
            "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var lid = parseResult.GetValue(libraryItemOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ProgressService(client);
            await service.RemoveAsync(lid);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateEmbedMetadataCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var noBackupOption = new Option<bool>("--no-backup") { Description = "Skip the server-side pre-rewrite backup (default: backup on)" };
        var forceEmbedChaptersOption = new Option<bool>("--force-embed-chapters") { Description = "Embed chapters into multi-file items (default: tags + cover only on multi-file)" };
        var waitOption = new Option<bool>("--wait") { Description = "Block until the embed task disappears from /api/tasks (max 600s)" };
        var command = new Command("embed-metadata", "Embed ABS state (tags, cover, chapters) into the audio files via in-place ffmpeg rewrite (admin-only)")
        {
            idOption, noBackupOption, forceEmbedChaptersOption, waitOption
        };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli items embed-metadata --id \"li_abc123\"",
            "abs-cli items embed-metadata --id \"li_abc123\" --wait",
            "abs-cli items embed-metadata --id \"li_abc123\" --force-embed-chapters --no-backup");
        command.AddHelpSection("Caveats",
            "Admin only.",
            "In-place destructive rewrite — --backup defaults on; pass --no-backup only when you mean it.",
            "Multi-file books: chapters embedded only with --force-embed-chapters.",
            "--wait exits 0 when ABS stops processing; this does NOT guarantee success.",
            "ABS internally queues at MAX_CONCURRENT_TASKS; --wait may sit in queue first.");
        command.AddResponseExample<EmbedMetadataReceipt>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var noBackup = parseResult.GetValue(noBackupOption);
            var forceEmbedChapters = parseResult.GetValue(forceEmbedChaptersOption);
            var wait = parseResult.GetValue(waitOption);
            var options = new EmbedMetadataOptions
            {
                Backup = !noBackup,
                ForceEmbedChapters = forceEmbedChapters
            };
            var (client, _) = CommandHelper.BuildClient();
            var service = new EmbedMetadataService(client);
            var receipt = await service.StartAsync(id, options);
            if (wait)
            {
                var ok = await service.WaitForCompletionAsync(
                    new[] { id }, TimeSpan.FromSeconds(600), cancellationToken);
                if (!ok)
                {
                    _logger.Error("Timed out waiting for embed-metadata task to complete");
                    Environment.Exit(2);
                    return 2;
                }
            }
            ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.EmbedMetadataReceipt);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchEmbedMetadataCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file path with {libraryItemIds:[...]}" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON from stdin" };
        var noBackupOption = new Option<bool>("--no-backup") { Description = "Skip the server-side pre-rewrite backup (default: backup on)" };
        var forceEmbedChaptersOption = new Option<bool>("--force-embed-chapters") { Description = "Embed chapters into multi-file items (default: tags + cover only on multi-file)" };
        var waitOption = new Option<bool>("--wait") { Description = "Block until all embed tasks disappear from /api/tasks (max 600s)" };
        var command = new Command("batch-embed-metadata", "Embed ABS state into multiple items' audio files in one batch (admin-only)")
        {
            inputOption, stdinOption, noBackupOption, forceEmbedChaptersOption, waitOption
        };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli items batch-embed-metadata --input ids.json --wait",
            "echo '{\"libraryItemIds\":[\"li_a\",\"li_b\"]}' | abs-cli items batch-embed-metadata --stdin");
        command.AddHelpSection("Caveats",
            "Admin only.",
            "Body must be {\"libraryItemIds\":[...]}.",
            "Batch validates ALL items upfront — any one bad ID aborts the whole batch.",
            "Same options applied uniformly across every item — no per-item override.",
            "In-place destructive rewrite — --backup defaults on; pass --no-backup only when you mean it.",
            "Multi-file books: chapters embedded only with --force-embed-chapters.",
            "--wait exits 0 when ABS stops processing; this does NOT guarantee success.",
            "ABS internally queues at MAX_CONCURRENT_TASKS.");
        command.AddResponseExample<BatchEmbedMetadataReceipt>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            var noBackup = parseResult.GetValue(noBackupOption);
            var forceEmbedChapters = parseResult.GetValue(forceEmbedChaptersOption);
            var wait = parseResult.GetValue(waitOption);

            string jsonBody;
            if (stdin && input != null)
            {
                _logger.Error("Provide exactly one of --input or --stdin (got both)");
                Environment.Exit(1);
                return 1;
            }
            else if (stdin)
            {
                jsonBody = await Console.In.ReadToEndAsync();
            }
            else if (input != null)
            {
                jsonBody = CommandHelper.ReadJsonInput(input);
            }
            else
            {
                _logger.Error("Provide --input <file> or --stdin");
                Environment.Exit(1);
                return 1;
            }

            BatchEmbedMetadataRequest request;
            try
            {
                request = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.BatchEmbedMetadataRequest)!;
            }
            catch (JsonException ex)
            {
                _logger.Error($"Invalid JSON: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            if (request.LibraryItemIds.Count == 0)
            {
                _logger.Error("libraryItemIds must be a non-empty array");
                Environment.Exit(1);
                return 1;
            }

            var options = new EmbedMetadataOptions
            {
                Backup = !noBackup,
                ForceEmbedChapters = forceEmbedChapters
            };
            var (client, _) = CommandHelper.BuildClient();
            var service = new EmbedMetadataService(client);
            var receipt = await service.StartBatchAsync(request, options);
            if (wait)
            {
                var ok = await service.WaitForCompletionAsync(
                    request.LibraryItemIds, TimeSpan.FromSeconds(600), cancellationToken);
                if (!ok)
                {
                    _logger.Error("Timed out waiting for embed-metadata task(s) to complete");
                    Environment.Exit(2);
                    return 2;
                }
            }
            ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.BatchEmbedMetadataReceipt);
            return 0;
        });
        return command;
    }

    private static Command CreateToggleEbookStatusCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var inoOption = new Option<string>("--ino") { Description = "Ebook file's inode (from items get → libraryFiles[].ino)", Required = true };
        var command = new Command("toggle-ebook-status", "Toggle which ebook file is primary on a multi-format item")
        {
            idOption, inoOption
        };
        command.AddPermissionRequired("update");
        command.AddExamples(
            "abs-cli items toggle-ebook-status --id \"li_abc123\" --ino \"12345678\"");
        command.AddHelpSection("Caveats",
            "Toggle: targeting a supplementary makes it primary; targeting the current primary unsets it (no auto-promote).",
            "--ino comes from 'items get --expanded' → libraryFiles[].ino.");
        command.AddResponseExample<EbookFileStatusReceipt>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var ino = parseResult.GetValue(inoOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            await service.ToggleEbookFileStatusAsync(id, ino);
            var receipt = new EbookFileStatusReceipt
            {
                LibraryItemId = id,
                FileIno = ino,
                Action = "toggle-ebook-status",
                Toggled = true
            };
            ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.EbookFileStatusReceipt);
            return 0;
        });
        return command;
    }
}

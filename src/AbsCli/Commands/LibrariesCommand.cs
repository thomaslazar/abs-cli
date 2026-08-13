using System.CommandLine;
using System.Text.Json;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class LibrariesCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static Command Create()
    {
        var command = new Command("libraries", "Manage libraries");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateScanCommand());
        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateReorderCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var serverOption = new Option<string?>("--server") { Description = "Server URL override" };
        var tokenOption = new Option<string?>("--token") { Description = "Token override" };
        var command = new Command("list", "List all libraries") { serverOption, tokenOption };
        command.AddExamples(
            "abs-cli libraries list");
        command.AddResponseExample<LibraryListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var server = parseResult.GetValue(serverOption);
            var token = parseResult.GetValue(tokenOption);
            var (client, _) = CommandHelper.BuildClient(serverOverride: server, tokenOverride: token);
            var service = new LibrariesService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library ID", Required = true };
        var serverOption = new Option<string?>("--server") { Description = "Server URL override" };
        var tokenOption = new Option<string?>("--token") { Description = "Token override" };
        var command = new Command("get", "Get a single library") { idOption, serverOption, tokenOption };
        command.AddExamples(
            "abs-cli libraries get --id \"lib_abc123\"");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var server = parseResult.GetValue(serverOption);
            var token = parseResult.GetValue(tokenOption);
            var (client, _) = CommandHelper.BuildClient(serverOverride: server, tokenOverride: token);
            var service = new LibrariesService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    private static Command CreateScanCommand()
    {
        var idOption = new Option<string?>("--id") { Description = "Library ID (or default from config)" };
        var forceOption = new Option<bool>("--force") { Description = "Force full rescan" };
        var command = new Command("scan", "Trigger a library scan (admin-only, async)") { idOption, forceOption };
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli libraries scan",
            "abs-cli libraries scan --force",
            "abs-cli libraries scan --id \"lib_abc123\"");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption);
            var force = parseResult.GetValue(forceOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: id);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new LibrariesService(client);
            await service.ScanAsync(libraryId, force);
            return 0;
        });
        return command;
    }

    private static Command CreateCreateCommand()
    {
        var nameOption = new Option<string>("--name") { Description = "Library name", Required = true };
        var folderOption = new Option<string[]>("--folder") { Description = "Server-side folder path (repeatable; created if missing)", AllowMultipleArgumentsPerToken = true };
        var mediaTypeOption = new Option<string?>("--media-type") { Description = "book | podcast (default book)" };
        var providerOption = new Option<string?>("--provider") { Description = "Metadata provider (default google)" };
        var iconOption = new Option<string?>("--icon") { Description = "Library icon (default database)" };
        var command = new Command("create", "Create a new library")
        { nameOption, folderOption, mediaTypeOption, providerOption, iconOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "At least one --folder is required.");
        command.AddExamples(
            "abs-cli libraries create --name \"Audiobooks\" --folder /audiobooks",
            "abs-cli libraries create --name \"Pods\" --folder /pods --media-type podcast");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var folders = parseResult.GetValue(folderOption) ?? Array.Empty<string>();
            var mediaType = parseResult.GetValue(mediaTypeOption);
            var provider = parseResult.GetValue(providerOption);
            var icon = parseResult.GetValue(iconOption);
            if (folders.Length == 0)
            {
                _logger.Error("At least one --folder is required.");
                Environment.Exit(1);
                return 1;
            }
            var body = new LibraryCreateRequest
            {
                Name = name,
                Folders = folders.Select(f => new LibraryFolderRequest { FullPath = f }).ToList(),
                MediaType = string.IsNullOrEmpty(mediaType) ? null : mediaType,
                Provider = string.IsNullOrEmpty(provider) ? null : provider,
                Icon = string.IsNullOrEmpty(icon) ? null : icon
            };
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.CreateAsync(body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name" };
        var mediaTypeOption = new Option<string?>("--media-type") { Description = "New media type (book | podcast)" };
        var providerOption = new Option<string?>("--provider") { Description = "New metadata provider" };
        var iconOption = new Option<string?>("--icon") { Description = "New icon" };
        var displayOrderOption = new Option<int?>("--display-order") { Description = "New display order (number)" };
        var command = new Command("update", "Edit a library's name, media type, provider, icon, or display order")
        { idOption, nameOption, mediaTypeOption, providerOption, iconOption, displayOrderOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Empty --name is rejected. At least one edit flag is required.");
        command.AddExamples(
            "abs-cli libraries update --id \"lib_1\" --name \"Renamed\"",
            "abs-cli libraries update --id \"lib_1\" --display-order 2");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var mediaType = parseResult.GetValue(mediaTypeOption);
            var provider = parseResult.GetValue(providerOption);
            var icon = parseResult.GetValue(iconOption);
            var displayOrder = parseResult.GetValue(displayOrderOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
                return 1;
            }
            var body = BuildUpdateBody(name, mediaType, provider, icon, displayOrder);
            if (body.Name == null && body.MediaType == null && body.Provider == null && body.Icon == null && body.DisplayOrder == null)
            {
                _logger.Error("Specify at least one of --name, --media-type, --provider, --icon, --display-order");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body from the flags, coercing empty strings to null so
    /// they are omitted. The production body builder for `update`; exposed
    /// <c>internal</c> so it can be unit-tested directly.
    /// </summary>
    internal static LibraryUpdateRequest BuildUpdateBody(string? name, string? mediaType, string? provider, string? icon, int? displayOrder)
    {
        return new LibraryUpdateRequest
        {
            Name = string.IsNullOrEmpty(name) ? null : name,
            MediaType = string.IsNullOrEmpty(mediaType) ? null : mediaType,
            Provider = string.IsNullOrEmpty(provider) ? null : provider,
            Icon = string.IsNullOrEmpty(icon) ? null : icon,
            DisplayOrder = displayOrder
        };
    }

    /// <summary>
    /// True when the typed confirmation (trimmed) exactly matches the library
    /// name. Case-sensitive; null/empty never matches. Exposed for testing.
    /// </summary>
    internal static bool ConfirmationMatches(string? input, string libraryName)
        => input?.Trim() == libraryName;

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library ID", Required = true };
        var command = new Command("delete", "Delete a library and ALL its contents") { idOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "DESTRUCTIVE CASCADE: permanently deletes the library AND every item",
            "in it, all collections for the library, and removes it from playlists",
            "and playback sessions. Cannot be undone. Returns the deleted library.",
            "",
            "Guarded: you must type the library's exact name to confirm (the",
            "name is shown in the prompt). Non-matching input aborts (exit 1).");
        command.AddExamples(
            "abs-cli libraries delete --id \"lib_1\"");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            // Fetch first so the confirmation prompt can show what will be
            // destroyed (a deliberate pre-fetch — the safety gate needs the name).
            var library = await service.GetAsync(id);
            Console.Error.WriteLine(
                $"WARNING: this permanently deletes library \"{library.Name}\" ({library.Id}) and ALL its " +
                "contents (items, collections, playlist/session references). This cannot be undone.");
            Console.Error.Write("Type the library name to confirm: ");
            var confirmation = Console.In.ReadLine();
            if (!ConfirmationMatches(confirmation, library.Name))
            {
                _logger.Error("Confirmation did not match the library name. Aborted.");
                Environment.Exit(1);
                return 1;
            }
            var result = await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    private static Command CreateReorderCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file with an array of {id, newOrder}" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the reorder JSON array from stdin" };
        var command = new Command("reorder", "Reorder libraries by display order")
        { inputOption, stdinOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Body is a JSON array of objects: [{\"id\":\"lib_1\",\"newOrder\":1}, ...].");
        command.AddExamples(
            "abs-cli libraries reorder --input order.json",
            "echo '[{\"id\":\"lib_1\",\"newOrder\":1}]' | abs-cli libraries reorder --stdin");
        command.AddRequestExample<List<LibraryReorderEntry>>();
        command.AddResponseExample<LibraryListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            string orderJson;
            if (stdin) orderJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) orderJson = CommandHelper.ReadJsonInput(input);
            else
            {
                _logger.Error("Provide --input <file> or --stdin.");
                Environment.Exit(1);
                return 1;
            }
            string validated;
            try
            {
                validated = PrepareReorderBody(orderJson);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                _logger.Error($"Invalid reorder JSON: {ex.Message}");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.ReorderAsync(validated);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryListResponse);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Validates a reorder body and returns it unchanged. ABS requires the
    /// whole body to be an array of objects, each with a string "id" and a
    /// numeric "newOrder" (LibraryController.reorder) — a single bad entry
    /// 400s the entire request before any library is touched, so we check
    /// the same up front rather than shipping a request we know will fail.
    /// </summary>
    internal static string PrepareReorderBody(string jsonBody)
    {
        var entries = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ListLibraryReorderEntry);
        if (entries is null)
            throw new ArgumentException("reorder requires a JSON array of objects");
        if (entries.Any(e => string.IsNullOrEmpty(e.Id) || e.NewOrder is null))
            throw new ArgumentException("every reorder entry needs a string \"id\" and a numeric \"newOrder\"");
        return jsonBody;
    }
}

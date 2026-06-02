using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class AuthorsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static Command Create()
    {
        var command = new Command("authors", "Manage authors");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Authors are derived from book metadata. The scanner removes orphaned",
            "authors on its next run (unless a custom image is set). Use 'delete' to",
            "remove one explicitly.",
            "",
            "'match' and 'lookup' both query Audnexus (audnex.us, same backend as the",
            "ABS web UI). 'match' writes; 'lookup' is read-only.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateMatchCommand());
        command.Subcommands.Add(CreateLookupCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateImageCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var sortOption = new Option<string?>("--sort") { Description = "Sort field (name | lastFirst | addedAt | updatedAt | numBooks); default name" };
        var descOption = new Option<bool>("--desc") { Description = "Sort descending" };
        var command = new Command("list", "List authors in a library (paginated)")
        { libraryOption, limitOption, pageOption, sortOption, descOption };
        command.AddExamples(
            "abs-cli authors list",
            "abs-cli authors list --limit 100 --page 0",
            "abs-cli authors list --sort numBooks --desc --limit 10");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var sort = parseResult.GetValue(sortOption) ?? "name";
            var desc = parseResult.GetValue(descOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new AuthorsService(client);
            var result = await service.ListAsync(libraryId, limit, page, sort, desc);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("get", "Get a single author") { idOption };
        command.AddExamples(
            "abs-cli authors get --id \"aut_abc123\"");
        command.AddResponseExample<AuthorItem>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorItem);
            return 0;
        });
        return command;
    }

    private static Command CreateMatchCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "Search Audnexus by name (mutually exclusive with --asin)" };
        var asinOption = new Option<string?>("--asin") { Description = "Look up Audnexus author by ASIN (mutually exclusive with --name)" };
        var regionOption = new Option<string?>("--region") { Description = "Audnexus region override (defaults to 'us' server-side)" };
        var command = new Command("match", "Apply Audnexus author data to an existing ABS author")
        {
            idOption, nameOption, asinOption, regionOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Destructive: writes ASIN, description, and image onto the author.",
            "",
            "Audnexus picks the closest Levenshtein match by name and silently drops",
            "alternatives — pass --asin to disambiguate same-name authors.",
            "",
            "No upstream match → exit 2, stderr \"Not found. Author not found\";",
            "the ABS author record is unchanged.");
        command.AddExamples(
            "abs-cli authors match --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors match --id \"aut_xyz\" --asin \"B000AP9DSU\"",
            "abs-cli authors match --id \"aut_xyz\" --name \"Bob Bunyon\" --region \"uk\"");
        command.AddResponseExample<AuthorMatchResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var asin = parseResult.GetValue(asinOption);
            var region = parseResult.GetValue(regionOption);
            var sources = new[] { name, asin }.Count(s => !string.IsNullOrEmpty(s));
            if (sources != 1)
            {
                _logger.Error("Specify exactly one of --name or --asin");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var body = new AuthorMatchRequest
            {
                Q = string.IsNullOrEmpty(name) ? null : name,
                Asin = string.IsNullOrEmpty(asin) ? null : asin,
                Region = string.IsNullOrEmpty(region) ? null : region
            };
            var result = await service.MatchAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorMatchResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateLookupCommand()
    {
        var nameOption = new Option<string>("--name") { Description = "Author name to search Audnexus", Required = true };
        var command = new Command("lookup", "Read-only Audnexus probe by author name") { nameOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Read-only Audnexus probe; does not touch ABS state.",
            "",
            "Returns a single best-guess match. If the result looks wrong, look up",
            "the specific author by ASIN via 'authors match'.",
            "",
            "No match → prints literal JSON null and exits 0.");
        command.AddExamples(
            "abs-cli authors lookup --name \"Brandon Sanderson\"");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var json = await service.LookupAsync(name);
            ConsoleOutput.WriteRawJson(json);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (renaming to an existing name in the same library merges authors — see Notes)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description; empty string clears the field" };
        var asinOption = new Option<string?>("--asin") { Description = "New ASIN; empty string clears the field" };
        var command = new Command("update", "Edit an author's name, description, and/or ASIN")
        {
            idOption, nameOption, descriptionOption, asinOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Merge-on-rename: renaming to an existing author's name in the same",
            "library merges the two — books move to the target, source is deleted.",
            "Response becomes { merged: true, author: <target> } instead of",
            "{ updated, author }. Any other fields supplied alongside the rename are",
            "silently dropped.",
            "",
            "Empty string for --description or --asin clears the field. Empty --name",
            "is rejected. At least one editable flag is required.");
        command.AddExamples(
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors update --id \"aut_xyz\" --description \"American author of high fantasy\"",
            "abs-cli authors update --id \"aut_xyz\" --asin \"\"",
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\" --description \"American author of high fantasy\" --asin \"B000AP9DSU\"");
        command.AddResponseExample<AuthorUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            var asin = parseResult.GetValue(asinOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
            }
            var body = BuildUpdateBodyForTesting(name, description, asin);
            if (body.Count == 0)
            {
                _logger.Error("Specify at least one of --name, --description, --asin");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorUpdateResponse);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body honouring the tri-state semantics: null = field
    /// absent (omit from JSON), empty string = clear (send JSON null),
    /// non-empty = set value. Exposed internally for unit testing.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBodyForTesting(string? name, string? description, string? asin)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description == "" ? null! : description;
        if (asin is not null)
            body["asin"] = asin == "" ? null! : asin;
        return body;
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("delete", "Delete an author and unlink it from all books") { idOption };
        command.AddPermissionRequired("delete");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the author from all books and deletes the record. The scanner",
            "may re-derive it on the next run if a book's file metadata still names",
            "it.");
        command.AddExamples(
            "abs-cli authors delete --id \"aut_xyz\"");
        command.AddShapeSection("Response shape",
            "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateImageCommand()
    {
        var command = new Command("image", "Manage author images (set, get, remove)");
        command.Subcommands.Add(CreateImageSetCommand());
        command.Subcommands.Add(CreateImageGetCommand());
        command.Subcommands.Add(CreateImageRemoveCommand());
        return command;
    }

    private static Command CreateImageSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var urlOption = new Option<string>("--url") { Description = "Image URL (http or https) — ABS server downloads it", Required = true };
        var command = new Command("set", "Set the author image from a URL")
        { idOption, urlOption };
        command.AddPermissionRequired("upload");
        command.AddExamples(
            "abs-cli authors image set --id \"aut_xyz\" --url \"https://example.com/author.png\"");
        command.AddResponseExample<AuthorImageResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var url = parseResult.GetValue(urlOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.SetImageAsync(id, url);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorImageResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateImageGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var outputOption = new Option<string>("--output") { Description = "Output file path, or '-' for binary to stdout", Required = true };
        var rawOption = new Option<bool>("--raw") { Description = "Fetch the original unprocessed image (default: ABS-resized)" };
        var command = new Command("get", "Download the author image")
        { idOption, outputOption, rawOption };
        command.AddExamples(
            "abs-cli authors image get --id \"aut_xyz\" --output author.jpg",
            "abs-cli authors image get --id \"aut_xyz\" --output author.png --raw",
            "abs-cli authors image get --id \"aut_xyz\" --output - > author.jpg");
        command.AddResponseExample<CoverFileSavedDescriptor>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var raw = parseResult.GetValue(rawOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            await using var stream = await service.GetImageStreamAsync(id, raw);
            if (output == "-")
            {
                await using var stdout = Console.OpenStandardOutput();
                await stream.CopyToAsync(stdout);
                return 0;
            }
            long bytes;
            await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
                bytes = fileStream.Length;
            }
            var descriptor = new CoverFileSavedDescriptor { Path = output, Bytes = bytes };
            ConsoleOutput.WriteJson(descriptor, AppJsonContext.Default.CoverFileSavedDescriptor);
            return 0;
        });
        return command;
    }

    private static Command CreateImageRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("remove", "Remove the author image")
        { idOption };
        command.AddPermissionRequired("delete");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "No current image → exit 2, stderr \"Bad request. Author has no",
            "image path set\". Check imagePath via 'authors get' first if needed.");
        command.AddExamples(
            "abs-cli authors image remove --id \"aut_xyz\"");
        command.AddResponseExample<AuthorImageResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.RemoveImageAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorImageResponse);
            return 0;
        });
        return command;
    }
}

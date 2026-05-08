using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class AuthorsCommand
{
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
                ConsoleOutput.WriteError("Specify exactly one of --name or --asin");
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
                ConsoleOutput.WriteError("--name cannot be empty");
                Environment.Exit(1);
            }
            var body = BuildUpdateBodyForTesting(name, description, asin);
            if (body.Count == 0)
            {
                ConsoleOutput.WriteError("Specify at least one of --name, --description, --asin");
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
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the author from all books and deletes the record. The scanner",
            "may re-derive it on the next run if a book's file metadata still names",
            "it.");
        command.AddExamples(
            "abs-cli authors delete --id \"aut_xyz\"");
        command.AddHelpSection("Response shape", HelpSectionPosition.Bottom,
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
}

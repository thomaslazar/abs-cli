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
            "Authors are derived from book metadata. An author record exists while at",
            "least one library item references it. When the last referencing item is",
            "removed or re-tagged, the scanner deletes the author on its next run",
            "(unless a custom image is set). To remove an author, update the books",
            "that reference it.",
            "",
            "Author matching uses the Audnexus provider (audnex.us), the same backend",
            "the ABS web UI uses. 'match' writes ASIN/description/image onto the ABS",
            "author record; 'lookup' is a read-only probe that does not touch ABS",
            "state.");
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
        var libraryOption = new Option<string?>("--library") { Description = "Library ID or name" };
        var command = new Command("list",
            "List authors in a library (returns all, no pagination)") { libraryOption };
        command.AddExamples(
            "abs-cli authors list",
            "abs-cli authors list | jq '.authors[] | {name, numBooks}'",
            "abs-cli authors list | jq '.authors | sort_by(.numBooks) | reverse | .[:5]'");
        command.AddResponseExample<AuthorListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new AuthorsService(client);
            var result = await service.ListAsync(libraryId);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("get", "Get a single author") { idOption };
        command.AddExamples(
            "abs-cli authors get --id \"aut_abc123\"",
            "abs-cli authors get --id \"aut_abc123\" | jq '.name'");
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
            "Destructive on hit: writes ASIN, description, and image onto the author",
            "and emits an 'author_updated' socket event. Image is written only when",
            "the author had no prior image or the ASIN changed.",
            "",
            "Audnexus may return multiple candidates for a name. ABS picks the closest",
            "Levenshtein match and silently discards alternatives — for two real-world",
            "authors with the same name, the wrong one may be picked. Pass --asin to",
            "disambiguate.",
            "",
            "404 means 'no upstream match found' — useful when scanning for unmatched",
            "authors. The ABS author record is untouched on 404.");
        command.AddExamples(
            "abs-cli authors match --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors match --id \"aut_xyz\" --asin \"B000AP9DSU\"",
            "abs-cli authors match --id \"aut_xyz\" --name \"Bob Bunyon\" --region \"uk\"");
        command.AddResponseExample<AuthorMatchResponse>();
        command.SetAction(async parseResult =>
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
            var request = new AuthorMatchRequest
            {
                Q = string.IsNullOrEmpty(name) ? null : name,
                Asin = string.IsNullOrEmpty(asin) ? null : asin,
                Region = string.IsNullOrEmpty(region) ? null : region
            };
            var result = await service.MatchAsync(id, request);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorMatchResponse);
        });
        return command;
    }

    private static Command CreateLookupCommand()
    {
        var nameOption = new Option<string>("--name") { Description = "Author name to search Audnexus", Required = true };
        var command = new Command("lookup", "Read-only Audnexus probe by author name") { nameOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Read-only Audnexus probe. Does not touch any ABS author record.",
            "",
            "ABS reduces multiple Audnexus candidates to the closest-Levenshtein single",
            "match. The candidate list is not exposed.",
            "",
            "Returns the literal JSON 'null' (HTTP 200) when no match is found — agents",
            "check the JSON value, not the HTTP status. The CLI does not exit non-zero",
            "for this case.",
            "",
            "Region selection is not supported (the underlying endpoint does not accept",
            "one); ASIN lookup is not available (this is a name-only search).");
        command.AddExamples(
            "abs-cli authors lookup --name \"Brandon Sanderson\"");
        command.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var json = await service.LookupAsync(name);
            ConsoleOutput.WriteRawJson(json);
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
            "Merge-on-rename: if --name is set to a name that already exists in the",
            "same library, ABS auto-merges the two authors. All books of the source",
            "author are reassigned to the existing author and the source author is",
            "deleted. Books are not lost; the operation is recoverable by re-editing",
            "books. The response becomes { merged: true, author: <existingAuthor> }",
            "instead of the usual { updated, author }.",
            "",
            "When the merge path runs, any also-supplied --description / --asin is",
            "silently dropped. The merged-into author keeps its original description",
            "and asin.",
            "",
            "Empty string for --description or --asin clears the field on the server",
            "(JSON null). Empty --name is rejected client-side. At least one of",
            "--name / --description / --asin must be supplied.");
        command.AddExamples(
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors update --id \"aut_xyz\" --description \"American author of high fantasy\"",
            "abs-cli authors update --id \"aut_xyz\" --asin \"\"",
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\" --description \"American author of high fantasy\" --asin \"B000AP9DSU\"");
        command.AddResponseExample<AuthorUpdateResponse>();
        command.SetAction(async parseResult =>
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
            "Removes the author from all books and deletes the record. Books lose",
            "their author tag; the scanner may re-derive the author on its next run",
            "if file metadata still references the name (see the group-level lifecycle",
            "note).",
            "",
            "No confirmation prompt — consistent with 'backup delete' and 'items cover",
            "remove'. Mirrors the ABS web UI's delete behaviour.");
        command.AddExamples(
            "abs-cli authors delete --id \"aut_xyz\"");
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
        });
        return command;
    }
}

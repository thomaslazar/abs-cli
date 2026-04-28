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
            "that reference it.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
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
}

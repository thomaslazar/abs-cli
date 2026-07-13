using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class NarratorsCommand
{
    public static Command Create()
    {
        var command = new Command("narrators", "Manage narrators");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Narrators are derived from book metadata and scoped to a library.",
            "'rename' and 'delete' both require the 'update' permission (delete",
            "does NOT need 'delete').");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("list", "List narrators in a library (natural-sorted by name)")
        { libraryOption };
        command.AddExamples("abs-cli narrators list");
        command.AddResponseExample<NarratorListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.ListAsync(libraryId);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldArg = new Argument<string>("old-narrator") { Description = "Existing narrator name" };
        var newArg = new Argument<string>("new-narrator") { Description = "New narrator name" };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("rename", "Rename a narrator across all items in a library")
        {
            oldArg,
            newArg,
            libraryOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old name",
            "move onto the existing narrator. Returns the count of items updated.");
        command.AddExamples("abs-cli narrators rename \"Rob Inglis\" \"Robert Inglis\"");
        command.AddResponseExample<NarratorUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldName = parseResult.GetValue(oldArg)!;
            var newName = parseResult.GetValue(newArg)!;
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.RenameAsync(libraryId, oldName, newName);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorUpdateResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var narratorArg = new Argument<string>("narrator") { Description = "Narrator name to remove" };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("delete", "Remove a narrator from every item in a library that has it")
        {
            narratorArg,
            libraryOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Requires the 'update' permission (NOT 'delete'). Removes the narrator",
            "from all items and returns the count of items updated. No confirmation",
            "prompt.");
        command.AddExamples("abs-cli narrators delete \"Rob Inglis\"");
        command.AddResponseExample<NarratorUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var narrator = parseResult.GetValue(narratorArg)!;
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.DeleteAsync(libraryId, narrator);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorUpdateResponse);
            return 0;
        });
        return command;
    }
}

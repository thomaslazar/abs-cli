using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class TagsCommand
{
    public static Command Create()
    {
        var command = new Command("tags", "Manage tags");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "All tag operations require admin.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all tags (server-sorted, case-insensitive)");
        command.AddPermissionRequired("admin");
        command.AddExamples("abs-cli tags list");
        command.AddResponseExample<TagListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldTagArg = new Argument<string>("old-tag") { Description = "Existing tag" };
        var newTagArg = new Argument<string>("new-tag") { Description = "New tag name" };
        var command = new Command("rename", "Rename a tag across all items")
        {
            oldTagArg,
            newTagArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old tag",
            "move onto the existing tag and the response reports tagMerged: true.");
        command.AddExamples("abs-cli tags rename scifi \"Science Fiction\"");
        command.AddResponseExample<TagRenameResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldTag = parseResult.GetValue(oldTagArg)!;
            var newTag = parseResult.GetValue(newTagArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.RenameAsync(oldTag, newTag);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagRenameResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var tagArg = new Argument<string>("tag") { Description = "Tag to delete" };
        var command = new Command("delete", "Remove a tag from every item that has it")
        {
            tagArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the tag from all items and returns numItemsUpdated. No",
            "confirmation prompt.");
        command.AddExamples("abs-cli tags delete scifi");
        command.AddResponseExample<TagDeleteResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var tag = parseResult.GetValue(tagArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.DeleteAsync(tag);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagDeleteResponse);
            return 0;
        });
        return command;
    }
}

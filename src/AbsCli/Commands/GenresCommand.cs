using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class GenresCommand
{
    public static Command Create()
    {
        var command = new Command("genres", "Manage genres");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "All genre operations require admin.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all genres (unsorted — server discovery order, unlike tags)");
        command.AddPermissionRequired("admin");
        command.AddExamples("abs-cli genres list");
        command.AddResponseExample<GenreListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldGenreArg = new Argument<string>("old-genre") { Description = "Existing genre" };
        var newGenreArg = new Argument<string>("new-genre") { Description = "New genre name" };
        var command = new Command("rename", "Rename a genre across all items")
        {
            oldGenreArg,
            newGenreArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old genre",
            "move onto the existing genre and the response reports genreMerged: true.");
        command.AddExamples("abs-cli genres rename scifi \"Science Fiction\"");
        command.AddResponseExample<GenreRenameResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldGenre = parseResult.GetValue(oldGenreArg)!;
            var newGenre = parseResult.GetValue(newGenreArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.RenameAsync(oldGenre, newGenre);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreRenameResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var genreArg = new Argument<string>("genre") { Description = "Genre to delete" };
        var command = new Command("delete", "Remove a genre from every item that has it")
        {
            genreArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the genre from all items and returns numItemsUpdated. No",
            "confirmation prompt.");
        command.AddExamples("abs-cli genres delete scifi");
        command.AddResponseExample<GenreDeleteResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var genre = parseResult.GetValue(genreArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.DeleteAsync(genre);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreDeleteResponse);
            return 0;
        });
        return command;
    }
}

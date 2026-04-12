using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class AuthorsCommand
{
    public static Command Create()
    {
        var command = new Command("authors", "Manage authors");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var command = new Command("list",
            "List authors in a library (returns all, no pagination)") { libraryOption };
        command.AddExamples(
            "abs-cli authors list",
            "abs-cli authors list | jq '.authors[] | {name, numBooks}'",
            "abs-cli authors list | jq '.authors | sort_by(.numBooks) | reverse | .[:5]'");

        command.SetHandler(async (string? library) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new AuthorsService(client);
            var json = await service.ListAsync(libraryId);
            ConsoleOutput.WriteRawJson(json);
        }, libraryOption);

        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id", "Author ID") { IsRequired = true };
        var command = new Command("get", "Get a single author") { idOption };
        command.AddExamples(
            "abs-cli authors get --id \"aut_abc123\"",
            "abs-cli authors get --id \"aut_abc123\" | jq '.name'");

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var json = await service.GetAsync(id);
            ConsoleOutput.WriteRawJson(json);
        }, idOption);

        return command;
    }
}

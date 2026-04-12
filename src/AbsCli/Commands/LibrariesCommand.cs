using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class LibrariesCommand
{
    public static Command Create()
    {
        var command = new Command("libraries", "Manage libraries");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var serverOption = new Option<string?>("--server", "Server URL override");
        var tokenOption = new Option<string?>("--token", "Token override");

        var command = new Command("list", """
            List all libraries

            Examples:
              abs-cli libraries list
              abs-cli libraries list | jq '.libraries[].name'
            """) { serverOption, tokenOption };

        command.SetHandler(async (string? server, string? token) =>
        {
            var (client, _) = CommandHelper.BuildClient(serverOverride: server, tokenOverride: token);
            var service = new LibrariesService(client);
            var json = await service.ListAsync();
            ConsoleOutput.WriteRawJson(json);
        }, serverOption, tokenOption);

        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id", "Library ID") { IsRequired = true };
        var serverOption = new Option<string?>("--server", "Server URL override");
        var tokenOption = new Option<string?>("--token", "Token override");

        var command = new Command("get", """
            Get a single library

            Examples:
              abs-cli libraries get --id "lib_abc123"
              abs-cli libraries get --id "lib_abc123" | jq '.name'
            """) { idOption, serverOption, tokenOption };

        command.SetHandler(async (string id, string? server, string? token) =>
        {
            var (client, _) = CommandHelper.BuildClient(serverOverride: server, tokenOverride: token);
            var service = new LibrariesService(client);
            var json = await service.GetAsync(id);
            ConsoleOutput.WriteRawJson(json);
        }, idOption, serverOption, tokenOption);

        return command;
    }
}

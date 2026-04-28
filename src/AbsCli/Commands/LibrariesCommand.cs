using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class LibrariesCommand
{
    public static Command Create()
    {
        var command = new Command("libraries", "Manage libraries");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateScanCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var serverOption = new Option<string?>("--server") { Description = "Server URL override" };
        var tokenOption = new Option<string?>("--token") { Description = "Token override" };
        var command = new Command("list", "List all libraries") { serverOption, tokenOption };
        command.AddExamples(
            "abs-cli libraries list",
            "abs-cli libraries list | jq '.libraries[].name'");
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
            "abs-cli libraries get --id \"lib_abc123\"",
            "abs-cli libraries get --id \"lib_abc123\" | jq '.name'");
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
}

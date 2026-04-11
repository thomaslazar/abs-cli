using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class SeriesCommand
{
    public static Command Create()
    {
        var command = new Command("series", "Manage series");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var limitOption = new Option<int?>("--limit", "Results per page");
        var pageOption = new Option<int?>("--page", "Page number (0-indexed)");

        var command = new Command("list", "List series in a library") { libraryOption, limitOption, pageOption };

        command.SetHandler(async (string? library, int? limit, int? page) =>
        {
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SeriesService(client);
            var json = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteRawJson(json);
        }, libraryOption, limitOption, pageOption);

        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id", "Series ID") { IsRequired = true };
        var command = new Command("get", "Get a single series") { idOption };

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var json = await service.GetAsync(id);
            ConsoleOutput.WriteRawJson(json);
        }, idOption);

        return command;
    }
}

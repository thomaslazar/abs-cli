using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class SeriesCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    public static Command Create()
    {
        var command = new Command("series", "Manage series");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Series are derived from book metadata. The scanner removes orphaned",
            "series on its next run. To remove a series, retag the books that",
            "reference it.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50, pass higher value to retrieve more)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var command = new Command("list",
            "List series in a library (defaults to 50 results)")
        { libraryOption, limitOption, pageOption };
        command.AddExamples(
            "abs-cli series list",
            "abs-cli series list --limit 10 --page 0");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(SeriesItem));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new SeriesService(client);
            var result = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Series ID", Required = true };
        var command = new Command("get", "Get a single series") { idOption };
        command.AddExamples(
            "abs-cli series get --id \"se_abc123\"");
        command.AddResponseExample<SeriesItem>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SeriesItem);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Series ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description; empty string clears the field" };
        var command = new Command("update", "Edit a series' name and/or description")
        {
            idOption,
            nameOption,
            descriptionOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to an existing series name does NOT merge — ABS creates a",
            "second series with the duplicate name. Empty --name is rejected;",
            "at least one of --name / --description is required.");
        command.AddExamples(
            "abs-cli series update --id \"se_abc\" --name \"The Stormlight Archive\"",
            "abs-cli series update --id \"se_abc\" --description \"Epic fantasy\"");
        command.AddResponseExample<SeriesItem>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
            }
            var body = BuildUpdateBody(name, description);
            if (body.Count == 0)
            {
                _logger.Error("Specify at least one of --name, --description");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SeriesItem);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body. name is included only when non-empty (empty is
    /// rejected upstream); description is included whenever supplied, so an
    /// empty string clears it server-side. Exposed internally for unit testing.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBody(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description;
        return body;
    }
}

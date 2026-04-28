using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class MetadataCommand
{
    public static Command Create()
    {
        var command = new Command("metadata", "Search metadata providers");
        command.Subcommands.Add(CreateSearchCommand());
        command.Subcommands.Add(CreateProvidersCommand());
        command.Subcommands.Add(CreateCoversCommand());
        return command;
    }

    private static Command CreateSearchCommand()
    {
        var providerOption = new Option<string>("--provider") { Description = "Metadata provider (e.g. audible)", Required = true };
        var titleOption = new Option<string>("--title") { Description = "Book title to search", Required = true };
        var authorOption = new Option<string?>("--author") { Description = "Author name to narrow results" };
        var command = new Command("search", "Search for book metadata from a provider")
        {
            providerOption, titleOption, authorOption
        };
        command.AddExamples(
            "abs-cli metadata search --provider audible --title \"The Way of Kings\"",
            "abs-cli metadata search --provider audible --title \"Mistborn\" --author \"Brandon Sanderson\"");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var provider = parseResult.GetValue(providerOption)!;
            var title = parseResult.GetValue(titleOption)!;
            var author = parseResult.GetValue(authorOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.SearchAsync(provider, title, author);
            ConsoleOutput.WriteRawJson(result);
            return 0;
        });
        return command;
    }

    private static Command CreateProvidersCommand()
    {
        var command = new Command("providers", "List available metadata providers");
        command.AddExamples(
            "abs-cli metadata providers",
            "abs-cli metadata providers | jq '.providers.books[].value'");
        command.AddResponseExample<MetadataProvidersResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.ListProvidersAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.MetadataProvidersResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateCoversCommand()
    {
        var providerOption = new Option<string>("--provider") { Description = "Cover provider", Required = true };
        var titleOption = new Option<string>("--title") { Description = "Book title to search", Required = true };
        var authorOption = new Option<string?>("--author") { Description = "Author name to narrow results" };
        var command = new Command("covers", "Search for book cover images")
        {
            providerOption, titleOption, authorOption
        };
        command.AddExamples(
            "abs-cli metadata covers --provider audible --title \"The Way of Kings\"",
            "abs-cli metadata covers --provider audible --title \"Mistborn\" --author \"Brandon Sanderson\"");
        command.AddResponseExample<CoverSearchResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var provider = parseResult.GetValue(providerOption)!;
            var title = parseResult.GetValue(titleOption)!;
            var author = parseResult.GetValue(authorOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.SearchCoversAsync(provider, title, author);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverSearchResponse);
            return 0;
        });
        return command;
    }
}

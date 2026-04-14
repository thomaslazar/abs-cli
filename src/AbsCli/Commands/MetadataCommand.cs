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
        command.AddCommand(CreateSearchCommand());
        command.AddCommand(CreateProvidersCommand());
        command.AddCommand(CreateCoversCommand());
        return command;
    }

    private static Command CreateSearchCommand()
    {
        var providerOption = new Option<string>("--provider", "Metadata provider (e.g. audible)") { IsRequired = true };
        var titleOption = new Option<string>("--title", "Book title to search") { IsRequired = true };
        var authorOption = new Option<string?>("--author", "Author name to narrow results");
        var command = new Command("search", "Search for book metadata from a provider")
        {
            providerOption, titleOption, authorOption
        };
        command.AddExamples(
            "abs-cli metadata search --provider audible --title \"The Way of Kings\"",
            "abs-cli metadata search --provider audible --title \"Mistborn\" --author \"Brandon Sanderson\"");
        command.SetHandler(async (string provider, string title, string? author) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.SearchAsync(provider, title, author);
            ConsoleOutput.WriteRawJson(result);
        }, providerOption, titleOption, authorOption);
        return command;
    }

    private static Command CreateProvidersCommand()
    {
        var command = new Command("providers", "List available metadata providers");
        command.AddExamples(
            "abs-cli metadata providers",
            "abs-cli metadata providers | jq '.providers.books[].value'");
        command.SetHandler(async () =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.ListProvidersAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.MetadataProvidersResponse);
        });
        return command;
    }

    private static Command CreateCoversCommand()
    {
        var providerOption = new Option<string>("--provider", "Cover provider") { IsRequired = true };
        var titleOption = new Option<string>("--title", "Book title to search") { IsRequired = true };
        var authorOption = new Option<string?>("--author", "Author name to narrow results");
        var command = new Command("covers", "Search for book cover images")
        {
            providerOption, titleOption, authorOption
        };
        command.AddExamples(
            "abs-cli metadata covers --provider audible --title \"The Way of Kings\"",
            "abs-cli metadata covers --provider audible --title \"Mistborn\" --author \"Brandon Sanderson\"");
        command.SetHandler(async (string provider, string title, string? author) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new MetadataService(client);
            var result = await service.SearchCoversAsync(provider, title, author);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverSearchResponse);
        }, providerOption, titleOption, authorOption);
        return command;
    }
}

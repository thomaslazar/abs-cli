using System.CommandLine;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class CacheCommand
{
    public static Command Create()
    {
        var command = new Command("cache", "Manage server-side cache (admin)");
        command.Subcommands.Add(CreatePurgeItemsCommand());
        command.Subcommands.Add(CreatePurgeCommand());
        return command;
    }

    private static Command CreatePurgeItemsCommand()
    {
        var command = new Command("purge-items", "Purge per-item cache backups (admin)");
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli cache purge-items");
        command.AddHelpSection("Caveats",
            "Wipes <MetadataPath>/cache/items/ — encode-m4b backups, embed-metadata --backup copies.",
            "200 does not guarantee deletion — ABS swallows filesystem errors server-side.",
            "Irreversible: cached track originals have no upstream replica once encode-m4b moved them out of the library dir.");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new CacheService(client);
            await service.PurgeItemsAsync();
            return 0;
        });
        return command;
    }

    private static Command CreatePurgeCommand()
    {
        var command = new Command("purge", "Purge all server cache: items, covers, images (admin)");
        command.AddPermissionRequired("admin");
        command.AddExamples(
            "abs-cli cache purge");
        command.AddHelpSection("Caveats",
            "Wipes <MetadataPath>/cache/ in full — cache/items/, cache/covers/, cache/images/.",
            "200 does not guarantee deletion — ABS swallows filesystem errors server-side.",
            "covers/ and images/ rebuild lazily on next request; expect a first-request latency spike.");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new CacheService(client);
            await service.PurgeAsync();
            return 0;
        });
        return command;
    }
}

using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class BackupCommand
{
    public static Command Create()
    {
        var command = new Command("backup", "Manage server backups");
        command.AddCommand(CreateCreateCommand());
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateApplyCommand());
        command.AddCommand(CreateDownloadCommand());
        command.AddCommand(CreateDeleteCommand());
        command.AddCommand(CreateUploadCommand());
        return command;
    }

    private static Command CreateCreateCommand()
    {
        var command = new Command("create", "Create a new server backup");
        command.AddExamples(
            "abs-cli backup create",
            "abs-cli backup create | jq '.backups | length'");
        command.AddResponseExample<BackupListResponse>();

        command.SetHandler(async () =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            var result = await service.CreateAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BackupListResponse);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all server backups");
        command.AddExamples(
            "abs-cli backup list",
            "abs-cli backup list | jq '.backups[] | {id, filename, datePretty}'");
        command.AddResponseExample<BackupListResponse>();

        command.SetHandler(async () =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BackupListResponse);
        });

        return command;
    }

    private static Command CreateApplyCommand()
    {
        var idOption = new Option<string>("--id", "Backup ID") { IsRequired = true };
        var command = new Command("apply", "Apply (restore) a server backup") { idOption };
        command.AddExamples(
            "abs-cli backup apply --id \"2024-01-15T0000\"",
            "abs-cli backup apply --id \"2024-01-15T0000\" | jq '.success'");

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            var result = await service.ApplyAsync(id);
            ConsoleOutput.WriteRawJson(result);
        }, idOption);

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var idOption = new Option<string>("--id", "Backup ID") { IsRequired = true };
        var outputOption = new Option<string>("--output", "Output file path (use .audiobookshelf extension — required for re-upload)") { IsRequired = true };
        var command = new Command("download", "Download a server backup to a local file") { idOption, outputOption };
        command.AddExamples(
            "abs-cli backup download --id \"2024-01-15T0000\" --output backup.audiobookshelf",
            "abs-cli backup download --id \"2024-01-15T0000\" --output /tmp/abs-backup.audiobookshelf");

        command.SetHandler(async (string id, string output) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            await service.DownloadAsync(id, output);
        }, idOption, outputOption);

        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id", "Backup ID") { IsRequired = true };
        var command = new Command("delete", "Delete a server backup") { idOption };
        command.AddExamples(
            "abs-cli backup delete --id \"2024-01-15T0000\"",
            "abs-cli backup delete --id \"2024-01-15T0000\" | jq '.backups | length'");
        command.AddResponseExample<BackupListResponse>();

        command.SetHandler(async (string id) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            var result = await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BackupListResponse);
        }, idOption);

        return command;
    }

    private static Command CreateUploadCommand()
    {
        var fileOption = new Option<string>("--file", "Path to backup file (must have .audiobookshelf extension)") { IsRequired = true };
        var command = new Command("upload", "Upload a backup file to the server") { fileOption };
        command.AddExamples(
            "abs-cli backup upload --file backup.audiobookshelf",
            "abs-cli backup upload --file /tmp/abs-backup.audiobookshelf | jq '.backups | length'");
        command.AddResponseExample<BackupListResponse>();

        command.SetHandler(async (string file) =>
        {
            if (!File.Exists(file))
            {
                ConsoleOutput.WriteError($"File not found: {file}");
                Environment.Exit(1);
                return;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new BackupService(client);
            var result = await service.UploadAsync(file);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.BackupListResponse);
        }, fileOption);

        return command;
    }
}

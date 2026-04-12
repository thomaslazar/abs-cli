using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class TasksCommand
{
    public static Command Create()
    {
        var command = new Command("tasks", "Manage server tasks");
        command.AddCommand(CreateListCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List active and recent tasks");
        command.AddExamples(
            "abs-cli tasks list",
            "abs-cli tasks list | jq '.tasks[] | select(.isFinished==false)'",
            "abs-cli tasks list | jq '.tasks[] | {action, title, isFinished}'");
        command.SetHandler(async () =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new TasksService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TaskListResponse);
        });
        return command;
    }
}

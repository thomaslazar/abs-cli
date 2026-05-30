using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class MeCommand
{
    public static Command Create()
    {
        var command = new Command("me", "Show the currently authenticated user");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Includes the full mediaProgress array — can be MB-size on",
            "power users. Server has no slim variant for this endpoint.");
        command.AddExamples("abs-cli me");
        command.AddResponseExample<Me>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new MeService(client);
            var result = await service.GetAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Me);
            return 0;
        });
        return command;
    }
}

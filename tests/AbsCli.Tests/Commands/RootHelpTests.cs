using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Output;
using Xunit;

namespace AbsCli.Tests.Commands;

public class RootHelpTests
{
    private static string RenderHelp(params string[] args)
    {
        var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");
        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable debug-level logging (HTTP requests, token refresh, version check) to stderr."
        };
        var logJsonOption = new Option<bool>("--log-json")
        {
            Description = "Emit stderr log lines as single-line JSON instead of text."
        };
        rootCommand.Options.Add(debugOption);
        rootCommand.Options.Add(logJsonOption);
        rootCommand.Subcommands.Add(LibrariesCommand.Create());
        rootCommand.AddHelpSection("Environment variables",
            "ABS_DEBUG=1   Same as --debug. Enables debug-level logging to stderr.");
        rootCommand.UseCustomHelpSections();

        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var actualArgs = args.Concat(new[] { "--help" }).ToArray();
        rootCommand.Parse(actualArgs).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Root_Help_Shows_DebugOption_WithDescription()
    {
        var output = RenderHelp();
        Assert.Contains("--debug", output);
        Assert.Contains("Enable debug-level logging", output);
    }

    [Fact]
    public void Root_Help_Shows_LogJsonOption_WithDescription()
    {
        var output = RenderHelp();
        Assert.Contains("--log-json", output);
        Assert.Contains("single-line JSON", output);
    }

    [Fact]
    public void Root_Help_Shows_EnvironmentVariablesSection_WithAbsDebug()
    {
        var output = RenderHelp();
        Assert.Contains("Environment variables", output);
        Assert.Contains("ABS_DEBUG=1", output);
    }

    [Fact]
    public void Root_Help_Shows_Both_Options_Together()
    {
        var output = RenderHelp();
        Assert.Contains("--debug", output);
        Assert.Contains("--log-json", output);
        Assert.Contains("ABS_DEBUG=1", output);
    }
}

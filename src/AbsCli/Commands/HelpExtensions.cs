using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;

namespace AbsCli.Commands;

/// <summary>
/// Adds an "Examples" section to help output for commands.
/// </summary>
public static class HelpExtensions
{
    private static readonly Dictionary<Command, string[]> CommandExamples = new();

    /// <summary>
    /// Register examples for a command (displayed as a separate help section).
    /// </summary>
    public static void AddExamples(this Command command, params string[] examples)
    {
        CommandExamples[command] = examples;
    }

    /// <summary>
    /// Get registered examples for a command (used by tests).
    /// </summary>
    public static string[]? GetExamples(this Command command)
    {
        return CommandExamples.TryGetValue(command, out var examples) ? examples : null;
    }

    /// <summary>
    /// Configure the CommandLineBuilder to render examples sections in help output.
    /// </summary>
    public static CommandLineBuilder UseExamplesHelp(this CommandLineBuilder builder)
    {
        builder.UseHelp(ctx =>
        {
            ctx.HelpBuilder.CustomizeLayout(_ =>
                HelpBuilder.Default.GetLayout()
                    .Append(helpCtx => WriteExamples(helpCtx)));
        });
        return builder;
    }

    private static void WriteExamples(HelpContext ctx)
    {
        if (ctx.Command is Command command &&
            CommandExamples.TryGetValue(command, out var examples) &&
            examples.Length > 0)
        {
            ctx.Output.WriteLine("Examples:");
            foreach (var example in examples)
            {
                ctx.Output.WriteLine($"  {example}");
            }
            ctx.Output.WriteLine();
        }
    }
}

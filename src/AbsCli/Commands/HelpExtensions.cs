using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;

namespace AbsCli.Commands;

/// <summary>
/// Adds custom sections (Examples, Filter groups, Sort fields, etc.) to help output.
/// Sections render after the default Options section, in registration order.
/// </summary>
public static class HelpExtensions
{
    private static readonly Dictionary<Command, List<(string Title, string[] Lines)>> CommandSections = new();

    /// <summary>
    /// Add a named section to a command's help output.
    /// </summary>
    public static void AddHelpSection(this Command command, string title, params string[] lines)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
        {
            sections = new List<(string, string[])>();
            CommandSections[command] = sections;
        }
        sections.Add((title, lines));
    }

    /// <summary>
    /// Shorthand for adding an "Examples" section.
    /// </summary>
    public static void AddExamples(this Command command, params string[] examples)
    {
        command.AddHelpSection("Examples", examples);
    }

    /// <summary>
    /// Get the number of examples registered for a command (used by tests).
    /// </summary>
    public static int GetExampleCount(this Command command)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
            return 0;
        return sections
            .Where(s => s.Title == "Examples")
            .SelectMany(s => s.Lines)
            .Count();
    }

    /// <summary>
    /// Configure the CommandLineBuilder to render custom help sections.
    /// </summary>
    public static CommandLineBuilder UseCustomHelpSections(this CommandLineBuilder builder)
    {
        builder.UseHelp(ctx =>
        {
            ctx.HelpBuilder.CustomizeLayout(_ =>
                HelpBuilder.Default.GetLayout()
                    .Append(helpCtx => WriteSections(helpCtx)));
        });
        return builder;
    }

    private static void WriteSections(HelpContext ctx)
    {
        if (ctx.Command is not Command command)
            return;
        if (!CommandSections.TryGetValue(command, out var sections))
            return;

        foreach (var (title, lines) in sections)
        {
            ctx.Output.WriteLine($"{title}:");
            foreach (var line in lines)
            {
                ctx.Output.WriteLine($"  {line}");
            }
            ctx.Output.WriteLine();
        }
    }
}

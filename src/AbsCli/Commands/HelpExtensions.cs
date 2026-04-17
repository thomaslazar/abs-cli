using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;

namespace AbsCli.Commands;

public enum HelpSectionPosition { Top, Bottom }

/// <summary>
/// Adds custom sections (Notes, Examples, Filter groups, etc.) to help output.
/// Top-positioned sections render before the default layout; Bottom-positioned
/// sections render after Options in registration order.
/// </summary>
public static class HelpExtensions
{
    private record Section(string Title, string[] Lines, HelpSectionPosition Position);

    private static readonly Dictionary<Command, List<Section>> CommandSections = new();

    public static void AddHelpSection(this Command command, string title, params string[] lines)
        => command.AddHelpSection(title, HelpSectionPosition.Bottom, lines);

    public static void AddHelpSection(this Command command, string title, HelpSectionPosition position, params string[] lines)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
        {
            sections = new List<Section>();
            CommandSections[command] = sections;
        }
        sections.Add(new Section(title, lines, position));
    }

    public static void AddExamples(this Command command, params string[] examples)
        => command.AddHelpSection("Examples", HelpSectionPosition.Bottom, examples);

    public static int GetExampleCount(this Command command)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
            return 0;
        return sections
            .Where(s => s.Title == "Examples")
            .SelectMany(s => s.Lines)
            .Count();
    }

    public static CommandLineBuilder UseCustomHelpSections(this CommandLineBuilder builder)
    {
        builder.UseHelp(ctx =>
        {
            ctx.HelpBuilder.CustomizeLayout(_ =>
            {
                var defaultLayout = HelpBuilder.Default.GetLayout().ToList();
                var withTop = new List<HelpSectionDelegate> { helpCtx => WriteSections(helpCtx, HelpSectionPosition.Top) };
                withTop.AddRange(defaultLayout);
                withTop.Add(helpCtx => WriteSections(helpCtx, HelpSectionPosition.Bottom));
                return withTop;
            });
        });
        return builder;
    }

    private static void WriteSections(HelpContext ctx, HelpSectionPosition position)
    {
        if (!CommandSections.TryGetValue(ctx.Command, out var sections)) return;

        foreach (var section in sections.Where(s => s.Position == position))
        {
            ctx.Output.WriteLine($"{section.Title}:");
            foreach (var line in section.Lines)
                ctx.Output.WriteLine($"  {line}");
            ctx.Output.WriteLine();
        }
    }
}

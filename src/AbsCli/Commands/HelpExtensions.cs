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

    public static void AddResponseExample<T>(this Command command)
        => AddResponseExampleSection(command, ResponseExamples.For(typeof(T)));

    /// <summary>
    /// Appends two extra sections describing the concrete shapes of
    /// <c>LibraryItemMinified.media</c>. The main response-shape sample emits a
    /// placeholder there because the field is a union of book and podcast.
    /// Call after <see cref="AddResponseExample{T}"/> whenever the command
    /// surface includes a library item.
    /// </summary>
    public static void AddMediaUnionShapes(this Command command)
    {
        command.AddHelpSection(
            "Book media shape (when mediaType is \"book\")",
            HelpSectionPosition.Bottom,
            ResponseExamples.For(typeof(AbsCli.Models.BookMediaMinified)).Split('\n'));
        command.AddHelpSection(
            "Podcast media shape (when mediaType is \"podcast\")",
            HelpSectionPosition.Bottom,
            ResponseExamples.For(typeof(AbsCli.Models.PodcastMedia)).Split('\n'));
    }

    /// <summary>
    /// Registers a response-shape sample for a paginated envelope whose
    /// <c>results</c> array is typed as <c>List&lt;JsonElement&gt;</c>. The
    /// element sample is spliced into the envelope's results array.
    /// </summary>
    public static void AddResponseExample(this Command command, Type envelopeType, Type elementType)
    {
        var envelopeJson = ResponseExamples.For(envelopeType);
        var elementJson = ResponseExamples.For(elementType);
        var spliced = SpliceResultsArray(envelopeJson, elementJson);
        AddResponseExampleSection(command, spliced);
    }

    private static void AddResponseExampleSection(Command command, string json)
        => command.AddHelpSection("Response shape", HelpSectionPosition.Bottom, json.Split('\n'));

    private static string SpliceResultsArray(string envelopeJson, string elementJson)
    {
        var lines = envelopeJson.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("\"results\":", StringComparison.Ordinal)) continue;

            var indent = new string(' ', lines[i].Length - trimmed.Length);

            int closeIdx = i;
            while (closeIdx < lines.Length &&
                   !lines[closeIdx].TrimStart().StartsWith("]", StringComparison.Ordinal))
                closeIdx++;
            if (closeIdx == lines.Length) break;

            var elementIndented = string.Join("\n",
                elementJson.Split('\n').Select(l => indent + "  " + l));
            var trailing = lines[closeIdx].TrimStart().StartsWith("],", StringComparison.Ordinal) ? "]," : "]";

            var output = new List<string>();
            output.AddRange(lines.Take(i));
            output.Add($"{indent}\"results\": [");
            output.Add(elementIndented);
            output.Add($"{indent}{trailing}");
            output.AddRange(lines.Skip(closeIdx + 1));
            return string.Join('\n', output);
        }
        return envelopeJson;
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

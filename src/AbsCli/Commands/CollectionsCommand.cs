using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class CollectionsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static Command Create()
    {
        var command = new Command("collections", "Manage collections (curated lists of book library items)");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Collections are flat, manually-curated, library-scoped ordered lists",
            "of book library items. ABS has no smart-collection / saved-filter",
            "concept — membership is yours to maintain. See `collections create",
            "--help` for sharp edges; `update` edits metadata, `reorder` shuffles",
            "order, `add` / `remove` / `batch-*` change membership.");
        // Subcommands added in subsequent tasks.
        return command;
    }

    /// <summary>
    /// Build the PATCH body honouring tri-state semantics: null = field
    /// absent (omit from JSON), empty string = clear (send JSON null),
    /// non-empty = set value. Exposed internally for unit testing.
    /// Mirrors <c>AuthorsCommand.BuildUpdateBodyForTesting</c>.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBodyForTesting(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description == "" ? null! : description;
        return body;
    }
}

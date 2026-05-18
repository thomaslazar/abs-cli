using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsGetExpandedCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void ItemsGet_Help_ListsExpandedFlag()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("--id", output);
        Assert.Contains("--expanded", output);
    }

    [Fact]
    public void ItemsGet_Help_ShowsBothResponseShapes()
    {
        var output = RenderHelp("items", "get");
        // Default (minified) — has `numFiles`, no `libraryFiles`.
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"numFiles\"", output);
        // Expanded — has `libraryFiles`, `lastScan`, `scanVersion`.
        Assert.Contains("Response shape (--expanded):", output);
        Assert.Contains("\"libraryFiles\"", output);
        Assert.Contains("\"lastScan\"", output);
        Assert.Contains("\"scanVersion\"", output);
    }

    [Fact]
    public void ItemsGet_Help_HasExpandedExample()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("--expanded", output);
        Assert.Contains("items get --id \"li_abc123\" --expanded", output);
    }

    [Fact]
    public void ItemsGet_Help_NoCrossReferenceToOtherCommands()
    {
        // items get's help should describe items get only — no Caveats
        // block that points at other commands. The expanded-vs-minified
        // distinction is fully covered by the two response-shape blocks.
        var output = RenderHelp("items", "get");
        Assert.DoesNotContain("toggle-ebook-status", output);
        Assert.DoesNotContain("Caveats", output);
    }
}

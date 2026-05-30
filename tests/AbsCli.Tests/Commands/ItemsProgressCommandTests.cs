using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsProgressCommandTests
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
    public void Progress_HasThreeSubcommands()
    {
        var progress = ItemsCommand.Create().Subcommands.First(c => c.Name == "progress");
        var verbs = progress.Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "get", "set", "remove" }, verbs);
    }

    [Fact]
    public void ProgressGet_Help_DocumentsLibraryItemAndBooksOnly()
    {
        var output = RenderHelp("items", "progress", "get");
        Assert.Contains("--library-item", output);
        Assert.Contains("Books only", output);
    }

    [Fact]
    public void ProgressSet_Help_DocumentsAllBodyFlags()
    {
        var output = RenderHelp("items", "progress", "set");
        Assert.Contains("--library-item", output);
        Assert.Contains("--current-time", output);
        Assert.Contains("--is-finished", output);
        Assert.Contains("--ebook-location", output);
        Assert.Contains("--ebook-progress", output);
        Assert.Contains("--hide-from-continue-listening", output);
        Assert.Contains("--finished-at", output);
    }

    [Fact]
    public void ProgressSet_Help_DocumentsFinishedAtBackdatingConstraint()
    {
        var output = RenderHelp("items", "progress", "set");
        Assert.Contains("--finished-at", output);
        Assert.Contains("--is-finished true", output);
        Assert.Contains("backdating", output);
    }

    [Fact]
    public void ProgressRemove_Help_DocumentsBothHalvesWipe()
    {
        var output = RenderHelp("items", "progress", "remove");
        Assert.Contains("both audio and ebook", output);
        Assert.Contains("To reset only one half", output);
    }

    [Fact]
    public void BatchUpdateProgress_Help_DocumentsSilentFailure()
    {
        var output = RenderHelp("items", "batch-update-progress");
        Assert.Contains("200 even when individual entries fail", output);
        Assert.Contains("No per-entry feedback", output);
    }

    [Fact]
    public void ItemsGet_Help_DocumentsIncludeAutoImpliesExpanded()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("--include", output);
        Assert.Contains("automatically implies --expanded", output);
    }

    [Fact]
    public void ItemsGet_Help_DocumentsIncludeValues()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("progress", output);
        Assert.Contains("rssfeed", output);
        Assert.Contains("share", output);
        Assert.Contains("downloads", output);
    }
}

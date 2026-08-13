using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class PlaylistsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(PlaylistsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    private static string RenderFullHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(PlaylistsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Playlists_HasAllElevenSubcommands()
    {
        var verbs = PlaylistsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[]
        {
            "list", "get", "create", "update", "reorder", "delete",
            "add", "remove", "batch-add", "batch-remove", "create-from-collection"
        }, verbs);
    }

    [Fact]
    public void PlaylistsList_Help_DocumentsFlags()
    {
        var output = RenderHelp("playlists", "list");
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
    }

    [Fact]
    public void PlaylistsCreate_Help_DocumentsEmptyAllowed()
    {
        var output = RenderHelp("playlists", "create");
        Assert.Contains("empty playlist", output);
    }

    [Fact]
    public void PlaylistsRemove_Help_DocumentsAutoDelete()
    {
        var output = RenderHelp("playlists", "remove");
        Assert.Contains("last item", output);
        Assert.Contains("deletes the playlist", output);
    }

    [Fact]
    public void PlaylistsReorder_Help_DocumentsFullMembership()
    {
        var output = RenderHelp("playlists", "reorder");
        Assert.Contains("FULL current membership", output);
    }

    [Fact]
    public void PlaylistsCreateFromCollection_Help_DocumentsSnapshot()
    {
        var output = RenderHelp("playlists", "create-from-collection");
        Assert.Contains("--collection", output);
        Assert.Contains("snapshot", output);
    }

    [Fact]
    public void Playlists_NoSubcommand_DeclaresPermission()
    {
        foreach (var sub in PlaylistsCommand.Create().Subcommands)
        {
            var help = RenderHelp("playlists", sub.Name);
            Assert.DoesNotContain("Permission required", help);
        }
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("batch-add")]
    [InlineData("batch-remove")]
    public void BooksToItems_Help_DocumentsAsymmetryAndRequestShape(string verb)
    {
        var output = RenderFullHelp("playlists", verb);
        Assert.Contains("sends ABS's `items` shape", output);
        var requestIdx = output.IndexOf("Request shape:", StringComparison.Ordinal);
        Assert.True(requestIdx >= 0, "missing Request shape section");
        var afterRequest = output[requestIdx..];
        var responseIdx = afterRequest.IndexOf("Response shape:", StringComparison.Ordinal);
        var requestSection = responseIdx >= 0 ? afterRequest[..responseIdx] : afterRequest;
        // The CLI's documented input contract is `books`, not ABS's wire `items` —
        // this is the exact asymmetry the help note calls out, so the Request
        // shape block must show `books` and never `items`.
        Assert.Contains("\"books\"", requestSection);
        Assert.DoesNotContain("\"items\"", requestSection);
    }
}

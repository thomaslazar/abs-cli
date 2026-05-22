using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class CollectionsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(CollectionsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Collections_HasAllTenSubcommands()
    {
        var verbs = CollectionsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[]
        {
            "list", "get", "create", "update", "reorder",
            "delete", "add", "remove", "batch-add", "batch-remove"
        }, verbs);
    }

    [Fact]
    public void CollectionsList_Help_DocumentsFlags()
    {
        var output = RenderHelp("collections", "list");
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
        Assert.Contains("--include", output);
    }

    [Fact]
    public void CollectionsList_Help_DocumentsInertEcho()
    {
        var output = RenderHelp("collections", "list");
        // From spec --help text constraint: "sortBy / sortDesc / filterBy /
        // minified … inert."
        Assert.Contains("inert", output);
    }

    [Fact]
    public void CollectionsCreate_Help_DocumentsAtLeastOneBookAndHtmlStripping()
    {
        var output = RenderHelp("collections", "create");
        Assert.Contains("at least one book", output);
        Assert.Contains("HTML in --name is stripped", output);
    }

    [Fact]
    public void CollectionsUpdate_Help_DocumentsEditOnlyAndPointsToReorder()
    {
        var output = RenderHelp("collections", "update");
        Assert.Contains("Edits metadata only", output);
        Assert.Contains("reorder", output);
    }

    [Fact]
    public void CollectionsUpdate_Help_DocumentsTriStateDescription()
    {
        var output = RenderHelp("collections", "update");
        Assert.Contains("Empty string for --description clears", output);
    }

    [Fact]
    public void CollectionsReorder_Help_SaysReorderOnly()
    {
        var output = RenderHelp("collections", "reorder");
        Assert.Contains("Reorders existing members only", output);
        Assert.Contains("does not add or remove", output);
    }

    [Fact]
    public void CollectionsAdd_Help_DocumentsDuplicateBehavior()
    {
        var output = RenderHelp("collections", "add");
        Assert.Contains("400", output);
        Assert.Contains("already in the collection", output);
    }

    [Fact]
    public void CollectionsBatchAdd_Help_DocumentsSilentSkipAndCrossLibrary()
    {
        var output = RenderHelp("collections", "batch-add");
        Assert.Contains("Silently skips", output);
        Assert.Contains("different library", output);
    }

    [Fact]
    public void CollectionsUpdate_BuildBody_OmitsNullKeys()
    {
        var body = CollectionsCommand.BuildUpdateBodyForTesting(name: "X", description: null);
        Assert.Single(body);
        Assert.Equal("X", body["name"]);
    }

    [Fact]
    public void CollectionsUpdate_BuildBody_ClearsOnEmptyString()
    {
        var body = CollectionsCommand.BuildUpdateBodyForTesting(name: null, description: "");
        Assert.Single(body);
        Assert.Null(body["description"]); // null = JSON null on the wire
    }

    [Fact]
    public void CollectionsUpdate_BuildBody_SetsBothWhenProvided()
    {
        var body = CollectionsCommand.BuildUpdateBodyForTesting(name: "n", description: "d");
        Assert.Equal(2, body.Count);
        Assert.Equal("n", body["name"]);
        Assert.Equal("d", body["description"]);
    }
}

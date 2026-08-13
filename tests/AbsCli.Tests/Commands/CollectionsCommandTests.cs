using System.CommandLine;
using System.Text.Json;
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
    public void CollectionsUpdate_Help_DocumentsTriStateDescription()
    {
        // The tri-state semantics live in the flag descriptions (rendered
        // in help), not a separate Notes block.
        var output = RenderHelp("collections", "update");
        Assert.Contains("empty string clears the field", output);
    }

    [Fact]
    public void CollectionsReorder_Help_DocumentsFullMembership()
    {
        var output = RenderHelp("collections", "reorder");
        Assert.Contains("FULL current membership", output);
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
        var body = CollectionsCommand.BuildUpdateBody(name: "X", description: null);
        Assert.Single(body);
        Assert.Equal("X", body["name"]);
    }

    [Fact]
    public void CollectionsUpdate_BuildBody_ClearsOnEmptyString()
    {
        var body = CollectionsCommand.BuildUpdateBody(name: null, description: "");
        Assert.Single(body);
        Assert.Null(body["description"]); // null = JSON null on the wire
    }

    [Fact]
    public void CollectionsUpdate_BuildBody_SetsBothWhenProvided()
    {
        var body = CollectionsCommand.BuildUpdateBody(name: "n", description: "d");
        Assert.Equal(2, body.Count);
        Assert.Equal("n", body["name"]);
        Assert.Equal("d", body["description"]);
    }

    [Fact]
    public void CreateBooks_Valid_ReturnsFilteredList()
    {
        var books = CollectionsCommand.PrepareCreateBooks("{\"books\":[\"li_a\",\"li_b\"]}");
        Assert.Equal(new[] { "li_a", "li_b" }, books);
    }

    [Fact]
    public void CreateBooks_DropsEmptyEntries_LikeAbsDoes()
    {
        var books = CollectionsCommand.PrepareCreateBooks("{\"books\":[\"li_a\",\"\",\"li_b\"]}");
        Assert.Equal(new[] { "li_a", "li_b" }, books);
    }

    [Fact]
    public void CreateBooks_Empty_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareCreateBooks("{\"books\":[]}"));
    }

    [Fact]
    public void CreateBooks_OnlyEmptyStrings_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareCreateBooks("{\"books\":[\"\"]}"));
    }

    [Fact]
    public void CreateBooks_MissingField_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareCreateBooks("{}"));
    }

    [Fact]
    public void CreateBooks_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => CollectionsCommand.PrepareCreateBooks("{not json"));
    }

    [Fact]
    public void ReorderBody_Valid_IsForwardedUnchanged()
    {
        const string body = "{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}";
        Assert.Equal(body, CollectionsCommand.PrepareReorderBody(body));
    }

    [Fact]
    public void ReorderBody_Empty_IsAllowed()
    {
        // ABS treats an empty/absent books array as a no-op, not an error.
        const string body = "{\"books\":[]}";
        Assert.Equal(body, CollectionsCommand.PrepareReorderBody(body));
    }

    [Fact]
    public void ReorderBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => CollectionsCommand.PrepareReorderBody("{not json"));
    }

    [Fact]
    public void BatchAddBody_Valid_IsForwardedUnchanged()
    {
        const string body = "{\"books\":[\"li_a\",\"li_b\"]}";
        Assert.Equal(body, CollectionsCommand.PrepareBatchAddBody(body));
    }

    [Fact]
    public void BatchAddBody_Empty_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareBatchAddBody("{\"books\":[]}"));
    }

    [Fact]
    public void BatchAddBody_MissingField_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareBatchAddBody("{}"));
    }

    [Fact]
    public void BatchAddBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => CollectionsCommand.PrepareBatchAddBody("{not json"));
    }

    [Fact]
    public void BatchRemoveBody_Valid_IsForwardedUnchanged()
    {
        const string body = "{\"books\":[\"li_a\",\"li_b\"]}";
        Assert.Equal(body, CollectionsCommand.PrepareBatchRemoveBody(body));
    }

    [Fact]
    public void BatchRemoveBody_Empty_Rejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionsCommand.PrepareBatchRemoveBody("{\"books\":[]}"));
    }

    [Fact]
    public void BatchRemoveBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => CollectionsCommand.PrepareBatchRemoveBody("{not json"));
    }
}

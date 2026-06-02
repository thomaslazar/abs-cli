using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class AuthorsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(AuthorsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Authors_HasAllSevenSubcommands()
    {
        var verbs = AuthorsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "match", "lookup", "update", "delete", "image" }, verbs);
    }

    [Fact]
    public void AuthorsMatch_Help_DocumentsArgs()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("--id", output);
        Assert.Contains("--name", output);
        Assert.Contains("--asin", output);
        Assert.Contains("--region", output);
    }

    [Fact]
    public void AuthorsMatch_Help_DocumentsDestructiveAndAmbiguityCaveats()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("Destructive", output);
        Assert.Contains("Levenshtein", output);
        Assert.Contains("ASIN", output);
    }

    [Fact]
    public void AuthorsMatch_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"updated\"", output);
        Assert.Contains("\"author\"", output);
    }

    [Fact]
    public void AuthorsLookup_Help_DocumentsNameOnly()
    {
        var output = RenderHelp("authors", "lookup");
        Assert.Contains("--name", output);
        Assert.DoesNotContain("--id", output);
        Assert.DoesNotContain("--asin", output);
        Assert.DoesNotContain("--region", output);
    }

    [Fact]
    public void AuthorsLookup_Help_DocumentsReadOnlyAndNullCaveats()
    {
        var output = RenderHelp("authors", "lookup");
        Assert.Contains("Read-only", output);
        Assert.Contains("null", output);
    }

    [Fact]
    public void AuthorsUpdate_Help_DocumentsAllThreeFields()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("--id", output);
        Assert.Contains("--name", output);
        Assert.Contains("--description", output);
        Assert.Contains("--asin", output);
    }

    [Fact]
    public void AuthorsUpdate_Help_DocumentsMergeOnRenameCaveat()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("merge", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rename", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorsUpdate_Help_DocumentsClearSemantics()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("Empty string", output);
        Assert.Contains("clear", output);
    }

    [Fact]
    public void AuthorsUpdate_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"updated\"", output);
        Assert.Contains("\"author\"", output);
    }

    [Theory]
    [InlineData("Foo", null, null, "{\"name\": \"Foo\"}")]
    [InlineData(null, "", null, "{\"description\": null}")]
    [InlineData(null, null, "", "{\"asin\": null}")]
    [InlineData(null, "Bio", null, "{\"description\": \"Bio\"}")]
    [InlineData("Foo", "", "", "{\"name\": \"Foo\",\"description\": null,\"asin\": null}")]
    public void AuthorsUpdate_BuildBody_TriState(string? name, string? description, string? asin, string expected)
    {
        var body = AuthorsCommand.BuildUpdateBodyForTesting(name, description, asin);
        var json = System.Text.Json.JsonSerializer.Serialize(
            body, AbsCli.Models.AppJsonContext.Default.DictionaryStringString);
        var expectedFragments = expected.Trim('{', '}').Split(',')
            .Where(f => !string.IsNullOrEmpty(f)).ToList();
        Assert.Equal(expectedFragments.Count, body.Count);
        foreach (var fragment in expectedFragments)
            Assert.Contains(fragment, json);
    }

    [Fact]
    public void AuthorsDelete_Help_RequiresIdOnly()
    {
        var output = RenderHelp("authors", "delete");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--name", output);
        Assert.DoesNotContain("--asin", output);
        Assert.DoesNotContain("--description", output);
    }

    [Fact]
    public void AuthorsDelete_Help_DocumentsHardDeleteCaveat()
    {
        var output = RenderHelp("authors", "delete");
        Assert.Contains("removes the author from all books", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorsList_Help_DocumentsPaginationFlags()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
        Assert.Contains("--sort", output);
        Assert.Contains("--desc", output);
    }

    [Fact]
    public void AuthorsList_Help_ShowsPaginatedResponseShape()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"total\"", output);
        Assert.Contains("\"limit\"", output);
        Assert.Contains("\"page\"", output);
    }
}

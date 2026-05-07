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
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Authors_TopLevel_Help_ListsAllSixVerbs()
    {
        var output = RenderHelp("authors");
        Assert.Contains("list", output);
        Assert.Contains("get", output);
        Assert.Contains("match", output);
        Assert.Contains("lookup", output);
        Assert.Contains("update", output);
        Assert.Contains("delete", output);
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
}

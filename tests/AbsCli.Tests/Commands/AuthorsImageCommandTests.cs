using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class AuthorsImageCommandTests
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
    public void AuthorsImage_HasThreeSubcommands()
    {
        var authors = AuthorsCommand.Create();
        var image = authors.Subcommands.First(c => c.Name == "image");
        var verbs = image.Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "set", "get", "remove" }, verbs);
    }

    [Fact]
    public void AuthorsImageSet_Help_DocumentsUrlOnly()
    {
        var output = RenderHelp("authors", "image", "set");
        Assert.Contains("--id", output);
        Assert.Contains("--url", output);
        Assert.DoesNotContain("--file", output);
        Assert.DoesNotContain("--server-path", output);
    }

    [Fact]
    public void AuthorsImageSet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "image", "set");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"author\"", output);
    }

    [Fact]
    public void AuthorsImageGet_Help_DocumentsOutputAndRaw()
    {
        var output = RenderHelp("authors", "image", "get");
        Assert.Contains("--id", output);
        Assert.Contains("--output", output);
        Assert.Contains("--raw", output);
    }

    [Fact]
    public void AuthorsImageRemove_Help_RequiresIdOnly()
    {
        var output = RenderHelp("authors", "image", "remove");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--url", output);
        Assert.DoesNotContain("--output", output);
    }

    [Fact]
    public void AuthorsImageRemove_Help_DocumentsNoCurrentImageQuirk()
    {
        var output = RenderHelp("authors", "image", "remove");
        Assert.Contains("No current image", output);
        Assert.Contains("Bad request", output);
    }
}

using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsCoverCommandTests
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
    public void Cover_TopLevel_Help_ListsThreeVerbs()
    {
        var output = RenderHelp("items", "cover");
        Assert.Contains("set", output);
        Assert.Contains("get", output);
        Assert.Contains("remove", output);
    }

    [Fact]
    public void CoverSet_Help_ListsAllThreeSourceFlags()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("--url", output);
        Assert.Contains("--file", output);
        Assert.Contains("--server-path", output);
    }

    [Fact]
    public void CoverSet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"success\"", output);
        Assert.Contains("\"cover\"", output);
    }

    [Fact]
    public void CoverGet_Help_DocumentsOutputAndRaw()
    {
        var output = RenderHelp("items", "cover", "get");
        Assert.Contains("--output", output);
        Assert.Contains("--raw", output);
    }

    [Fact]
    public void CoverGet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "get");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"path\"", output);
        Assert.Contains("\"bytes\"", output);
    }

    [Fact]
    public void CoverRemove_Help_RequiresIdOnly()
    {
        var output = RenderHelp("items", "cover", "remove");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--url", output);
        Assert.DoesNotContain("--file", output);
    }
}

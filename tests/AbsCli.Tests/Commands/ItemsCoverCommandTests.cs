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
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Cover_TopLevel_Help_ListsFourVerbs()
    {
        var output = RenderHelp("items", "cover");
        Assert.Contains("set", output);
        Assert.Contains("link", output);
        Assert.Contains("get", output);
        Assert.Contains("remove", output);
    }

    [Fact]
    public void CoverSet_Help_ListsUrlAndFileOnly()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("--url", output);
        Assert.Contains("--file", output);
        Assert.DoesNotContain("--server-path", output);
    }

    [Fact]
    public void CoverSet_Help_RequiresUpdateAndUpload()
    {
        Assert.Contains("Permission required:\n  update, upload", RenderHelp("items", "cover", "set").Replace("\r\n", "\n"));
    }

    [Fact]
    public void CoverLink_Help_RequiresUpdateAndDocumentsPathRules()
    {
        var output = RenderHelp("items", "cover", "link").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  update", output);
        Assert.DoesNotContain("upload", output.Split("Options:")[0]);
        Assert.Contains("--path", output);
        Assert.Contains("library files", output);
        Assert.Contains("Invalid cover path", output);
        Assert.Contains("/metadata/items/", output);
    }

    [Fact]
    public void CoverLink_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "link");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"cover\"", output);
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

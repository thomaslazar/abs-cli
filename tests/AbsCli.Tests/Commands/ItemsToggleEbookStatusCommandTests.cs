using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsToggleEbookStatusCommandTests
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
    public void ToggleEbookStatus_Help_ListsFlags()
    {
        var output = RenderHelp("items", "toggle-ebook-status");
        Assert.Contains("--id", output);
        Assert.Contains("--ino", output);
    }

    [Fact]
    public void ToggleEbookStatus_Help_RendersPermissionRequired()
    {
        var output = RenderHelp("items", "toggle-ebook-status");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void ToggleEbookStatus_Help_SurfacesCaveats()
    {
        var output = RenderHelp("items", "toggle-ebook-status");
        Assert.Contains("Caveats", output);
        Assert.Contains("targeting a supplementary makes it primary", output);
        Assert.Contains("targeting the current primary unsets it", output);
        Assert.Contains("libraryFiles[].ino", output);
    }

    [Fact]
    public void ToggleEbookStatus_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "toggle-ebook-status");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"libraryItemId\"", output);
        Assert.Contains("\"fileIno\"", output);
        Assert.Contains("\"action\"", output);
        Assert.Contains("\"toggled\"", output);
    }
}

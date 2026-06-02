using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsEncodeM4bCommandTests
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
    public void EncodeM4b_TopLevel_Help_ListsTwoVerbs()
    {
        var output = RenderHelp("items", "encode-m4b");
        Assert.Contains("start", output);
        Assert.Contains("cancel", output);
    }

    [Fact]
    public void EncodeM4bStart_Help_ListsAllFlags()
    {
        var output = RenderHelp("items", "encode-m4b", "start");
        Assert.Contains("--id", output);
        Assert.Contains("--codec", output);
        Assert.Contains("--bitrate", output);
        Assert.Contains("--channels", output);
    }

    [Fact]
    public void EncodeM4bStart_Help_DocumentsEnumValues()
    {
        var output = RenderHelp("items", "encode-m4b", "start");
        Assert.Contains("copy", output);
        Assert.Contains("aac", output);
        Assert.Contains("opus", output);
        Assert.Contains("128k", output);
    }

    [Fact]
    public void EncodeM4bStart_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "encode-m4b", "start");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"libraryItemId\"", output);
        Assert.Contains("\"action\"", output);
        Assert.Contains("\"started\"", output);
        Assert.Contains("\"options\"", output);
    }

    [Fact]
    public void EncodeM4bStart_Help_SurfacesCaveats()
    {
        var output = RenderHelp("items", "encode-m4b", "start");
        Assert.Contains("Caveats", output);
        Assert.Contains("Fire-and-forget", output);
        Assert.Contains("No concurrency", output);
    }

    [Fact]
    public void EncodeM4bCancel_Help_RequiresIdOnly()
    {
        var output = RenderHelp("items", "encode-m4b", "cancel");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--codec", output);
        Assert.DoesNotContain("--bitrate", output);
        Assert.DoesNotContain("--channels", output);
    }

    [Fact]
    public void EncodeM4bCancel_Help_FlagsThe404Ambiguity()
    {
        var output = RenderHelp("items", "encode-m4b", "cancel");
        Assert.Contains("Caveats", output);
        Assert.Contains("404", output);
    }
}

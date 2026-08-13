using System.CommandLine;
using System.Text.Json;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsChaptersCommandTests
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
    public void Chapters_TopLevel_Help_ListsTwoVerbs()
    {
        var output = RenderHelp("items", "chapters");
        Assert.Contains("lookup", output);
        Assert.Contains("set", output);
    }

    [Fact]
    public void ChaptersLookup_Help_ListsFlags()
    {
        var output = RenderHelp("items", "chapters", "lookup");
        Assert.Contains("--asin", output);
        Assert.Contains("--region", output);
    }

    [Fact]
    public void ChaptersLookup_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "chapters", "lookup");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"asin\"", output);
        Assert.Contains("\"chapters\"", output);
        Assert.Contains("\"isAccurate\"", output);
    }

    [Fact]
    public void ChaptersLookup_Help_SurfacesUnitsAndAudnexusCaveats()
    {
        var output = RenderHelp("items", "chapters", "lookup");
        Assert.Contains("Caveats", output);
        Assert.Contains("ms-based", output);
        Assert.Contains("Audnexus", output);
    }

    [Fact]
    public void ChaptersSet_Help_ListsFlags()
    {
        var output = RenderHelp("items", "chapters", "set");
        Assert.Contains("--id", output);
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
    }

    [Fact]
    public void ChaptersSet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "chapters", "set");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"success\"", output);
        Assert.Contains("\"updated\"", output);
    }

    [Fact]
    public void ChaptersSet_Help_SurfacesFileVsAudioFileCaveat()
    {
        var output = RenderHelp("items", "chapters", "set");
        Assert.Contains("Caveats", output);
        Assert.Contains("DB + sidecar", output);
        Assert.Contains("embed-metadata", output);
        Assert.Contains("500", output);
    }

    [Fact]
    public void ChaptersSet_ForwardsOriginalBytes_NotCanonicalised()
    {
        // A body missing "end" must reach ABS unchanged so ABS can 400 it,
        // rather than being silently filled with end: 0.
        const string body = "{\"chapters\":[{\"title\":\"One\",\"start\":0}]}";
        var forwarded = ItemsCommand.PrepareChaptersBody(body);
        Assert.Equal(body, forwarded);
        Assert.DoesNotContain("\"end\"", forwarded);
    }

    [Fact]
    public void ChaptersSet_ValidFullBody_ReturnedUnchanged()
    {
        const string body = "{\"chapters\":[{\"title\":\"One\",\"start\":0,\"end\":10.5}]}";
        var forwarded = ItemsCommand.PrepareChaptersBody(body);
        Assert.Equal(body, forwarded);
    }

    [Fact]
    public void ChaptersSet_MalformedJson_ThrowsJsonException()
    {
        const string body = "{\"chapters\":[{";
        Assert.Throws<JsonException>(() => ItemsCommand.PrepareChaptersBody(body));
    }
}

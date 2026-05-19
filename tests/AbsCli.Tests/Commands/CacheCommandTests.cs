using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class CacheCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(CacheCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Cache_TopLevel_Help_ListsBothVerbs()
    {
        var output = RenderHelp("cache");
        Assert.Contains("purge-items", output);
        Assert.Contains("purge", output);
    }

    [Fact]
    public void CachePurgeItems_Help_ShowsPermissionAndCaveats()
    {
        var output = RenderHelp("cache", "purge-items");
        Assert.Contains("Permission required", output);
        Assert.Contains("admin", output);
        Assert.Contains("Caveats", output);
        Assert.Contains("cache/items/", output);
        Assert.Contains("200 does not guarantee deletion", output);
    }

    [Fact]
    public void CachePurgeItems_Help_HasExample()
    {
        var output = RenderHelp("cache", "purge-items");
        Assert.Contains("abs-cli cache purge-items", output);
    }

    [Fact]
    public void CachePurge_Help_ShowsPermissionAndCaveats()
    {
        var output = RenderHelp("cache", "purge");
        Assert.Contains("Permission required", output);
        Assert.Contains("admin", output);
        Assert.Contains("Caveats", output);
        Assert.Contains("rebuild lazily", output);
        Assert.Contains("200 does not guarantee deletion", output);
    }

    [Fact]
    public void CachePurge_Help_HasExample()
    {
        var output = RenderHelp("cache", "purge");
        Assert.Contains("abs-cli cache purge", output);
    }
}

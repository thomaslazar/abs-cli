using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class SearchCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(SearchCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Search_Help_ShowsOptions()
    {
        var output = RenderHelp("search");
        Assert.Contains("--query", output);
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
    }

    [Fact]
    public void Search_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("search"));
    }

    [Fact]
    public void Search_QueryIsRequired()
    {
        var root = new RootCommand();
        root.Subcommands.Add(SearchCommand.Create());
        var result = root.Parse(new[] { "search" });
        Assert.NotEmpty(result.Errors);
    }
}

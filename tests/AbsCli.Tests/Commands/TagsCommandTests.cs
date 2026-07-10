using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class TagsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(TagsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Tags_HasThreeSubcommands()
    {
        var verbs = TagsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void TagsRename_UsesPositionalArgs()
    {
        var output = RenderHelp("tags", "rename");
        Assert.Contains("old-tag", output);
        Assert.Contains("new-tag", output);
        Assert.DoesNotContain("--old-tag", output);
    }

    [Fact]
    public void TagsRename_Help_DocumentsMerge()
    {
        var output = RenderHelp("tags", "rename");
        Assert.Contains("merge", output.ToLowerInvariant());
    }

    [Fact]
    public void AllSubcommands_RequireAdmin()
    {
        Assert.Contains("admin", RenderHelp("tags", "list"));
        Assert.Contains("admin", RenderHelp("tags", "rename"));
        Assert.Contains("admin", RenderHelp("tags", "delete"));
        Assert.Contains("Permission required:", RenderHelp("tags", "list"));
    }
}

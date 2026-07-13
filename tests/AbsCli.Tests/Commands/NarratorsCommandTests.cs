using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class NarratorsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(NarratorsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Narrators_HasThreeSubcommands()
    {
        var verbs = NarratorsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void NarratorsRename_UsesPositionalArgs()
    {
        var output = RenderHelp("narrators", "rename");
        Assert.Contains("old-narrator", output);
        Assert.Contains("new-narrator", output);
        Assert.DoesNotContain("--old-narrator", output);
    }

    [Fact]
    public void NarratorsRenameAndDelete_RequireUpdate()
    {
        Assert.Contains("update", RenderHelp("narrators", "rename"));
        Assert.Contains("Permission required:", RenderHelp("narrators", "rename"));
        Assert.Contains("update", RenderHelp("narrators", "delete"));
        Assert.Contains("Permission required:", RenderHelp("narrators", "delete"));
    }

    [Fact]
    public void NarratorsList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("narrators", "list"));
    }

    [Fact]
    public void NarratorsDelete_Help_DocumentsUpdateNotDelete()
    {
        var output = RenderHelp("narrators", "delete").ToLowerInvariant();
        Assert.Contains("update", output);
    }
}

using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class LibrariesCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(LibrariesCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Libraries_HasListGetScan()
    {
        var verbs = LibrariesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "scan" }, verbs);
    }

    [Fact]
    public void LibrariesScan_RequiresAdminPermission()
    {
        var output = RenderHelp("libraries", "scan");
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void LibrariesList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("libraries", "list"));
    }

    [Fact]
    public void LibrariesGet_RequiresId()
    {
        Assert.Contains("--id", RenderHelp("libraries", "get"));
    }
}

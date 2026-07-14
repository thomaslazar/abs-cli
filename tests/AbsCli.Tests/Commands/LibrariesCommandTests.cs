using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Models;
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
    public void Libraries_HasAllSubcommands()
    {
        var verbs = LibrariesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "scan", "create", "update", "delete", "reorder" }, verbs);
    }

    [Fact]
    public void LibrariesCreate_RequiresAdmin_AndHasFolderAndNameOptions()
    {
        var output = RenderHelp("libraries", "create").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--name", output);
        Assert.Contains("--folder", output);
        Assert.Contains("--media-type", output);
    }

    [Fact]
    public void LibrariesUpdate_RequiresAdmin()
    {
        var output = RenderHelp("libraries", "update").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--id", output);
        Assert.Contains("--display-order", output);
    }

    [Fact]
    public void LibrariesDelete_RequiresAdmin_AndWarnsCascade()
    {
        var output = RenderHelp("libraries", "delete").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("cascade", output.ToLowerInvariant());
        Assert.Contains("confirm", output.ToLowerInvariant());
    }

    [Theory]
    [InlineData("My Library", "My Library", true)]
    [InlineData("  My Library  ", "My Library", true)]
    [InlineData("my library", "My Library", false)]
    [InlineData("Wrong", "My Library", false)]
    [InlineData("", "My Library", false)]
    [InlineData(null, "My Library", false)]
    public void ConfirmationMatches_RequiresExactTrimmedName(string? input, string name, bool expected)
    {
        Assert.Equal(expected, LibrariesCommand.ConfirmationMatches(input, name));
    }

    [Fact]
    public void LibrariesReorder_RequiresAdmin_AndHasInputStdin()
    {
        var output = RenderHelp("libraries", "reorder").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
    }

    [Fact]
    public void BuildUpdateBody_OmitsUnsetIncludesSet()
    {
        var body = LibrariesCommand.BuildUpdateBodyForTesting("New", null, null, null, 2);
        Assert.Equal("New", body.Name);
        Assert.Null(body.MediaType);
        Assert.Equal(2, body.DisplayOrder);
    }

    [Fact]
    public void LibrariesScan_RequiresAdminPermission()
    {
        var output = RenderHelp("libraries", "scan").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
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

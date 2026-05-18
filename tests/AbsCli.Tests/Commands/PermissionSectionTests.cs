using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class PermissionSectionTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.Subcommands.Add(AuthorsCommand.Create());
        root.Subcommands.Add(BackupCommand.Create());
        root.Subcommands.Add(UploadCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void AdminCommand_RendersAdminPermission()
    {
        var output = RenderHelp("backup", "create");
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void UpdateCommand_RendersUpdatePermission()
    {
        var output = RenderHelp("items", "update");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void UploadCommand_RendersUploadPermission()
    {
        var output = RenderHelp("upload");
        Assert.Contains("Permission required:", output);
        Assert.Contains("upload", output);
    }

    [Fact]
    public void DeleteCommand_RendersDeletePermission()
    {
        var output = RenderHelp("authors", "delete");
        Assert.Contains("Permission required:", output);
        Assert.Contains("delete", output);
    }

    [Fact]
    public void ReadOnlyCommand_RendersNoPermissionSection()
    {
        var output = RenderHelp("items", "list");
        Assert.DoesNotContain("Permission required:", output);
    }
}

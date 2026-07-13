using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class BackupCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(BackupCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Backup_HasExpectedSubcommands()
    {
        var verbs = BackupCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "create", "list", "apply", "download", "delete", "upload" }, verbs);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("list")]
    [InlineData("apply")]
    [InlineData("download")]
    [InlineData("delete")]
    [InlineData("upload")]
    public void BackupSubcommands_RequireAdmin(string sub)
    {
        var output = RenderHelp("backup", sub);
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void BackupDownload_RequiresIdAndOutput()
    {
        var output = RenderHelp("backup", "download");
        Assert.Contains("--id", output);
        Assert.Contains("--output", output);
    }
}

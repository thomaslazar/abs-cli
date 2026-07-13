using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsCommandTests
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
    public void ValidateInputSource_Stdin_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource(null, stdin: true, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_ExistingFile_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource("body.json", stdin: false, inputIsExistingFile: true));
    }

    [Fact]
    public void ValidateInputSource_InputNotAFile()
    {
        Assert.Equal("--input must be a file path (got '{\"x\":1}'). For inline JSON, pipe via --stdin.",
            ItemsCommand.ValidateInputSource("{\"x\":1}", stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_NeitherProvided()
    {
        Assert.Equal("Provide --input <file> or --stdin",
            ItemsCommand.ValidateInputSource(null, stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void Items_HasBaseVerbs()
    {
        var verbs = ItemsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        foreach (var v in new[] { "list", "get", "update", "batch-update", "batch-get", "delete", "batch-delete", "scan" })
            Assert.Contains(v, verbs);
    }

    [Fact]
    public void ItemsUpdate_RequiresUpdatePermission()
    {
        var output = RenderHelp("items", "update");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void ItemsScan_RequiresAdminPermission()
    {
        var output = RenderHelp("items", "scan");
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void ItemsList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("items", "list"));
    }
}

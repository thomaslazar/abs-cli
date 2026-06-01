using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsDeleteCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Items_HasDeleteAndBatchDelete()
    {
        var verbs = ItemsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Contains("delete", verbs);
        Assert.Contains("batch-delete", verbs);
    }

    [Fact]
    public void ItemsDelete_Help_DocumentsIdHardAndSoftDefault()
    {
        var output = RenderHelp("items", "delete");
        Assert.Contains("--id", output);
        Assert.Contains("--hard", output);
        Assert.Contains("database only", output);
        Assert.Contains("irreversible", output);
    }

    [Fact]
    public void ItemsBatchDelete_Help_DocumentsInputStdinHardAndCaveats()
    {
        var output = RenderHelp("items", "batch-delete");
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
        Assert.Contains("--hard", output);
        Assert.Contains("every id in the batch", output);
        Assert.Contains("all-or-nothing", output);
    }

    [Fact]
    public void ItemsUpdate_Help_DocumentsStdinAndDropsInlineJson()
    {
        var output = RenderHelp("items", "update");
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
        Assert.Contains("Inline JSON", output);
    }
}

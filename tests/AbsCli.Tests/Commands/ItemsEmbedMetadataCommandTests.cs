using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsEmbedMetadataCommandTests
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
    public void EmbedMetadata_Help_ListsAllFlags()
    {
        var output = RenderHelp("items", "embed-metadata");
        Assert.Contains("--id", output);
        Assert.Contains("--no-backup", output);
        Assert.Contains("--force-embed-chapters", output);
        Assert.Contains("--wait", output);
    }

    [Fact]
    public void EmbedMetadata_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "embed-metadata");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"libraryItemId\"", output);
        Assert.Contains("\"action\"", output);
        Assert.Contains("\"started\"", output);
        Assert.Contains("\"options\"", output);
    }

    [Fact]
    public void EmbedMetadata_Help_SurfacesCaveats()
    {
        var output = RenderHelp("items", "embed-metadata");
        Assert.Contains("Caveats", output);
        Assert.Contains("Admin only", output);
        Assert.Contains("destructive", output);
        Assert.Contains("does NOT guarantee success", output);
        Assert.Contains("MAX_CONCURRENT_TASKS", output);
    }

    [Fact]
    public void BatchEmbedMetadata_Help_ListsAllFlags()
    {
        var output = RenderHelp("items", "batch-embed-metadata");
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
        Assert.Contains("--no-backup", output);
        Assert.Contains("--force-embed-chapters", output);
        Assert.Contains("--wait", output);
    }

    [Fact]
    public void BatchEmbedMetadata_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "batch-embed-metadata");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"libraryItemIds\"", output);
    }

    [Fact]
    public void BatchEmbedMetadata_Help_SurfacesCaveats()
    {
        var output = RenderHelp("items", "batch-embed-metadata");
        Assert.Contains("Caveats", output);
        Assert.Contains("Admin only", output);
        Assert.Contains("libraryItemIds", output);
        Assert.Contains("Batch validates ALL items upfront", output);
        Assert.Contains("uniformly", output);
    }
}

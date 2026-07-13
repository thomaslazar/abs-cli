using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class MetadataCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(MetadataCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Metadata_HasSearchProvidersCovers()
    {
        var verbs = MetadataCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "search", "providers", "covers" }, verbs);
    }

    [Fact]
    public void MetadataSearch_Help_ShowsProviderAndTitle()
    {
        var output = RenderHelp("metadata", "search");
        Assert.Contains("--provider", output);
        Assert.Contains("--title", output);
        Assert.Contains("--author", output);
    }

    [Fact]
    public void MetadataCovers_Help_ShowsProviderAndTitle()
    {
        var output = RenderHelp("metadata", "covers");
        Assert.Contains("--provider", output);
        Assert.Contains("--title", output);
    }
}

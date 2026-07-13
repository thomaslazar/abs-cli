using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class SeriesCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(SeriesCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Series_HasListGetUpdate()
    {
        var verbs = SeriesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "update" }, verbs);
    }

    [Fact]
    public void SeriesList_Help_DocumentsOptions()
    {
        var output = RenderHelp("series", "list");
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
    }

    [Fact]
    public void SeriesList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("series", "list"));
    }

    [Fact]
    public void SeriesGet_Help_RequiresId()
    {
        var output = RenderHelp("series", "get");
        Assert.Contains("--id", output);
    }

    [Fact]
    public void SeriesGet_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("series", "get"));
    }

    [Fact]
    public void SeriesUpdate_RequiresUpdatePermission()
    {
        var output = RenderHelp("series", "update");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void SeriesUpdate_Help_DocumentsNoMerge()
    {
        var output = RenderHelp("series", "update").ToLowerInvariant();
        Assert.Contains("duplicate", output);
    }

    [Fact]
    public void BuildUpdateBody_OmitsUnsetKeys()
    {
        var body = SeriesCommand.BuildUpdateBodyForTesting("New Name", null);
        Assert.True(body.ContainsKey("name"));
        Assert.False(body.ContainsKey("description"));
        Assert.Equal("New Name", body["name"]);
    }

    [Fact]
    public void BuildUpdateBody_IncludesEmptyDescription()
    {
        var body = SeriesCommand.BuildUpdateBodyForTesting(null, "");
        Assert.True(body.ContainsKey("description"));
        Assert.Equal("", body["description"]);
        Assert.False(body.ContainsKey("name"));
    }
}

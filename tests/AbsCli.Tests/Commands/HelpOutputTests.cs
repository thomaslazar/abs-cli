using System.CommandLine;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class HelpOutputTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(AuthorsCommand.Create());
        root.Subcommands.Add(SeriesCommand.Create());
        root.Subcommands.Add(ItemsCommand.Create());
        root.Subcommands.Add(LibrariesCommand.Create());
        root.Subcommands.Add(BackupCommand.Create());
        root.Subcommands.Add(TasksCommand.Create());
        root.Subcommands.Add(MetadataCommand.Create());
        root.Subcommands.Add(SearchCommand.Create());
        HelpExtensions.UseCustomHelpSections(root);
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void AuthorsCommand_TopLevelHelp_ShowsNotes()
    {
        var output = RenderHelp("authors");
        Assert.Contains("Notes:", output);
        Assert.Contains("derived from book metadata", output);
        Assert.True(output.IndexOf("Notes:") < output.IndexOf("Commands:"));
    }

    [Fact]
    public void SeriesCommand_TopLevelHelp_ShowsNotes()
    {
        var output = RenderHelp("series");
        Assert.Contains("Notes:", output);
        Assert.Contains("derived from book metadata", output);
    }

    [Fact]
    public void AuthorsList_Help_ShowsResponseShapeWithNumBooks()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"numBooks\"", output);
        Assert.Contains("\"authors\"", output);
    }

    [Fact]
    public void SeriesList_Help_ShowsResponseShapeWithResults()
    {
        var output = RenderHelp("series", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"name\"", output);
    }

    [Fact]
    public void ItemsList_Help_ShowsPaginatedResponseWithLibraryItemFields()
    {
        var output = RenderHelp("items", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"mediaType\"", output);
        Assert.Contains("\"libraryId\"", output);
    }

    [Fact]
    public void ItemsGet_Help_ShowsLibraryItemMinified()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"mediaType\"", output);
        Assert.Contains("\"birthtimeMs\"", output);
    }

    [Theory]
    [InlineData("search")]
    [InlineData("update")]
    [InlineData("batch-update")]
    [InlineData("batch-get")]
    [InlineData("scan")]
    public void Items_Subcommand_Help_IncludesResponseShape(string sub)
    {
        var output = RenderHelp("items", sub);
        Assert.Contains("Response shape:", output);
    }

    [Theory]
    [InlineData("libraries", "list")]
    [InlineData("libraries", "get")]
    [InlineData("backup", "create")]
    [InlineData("backup", "list")]
    [InlineData("backup", "delete")]
    [InlineData("backup", "upload")]
    [InlineData("tasks", "list")]
    [InlineData("metadata", "providers")]
    [InlineData("metadata", "covers")]
    public void Command_Help_IncludesResponseShape(string group, string sub)
    {
        var output = RenderHelp(group, sub);
        Assert.Contains("Response shape:", output);
    }

    [Fact]
    public void SearchCommand_Help_IncludesResponseShape()
    {
        var output = RenderHelp("search");
        Assert.Contains("Response shape:", output);
    }

    [Theory]
    [InlineData("metadata", "search")]
    [InlineData("backup", "apply")]
    [InlineData("backup", "download")]
    [InlineData("libraries", "scan")]
    public void WriteRawJsonCommands_DoNotHaveResponseShape(string group, string sub)
    {
        var output = RenderHelp(group, sub);
        Assert.DoesNotContain("Response shape:", output);
    }

    [Theory]
    [InlineData("items", "get")]
    [InlineData("items", "list")]
    [InlineData("items", "search")]
    [InlineData("items", "update")]
    [InlineData("items", "batch-get")]
    public void Items_Help_ShowsBothMediaUnionShapes(string group, string sub)
    {
        var output = RenderHelp(group, sub);
        Assert.Contains("Book media shape", output);
        Assert.Contains("Podcast media shape", output);
        Assert.Contains("\"numTracks\"", output);   // BookMediaMinified-specific
    }

    [Fact]
    public void Search_Help_ShowsBothMediaUnionShapes()
    {
        var output = RenderHelp("search");
        Assert.Contains("Book media shape", output);
        Assert.Contains("Podcast media shape", output);
    }

    [Fact]
    public void Search_Help_ShowsConcreteSearchWrapperShapes()
    {
        var output = RenderHelp("search");
        // SearchResult.narrators and .genres/.tags have distinct wrappers.
        Assert.Contains("\"numBooks\"", output);
        Assert.Contains("\"numItems\"", output);
    }
}

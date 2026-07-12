using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class GenresCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(GenresCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Genres_HasThreeSubcommands()
    {
        var verbs = GenresCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void GenresRename_UsesPositionalArgs()
    {
        var output = RenderHelp("genres", "rename");
        Assert.Contains("old-genre", output);
        Assert.Contains("new-genre", output);
        Assert.DoesNotContain("--old-genre", output);
    }

    [Fact]
    public void GenresList_Help_DocumentsUnsorted()
    {
        var output = RenderHelp("genres", "list");
        Assert.Contains("unsorted", output.ToLowerInvariant());
    }

    [Fact]
    public void AllSubcommands_RequireAdmin()
    {
        Assert.Contains("admin", RenderHelp("genres", "list"));
        Assert.Contains("admin", RenderHelp("genres", "rename"));
        Assert.Contains("admin", RenderHelp("genres", "delete"));
        Assert.Contains("Permission required:", RenderHelp("genres", "delete"));
    }
}

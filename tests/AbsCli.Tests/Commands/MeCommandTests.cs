using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class MeCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(MeCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Me_Help_MentionsMediaProgressSize()
    {
        var output = RenderHelp("me");
        Assert.Contains("mediaProgress", output);
        Assert.Contains("MB-size", output);
    }

    [Fact]
    public void Me_Help_ShowsResponseShape()
    {
        var output = RenderHelp("me");
        Assert.Contains("Response shape:", output);
    }
}

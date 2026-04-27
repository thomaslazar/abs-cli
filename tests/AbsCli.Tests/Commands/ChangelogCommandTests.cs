using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ChangelogCommandTests
{
    private static int Invoke(TestConsole console, params string[] args)
    {
        var root = new RootCommand();
        root.AddCommand(ChangelogCommand.Create());
        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
        return parser.Invoke(args, console);
    }

    private static string FirstVersionHeading()
    {
        // tests/AbsCli.Tests/bin/Debug/net8.0 -> repo root is four levels up.
        // TrimEnd('/') normalises the trailing slash AppContext.BaseDirectory may include.
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
        for (var i = 0; i < 7 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "CHANGELOG.md");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadLines(candidate))
                {
                    if (line.StartsWith("## ", StringComparison.Ordinal))
                    {
                        return line;
                    }
                }
                throw new InvalidOperationException("Repo CHANGELOG.md has no version heading");
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo CHANGELOG.md from test base directory");
    }

    [Fact]
    public void Default_PrintsLatestEntryStartingAtTopmostHeading()
    {
        var console = new TestConsole();
        var exit = Invoke(console, "changelog");
        var stdout = console.Out.ToString() ?? "";

        Assert.Equal(0, exit);
        Assert.StartsWith(FirstVersionHeading(), stdout);
    }

    [Fact]
    public void All_PrintsFullFileStartingWithTopHeader()
    {
        var console = new TestConsole();
        var exit = Invoke(console, "changelog", "--all");
        var stdout = console.Out.ToString() ?? "";

        Assert.Equal(0, exit);
        Assert.StartsWith("# Changelog", stdout);
    }

    [Fact]
    public void All_OutputIsLongerThanDefault()
    {
        var defaultConsole = new TestConsole();
        Invoke(defaultConsole, "changelog");
        var allConsole = new TestConsole();
        Invoke(allConsole, "changelog", "--all");

        var defaultLen = (defaultConsole.Out.ToString() ?? "").Length;
        var allLen = (allConsole.Out.ToString() ?? "").Length;
        Assert.True(allLen > defaultLen,
            $"--all output ({allLen} chars) should be longer than default ({defaultLen})");
    }
}

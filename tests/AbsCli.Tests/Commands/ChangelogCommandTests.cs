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
        // Walk up from the test assembly's base directory until we find AbsCli.sln,
        // which marks the repo root. No fixed depth cap — the walk terminates at
        // the filesystem root.
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "AbsCli.sln")))
            {
                var changelog = Path.Combine(dir, "CHANGELOG.md");
                foreach (var line in File.ReadLines(changelog))
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
        throw new InvalidOperationException("Could not locate AbsCli.sln from test base directory");
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

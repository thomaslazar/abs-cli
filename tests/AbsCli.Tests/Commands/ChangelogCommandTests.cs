using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ChangelogCommandTests
{
    private static int Invoke(StringWriter output, params string[] args)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ChangelogCommand.Create());
        var config = new InvocationConfiguration { Output = output };
        return root.Parse(args).Invoke(config);
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
        var output = new StringWriter();
        var exit = Invoke(output, "changelog");
        var stdout = output.ToString();
        Assert.Equal(0, exit);
        Assert.StartsWith(FirstVersionHeading(), stdout);
    }

    [Fact]
    public void All_PrintsFullFileStartingWithTopHeader()
    {
        var output = new StringWriter();
        var exit = Invoke(output, "changelog", "--all");
        var stdout = output.ToString();
        Assert.Equal(0, exit);
        Assert.StartsWith("# Changelog", stdout);
    }

    [Fact]
    public void All_OutputIsLongerThanDefault()
    {
        var defaultOut = new StringWriter();
        Invoke(defaultOut, "changelog");
        var allOut = new StringWriter();
        Invoke(allOut, "changelog", "--all");
        var defaultLen = defaultOut.ToString().Length;
        var allLen = allOut.ToString().Length;
        Assert.True(allLen > defaultLen,
            $"--all output ({allLen} chars) should be longer than default ({defaultLen})");
    }
}

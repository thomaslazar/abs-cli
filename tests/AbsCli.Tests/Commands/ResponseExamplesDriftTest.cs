using System.Diagnostics;
using System.IO;

namespace AbsCli.Tests.Commands;

public class ResponseExamplesDriftTest
{
    [Fact]
    public void CheckedInFile_MatchesFreshGeneration()
    {
        var repoRoot = RepoRoot();
        var checkedInPath = Path.Combine(repoRoot, "src", "AbsCli", "Commands", "ResponseExamples.g.cs");
        Assert.True(File.Exists(checkedInPath), $"Missing generated file: {checkedInPath}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"response-examples-{Guid.NewGuid():N}.g.cs");
        try
        {
            var toolProject = Path.Combine(repoRoot, "tools", "GenerateResponseExamples", "GenerateResponseExamples.csproj");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "run", "--project", toolProject, "--", tempPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            Assert.True(proc.ExitCode == 0,
                $"Generator exited {proc.ExitCode}\nstdout: {proc.StandardOutput.ReadToEnd()}\nstderr: {proc.StandardError.ReadToEnd()}");

            var expected = File.ReadAllText(checkedInPath).Replace("\r\n", "\n");
            var actual = File.ReadAllText(tempPath).Replace("\r\n", "\n");
            Assert.True(
                expected == actual,
                "ResponseExamples.g.cs is stale. Regenerate with: " +
                "dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AbsCli.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}

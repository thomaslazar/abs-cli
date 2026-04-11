using System.Diagnostics;
using System.Text.Json;

namespace AbsCli.Tests.Integration;

public abstract class IntegrationTestBase
{
    protected static readonly string AbsUrl =
        Environment.GetEnvironmentVariable("ABS_URL") ?? "http://localhost:13378";

    protected static string BinaryPath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("ABS_CLI_BINARY")
                ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "src", "AbsCli", "bin", "Debug", "net8.0", "abs-cli");
            return path;
        }
    }

    protected async Task<(string stdout, string stderr, int exitCode)> RunCliAsync(
        string arguments, string? stdin = null, Dictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AbsCli", "AbsCli.csproj")} -- {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin != null,
            UseShellExecute = false
        };

        if (env != null)
        {
            foreach (var kv in env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        using var process = Process.Start(psi)!;

        if (stdin != null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (stdout, stderr, process.ExitCode);
    }

    protected static JsonElement ParseJson(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}

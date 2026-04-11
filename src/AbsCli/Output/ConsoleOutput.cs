using System.Text.Json;
using AbsCli.Models;

namespace AbsCli.Output;

public static class ConsoleOutput
{
    public static void WriteJson(Dictionary<string, string> data)
    {
        var json = JsonSerializer.Serialize(data, AppJsonContext.Default.DictionaryStringString);
        Console.Out.WriteLine(json);
    }

    public static void WriteRawJson(string json)
    {
        Console.Out.WriteLine(json);
    }

    public static void WriteError(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
    }

    public static void WriteWarning(string message)
    {
        Console.Error.WriteLine($"Warning: {message}");
    }
}

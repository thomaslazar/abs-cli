using System.Text.Json;

namespace AbsCli.Output;

public static class ConsoleOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void WriteJson<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
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

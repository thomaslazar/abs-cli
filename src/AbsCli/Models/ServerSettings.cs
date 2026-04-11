using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class ServerSettings
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("buildNumber")]
    public int BuildNumber { get; set; }
}

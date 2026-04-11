using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BatchUpdateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("updates")]
    public int Updates { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class PaginatedResponse
{
    [JsonPropertyName("results")]
    public List<JsonElement> Results { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }
}

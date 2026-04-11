using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class SearchResult
{
    [JsonPropertyName("book")]
    public List<JsonElement>? Book { get; set; }

    [JsonPropertyName("podcast")]
    public List<JsonElement>? Podcast { get; set; }

    [JsonPropertyName("narrators")]
    public List<JsonElement>? Narrators { get; set; }

    [JsonPropertyName("tags")]
    public List<JsonElement>? Tags { get; set; }

    [JsonPropertyName("genres")]
    public List<JsonElement>? Genres { get; set; }

    [JsonPropertyName("series")]
    public List<JsonElement>? Series { get; set; }

    [JsonPropertyName("authors")]
    public List<JsonElement>? Authors { get; set; }
}

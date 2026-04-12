using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class SeriesItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nameIgnorePrefix")]
    public string? NameIgnorePrefix { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";
}

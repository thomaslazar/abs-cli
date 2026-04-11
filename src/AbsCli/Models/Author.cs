using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class AuthorItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("numBooks")]
    public int NumBooks { get; set; }

    [JsonPropertyName("lastFirst")]
    public string? LastFirst { get; set; }
}

public class AuthorListResponse
{
    [JsonPropertyName("authors")]
    public List<AuthorItem> Authors { get; set; } = new();
}

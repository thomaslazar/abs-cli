using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Expanded collection shape returned by ABS — matches
/// <c>Collection.toOldJSONExpanded()</c> in
/// <c>server/models/Collection.js</c>. The <c>Books</c> array contains
/// full <see cref="LibraryItemExpanded"/> entries (with their nested
/// media), in the collection's order. <see cref="RssFeed"/> is populated
/// only when the caller passed <c>include=rssfeed</c> and a feed exists.
/// </summary>
public class Collection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("books")]
    public List<LibraryItemExpanded> Books { get; set; } = new();

    [JsonPropertyName("lastUpdate")]
    public long LastUpdate { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("rssFeed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RssFeed? RssFeed { get; set; }
}

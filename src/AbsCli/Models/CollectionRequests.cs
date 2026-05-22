using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for POST /api/collections. ABS requires <see cref="LibraryId"/>,
/// <see cref="Name"/>, and at least one entry in <see cref="Books"/>
/// (libraryItemIds). <see cref="Description"/> is optional and omitted
/// when null.
/// </summary>
public class CollectionCreateRequest
{
    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("books")]
    public List<string> Books { get; set; } = new();
}

/// <summary>
/// Body for reorder, batch-add, and batch-remove. Entries are
/// libraryItemIds. For reorder this must be the FULL current
/// membership in the desired order — partial lists produce undefined
/// behavior server-side (see spec).
/// </summary>
public class CollectionBooksRequest
{
    [JsonPropertyName("books")]
    public List<string> Books { get; set; } = new();
}

/// <summary>
/// Body for POST /api/collections/:id/book. ABS expects
/// <c>{"id": "<libraryItemId>"}</c> — not <c>bookId</c> or
/// <c>libraryItemId</c>.
/// </summary>
public class CollectionBookRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

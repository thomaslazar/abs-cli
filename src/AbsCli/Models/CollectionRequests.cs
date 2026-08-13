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
/// Body for <c>{"books":[...]}</c> — shared by collections reorder,
/// batch-add, batch-remove, and (as a partial input) create, AND by the
/// playlists reorder/batch-add/batch-remove CLI contract (see
/// PlaylistsService.SerializeItems for how those map it to ABS's
/// <c>items:[{libraryItemId}]</c> wire body). For collections reorder this
/// must be the FULL current membership in the desired order — partial
/// lists produce undefined behavior server-side (see spec).
/// </summary>
public class BooksRequest
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

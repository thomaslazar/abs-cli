using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for <c>POST /api/playlists</c>. <see cref="Items"/> may be empty —
/// ABS allows creating an empty playlist (unlike collections).
/// <see cref="Description"/> is omitted when null.
/// </summary>
public class PlaylistCreateRequest
{
    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    public List<PlaylistItemRef> Items { get; set; } = new();
}

/// <summary>
/// Body for reorder (<c>PATCH /api/playlists/:id</c>), batch-add, and
/// batch-remove. For reorder this must be the FULL current membership in the
/// desired order — ABS rejects a length mismatch with 400.
/// </summary>
public class PlaylistItemsRequest
{
    [JsonPropertyName("items")]
    public List<PlaylistItemRef> Items { get; set; } = new();
}

/// <summary>
/// A single playlist item reference. Books-only for this CLI, so just
/// <see cref="LibraryItemId"/>. Used as the <c>POST /playlists/:id/item</c>
/// body and as the elements of the arrays above.
/// </summary>
public class PlaylistItemRef
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";
}

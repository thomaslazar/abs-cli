using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Expanded playlist shape returned by ABS — matches
/// <c>Playlist.toOldJSONExpanded()</c> in <c>server/models/Playlist.js</c>.
/// Playlists are user-owned; <see cref="Items"/> are in playlist order.
/// </summary>
public class Playlist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("lastUpdate")]
    public long LastUpdate { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("items")]
    public List<PlaylistItem> Items { get; set; } = new();
}

/// <summary>
/// One entry in a playlist. For books <see cref="LibraryItem"/> holds the
/// expanded item. <see cref="EpisodeId"/> / <see cref="Episode"/> are
/// populated only for podcast playlists — this CLI never creates those, but
/// they are preserved on read so a podcast playlist round-trips faithfully.
/// </summary>
public class PlaylistItem
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";

    [JsonPropertyName("libraryItem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryItemExpanded? LibraryItem { get; set; }

    [JsonPropertyName("episodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EpisodeId { get; set; }

    [JsonPropertyName("episode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Episode { get; set; }
}

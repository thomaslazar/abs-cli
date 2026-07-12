using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Response from GET /api/genres. Returned in discovery order (NOT sorted).</summary>
public class GenreListResponse
{
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}

/// <summary>Request body for POST /api/genres/rename.</summary>
public class GenreRenameRequest
{
    [JsonPropertyName("genre")]
    public string Genre { get; set; } = "";
    [JsonPropertyName("newGenre")]
    public string NewGenre { get; set; } = "";
}

/// <summary>Response from POST /api/genres/rename. genreMerged is true when the new name already existed.</summary>
public class GenreRenameResponse
{
    [JsonPropertyName("genreMerged")]
    public bool GenreMerged { get; set; }
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

/// <summary>Response from DELETE /api/genres/:genre.</summary>
public class GenreDeleteResponse
{
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

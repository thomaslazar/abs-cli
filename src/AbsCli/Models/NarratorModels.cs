using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>One narrator from GET /api/libraries/:id/narrators. id is the URI-encoded base64 of the name.</summary>
public class NarratorItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("numBooks")]
    public int NumBooks { get; set; }
}

/// <summary>Response from GET /api/libraries/:id/narrators. Natural-sorted by name.</summary>
public class NarratorListResponse
{
    [JsonPropertyName("narrators")]
    public List<NarratorItem> Narrators { get; set; } = new();
}

/// <summary>Request body for PATCH /api/libraries/:id/narrators/:narratorId.</summary>
public class NarratorRenameRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>Response from PATCH/DELETE narrator — number of items whose narrator list changed.</summary>
public class NarratorUpdateResponse
{
    [JsonPropertyName("updated")]
    public int Updated { get; set; }
}

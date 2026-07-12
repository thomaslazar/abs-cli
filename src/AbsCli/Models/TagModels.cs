using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Response from GET /api/tags. Server-sorted case-insensitively.</summary>
public class TagListResponse
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>Request body for POST /api/tags/rename.</summary>
public class TagRenameRequest
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";
    [JsonPropertyName("newTag")]
    public string NewTag { get; set; } = "";
}

/// <summary>Response from POST /api/tags/rename. tagMerged is true when the new name already existed.</summary>
public class TagRenameResponse
{
    [JsonPropertyName("tagMerged")]
    public bool TagMerged { get; set; }
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

/// <summary>Response from DELETE /api/tags/:tag.</summary>
public class TagDeleteResponse
{
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

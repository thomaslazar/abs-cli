using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Folder entry for POST /api/libraries (server-side path).</summary>
public class LibraryFolderRequest
{
    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = "";
}

/// <summary>Request body for POST /api/libraries. Null optionals are omitted (server defaults apply).</summary>
public class LibraryCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("folders")]
    public List<LibraryFolderRequest> Folders { get; set; } = new();

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; set; }

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }
}

/// <summary>
/// One entry of the bare-array body for POST /api/libraries/order
/// (LibraryController.reorder). Both fields are required — ABS validates the
/// whole array upfront with `typeof o?.id !== 'string' || typeof o?.newOrder
/// !== 'number'` and 400s the entire request if any entry fails either check.
/// </summary>
public class LibraryReorderEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("newOrder")]
    public int? NewOrder { get; set; }
}

/// <summary>Request body for PATCH /api/libraries/:id. Null fields are omitted.</summary>
public class LibraryUpdateRequest
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; set; }

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    [JsonPropertyName("displayOrder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DisplayOrder { get; set; }
}

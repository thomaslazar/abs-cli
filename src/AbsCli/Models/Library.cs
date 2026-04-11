using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class Library
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("folders")]
    public List<LibraryFolder> Folders { get; set; } = new();

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("lastScan")]
    public long? LastScan { get; set; }

    [JsonPropertyName("lastScanVersion")]
    public string? LastScanVersion { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("lastUpdate")]
    public long LastUpdate { get; set; }
}

public class LibraryFolder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = "";

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }
}

public class LibraryListResponse
{
    [JsonPropertyName("libraries")]
    public List<Library> Libraries { get; set; } = new();
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class LibraryItemMinified
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ino")]
    public string? Ino { get; set; }

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("relPath")]
    public string? RelPath { get; set; }

    [JsonPropertyName("isFile")]
    public bool IsFile { get; set; }

    [JsonPropertyName("mtimeMs")]
    public long MtimeMs { get; set; }

    [JsonPropertyName("ctimeMs")]
    public long CtimeMs { get; set; }

    [JsonPropertyName("birthtimeMs")]
    public long BirthtimeMs { get; set; }

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("isMissing")]
    public bool IsMissing { get; set; }

    [JsonPropertyName("isInvalid")]
    public bool IsInvalid { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("media")]
    public JsonElement? Media { get; set; }

    [JsonPropertyName("numFiles")]
    public int NumFiles { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

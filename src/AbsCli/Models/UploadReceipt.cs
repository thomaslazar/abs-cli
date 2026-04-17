using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Returned by <c>abs-cli upload</c> (without <c>--wait</c>) after the POST
/// /api/upload call completes. ABS writes the files synchronously then returns
/// HTTP 200 with an empty body — the library item is created asynchronously
/// by ABS's file watcher on its next scan, so this receipt describes what
/// was uploaded, not the resulting library item. Use <c>--wait</c> to also
/// resolve the library item (returned as <see cref="LibraryItemMinified"/>).
/// </summary>
public class UploadReceipt
{
    [JsonPropertyName("uploaded")]
    public bool Uploaded { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("series")]
    public string? Series { get; set; }

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("folderId")]
    public string FolderId { get; set; } = "";

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}

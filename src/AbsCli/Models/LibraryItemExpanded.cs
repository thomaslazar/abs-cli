using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Per-file entry inside <see cref="LibraryItemExpanded.LibraryFiles"/>.
/// Mirrors ABS's LibraryFile.toJSON() shape.
/// </summary>
public class LibraryFile
{
    [JsonPropertyName("ino")]
    public string Ino { get; set; } = "";

    [JsonPropertyName("metadata")]
    public LibraryFileMetadata Metadata { get; set; } = new();

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    /// <summary>
    /// Ebook files carry true/false; non-ebook files (audio, image, metadata) leave this null.
    /// </summary>
    [JsonPropertyName("isSupplementary")]
    public bool? IsSupplementary { get; set; }

    /// <summary>"ebook" | "audio" | "image" | "metadata" | "other".</summary>
    [JsonPropertyName("fileType")]
    public string FileType { get; set; } = "";
}

/// <summary>
/// Nested `metadata` object on <see cref="LibraryFile"/>.
/// </summary>
public class LibraryFileMetadata
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("ext")]
    public string Ext { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("relPath")]
    public string RelPath { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mtimeMs")]
    public long MtimeMs { get; set; }

    [JsonPropertyName("ctimeMs")]
    public long CtimeMs { get; set; }

    [JsonPropertyName("birthtimeMs")]
    public long BirthtimeMs { get; set; }
}

/// <summary>
/// Expanded library item shape returned by GET /api/items/:id?expanded=1.
/// Sibling of <see cref="LibraryItemMinified"/> — adds libraryFiles[],
/// lastScan, scanVersion, oldLibraryItemId. The media field stays as a
/// JsonElement passthrough on both shapes (typing it is a separate spec).
/// </summary>
public class LibraryItemExpanded
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ino")]
    public string? Ino { get; set; }

    [JsonPropertyName("oldLibraryItemId")]
    public string? OldLibraryItemId { get; set; }

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

    [JsonPropertyName("lastScan")]
    public long? LastScan { get; set; }

    [JsonPropertyName("scanVersion")]
    public string? ScanVersion { get; set; }

    [JsonPropertyName("isMissing")]
    public bool IsMissing { get; set; }

    [JsonPropertyName("isInvalid")]
    public bool IsInvalid { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("media")]
    public JsonElement? Media { get; set; }

    [JsonPropertyName("libraryFiles")]
    public List<LibraryFile> LibraryFiles { get; set; } = new();

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("userMediaProgress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MediaProgress? UserMediaProgress { get; set; }

    [JsonPropertyName("rssFeed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RssFeed? RssFeed { get; set; }

    [JsonPropertyName("mediaItemShare")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? MediaItemShare { get; set; }

    [JsonPropertyName("episodeDownloadsQueued")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonElement>? EpisodeDownloadsQueued { get; set; }

    [JsonPropertyName("episodesDownloading")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonElement>? EpisodesDownloading { get; set; }
}

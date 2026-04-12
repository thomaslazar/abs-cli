using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BookMediaMinified
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("metadata")]
    public BookMetadataMinified Metadata { get; set; } = new();

    [JsonPropertyName("coverPath")]
    public string? CoverPath { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("numTracks")]
    public int NumTracks { get; set; }

    [JsonPropertyName("numAudioFiles")]
    public int NumAudioFiles { get; set; }

    [JsonPropertyName("numChapters")]
    public int NumChapters { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("ebookFormat")]
    public string? EbookFormat { get; set; }
}

public class BookMediaFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("libraryItemId")]
    public string? LibraryItemId { get; set; }

    [JsonPropertyName("metadata")]
    public BookMetadataExpanded Metadata { get; set; } = new();

    [JsonPropertyName("coverPath")]
    public string? CoverPath { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

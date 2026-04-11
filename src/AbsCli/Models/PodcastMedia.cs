using System.Text.Json.Serialization;

namespace AbsCli.Models;

// Placeholder for future podcast support.
// The CLI currently targets book libraries only.
public class PodcastMedia
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("metadata")]
    public PodcastMetadata Metadata { get; set; } = new();

    [JsonPropertyName("coverPath")]
    public string? CoverPath { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class PodcastMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}

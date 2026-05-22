using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Tolerant DTO for the optional <c>rssFeed</c> field on
/// <see cref="Collection"/> and other entities. The CLI does not manage
/// feeds; it only round-trips the shape ABS returns. All fields are
/// nullable so the shape can evolve upstream without breaking
/// deserialization. Matches <c>Feed.toOldJSON()</c> at a minimum.
/// </summary>
public class RssFeed
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("coverPath")]
    public string? CoverPath { get; set; }

    [JsonPropertyName("serverAddress")]
    public string? ServerAddress { get; set; }

    [JsonPropertyName("feedUrl")]
    public string? FeedUrl { get; set; }
}

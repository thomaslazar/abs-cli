using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// One progress record per (user, libraryItem) — or (user, podcastEpisode)
/// when <see cref="EpisodeId"/> is set. Carries both audio fields
/// (<see cref="CurrentTime"/>, <see cref="IsFinished"/>) and ebook fields
/// (<see cref="EbookLocation"/>, <see cref="EbookProgress"/>) on the same
/// row. Server source: <c>MediaProgress.getOldMediaProgress()</c>.
/// </summary>
public class MediaProgress
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("libraryItemId")]
    public string? LibraryItemId { get; set; }

    [JsonPropertyName("episodeId")]
    public string? EpisodeId { get; set; }

    [JsonPropertyName("mediaItemId")]
    public string MediaItemId { get; set; } = "";

    [JsonPropertyName("mediaItemType")]
    public string MediaItemType { get; set; } = "";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("currentTime")]
    public double CurrentTime { get; set; }

    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }

    [JsonPropertyName("hideFromContinueListening")]
    public bool HideFromContinueListening { get; set; }

    [JsonPropertyName("ebookLocation")]
    public string? EbookLocation { get; set; }

    [JsonPropertyName("ebookProgress")]
    public double? EbookProgress { get; set; }

    [JsonPropertyName("lastUpdate")]
    public long LastUpdate { get; set; }

    [JsonPropertyName("startedAt")]
    public long StartedAt { get; set; }

    [JsonPropertyName("finishedAt")]
    public long? FinishedAt { get; set; }
}

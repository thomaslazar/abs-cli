using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for PATCH /api/me/progress/:libraryItemId. All fields nullable +
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/> so omitted properties
/// stay out of the wire body (server treats absent fields as "leave
/// unchanged"). Empty string on <see cref="EbookLocation"/> is the
/// explicit-clear signal and must serialize.
/// </summary>
public class ProgressUpdateRequest
{
    [JsonPropertyName("currentTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CurrentTime { get; set; }

    [JsonPropertyName("isFinished")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsFinished { get; set; }

    [JsonPropertyName("ebookLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EbookLocation { get; set; }

    [JsonPropertyName("ebookProgress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EbookProgress { get; set; }

    [JsonPropertyName("hideFromContinueListening")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HideFromContinueListening { get; set; }

    [JsonPropertyName("finishedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FinishedAt { get; set; }
}

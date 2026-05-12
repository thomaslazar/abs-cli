using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Encode-m4b request options. All three are nullable; null means "caller
/// did not pass this flag — let ABS apply its server-side default" (aac /
/// 128k / 2). The CLI validates the enum membership of any non-null value
/// before any HTTP call.
/// </summary>
public class EncodeM4bOptions
{
    [JsonPropertyName("codec")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Codec { get; set; }

    [JsonPropertyName("bitrate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Bitrate { get; set; }

    [JsonPropertyName("channels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Channels { get; set; }
}

/// <summary>
/// Receipt printed by <c>abs-cli items encode-m4b start</c> on success.
/// ABS's POST returns empty 200; the receipt is CLI-synthesised from the
/// request inputs and does not guarantee the task has been registered
/// server-side (ABS starts the merge without awaiting). Use
/// <c>tasks list</c> (only while running) and <c>items get</c> (post-task,
/// to confirm the new file layout) for observability.
/// </summary>
public class EncodeM4bStartReceipt
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "encode-m4b";

    [JsonPropertyName("started")]
    public bool Started { get; set; }

    [JsonPropertyName("options")]
    public EncodeM4bOptions Options { get; set; } = new();
}

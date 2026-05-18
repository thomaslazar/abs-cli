using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Query-string options for both /api/tools/item/:id/embed-metadata and
/// /api/tools/batch/embed-metadata. Both flags are explicit booleans the
/// CLI always sends (Backup default true, ForceEmbedChapters default
/// false) — no "omit means server default" semantics.
/// </summary>
public class EmbedMetadataOptions
{
    [JsonPropertyName("backup")]
    public bool Backup { get; set; } = true;

    [JsonPropertyName("forceEmbedChapters")]
    public bool ForceEmbedChapters { get; set; }
}

/// <summary>
/// Receipt printed by `abs-cli items embed-metadata` on success. ABS's
/// POST returns empty 200; the receipt is CLI-synthesised from the
/// request inputs. With --wait the receipt prints only after the matching
/// task disappears from /api/tasks (which means ABS stopped processing,
/// not necessarily that the embed succeeded — see --help caveats).
/// </summary>
public class EmbedMetadataReceipt
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "embed-metadata";

    [JsonPropertyName("started")]
    public bool Started { get; set; }

    [JsonPropertyName("options")]
    public EmbedMetadataOptions Options { get; set; } = new();
}

/// <summary>
/// Request body for POST /api/tools/batch/embed-metadata. Also the
/// deserialization target for --input / --stdin payloads on
/// `abs-cli items batch-embed-metadata` — shape validation is the
/// AOT JSON pass.
/// </summary>
public class BatchEmbedMetadataRequest
{
    [JsonPropertyName("libraryItemIds")]
    public List<string> LibraryItemIds { get; set; } = new();
}

/// <summary>
/// Receipt printed by `abs-cli items batch-embed-metadata` on success.
/// Same semantics as EmbedMetadataReceipt but carries the full input
/// list instead of a single ID.
/// </summary>
public class BatchEmbedMetadataReceipt
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "embed-metadata";

    [JsonPropertyName("started")]
    public bool Started { get; set; }

    [JsonPropertyName("libraryItemIds")]
    public List<string> LibraryItemIds { get; set; } = new();

    [JsonPropertyName("options")]
    public EmbedMetadataOptions Options { get; set; } = new();
}

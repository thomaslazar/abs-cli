using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Receipt printed by `abs-cli items toggle-ebook-status` on success.
/// ABS's PATCH returns empty 200; the receipt is CLI-synthesised from
/// the request inputs. The Toggled bool always serialises true when
/// the receipt is built — it exists for jq discoverability and to
/// leave room for a future failure-receipt that flips it false.
/// </summary>
public class EbookFileStatusReceipt
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";

    [JsonPropertyName("fileIno")]
    public string FileIno { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "toggle-ebook-status";

    [JsonPropertyName("toggled")]
    public bool Toggled { get; set; }
}

using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for PATCH /api/items/:id/media. ABS validates nothing here
/// (LibraryItemController.updateMedia is `const mediaPayload = req.body`) and
/// applies only the fields below, ignoring the rest — so every field is
/// optional and this type documents what has an effect rather than gating it.
/// Fields per Book.updateFromRequest.
/// </summary>
public class ItemMediaUpdateRequest
{
    [JsonPropertyName("metadata")]
    public ItemMediaUpdateMetadata? Metadata { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Metadata sub-object. String fields accept a number too (ABS coerces it), and
/// null clears the field. `series` is handled separately by the controller
/// (updateSeriesFromRequest) and takes objects, not strings.
/// </summary>
public class ItemMediaUpdateMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("publishedYear")]
    public string? PublishedYear { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("abridged")]
    public bool? Abridged { get; set; }

    [JsonPropertyName("narrators")]
    public List<string>? Narrators { get; set; }

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }

    [JsonPropertyName("series")]
    public List<SeriesUpdateEntry>? Series { get; set; }
}

/// <summary>
/// Series entry for ItemMediaUpdateMetadata.series. Handled by the controller
/// separately from the rest of metadata (Book.updateSeriesFromRequest), which is
/// why it takes objects where the sibling metadata arrays take plain strings.
/// Two quirks worth knowing: sequence must be a JSON string — a number is
/// silently dropped to null (Book.js:528) — and if any entry lacks a string
/// name, ABS silently ignores the whole series update rather than erroring
/// (Book.js:511).
/// </summary>
public class SeriesUpdateEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sequence")]
    public string? Sequence { get; set; }
}

using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Audnexus chapter shape returned by GET /api/search/chapters.
/// Times are in milliseconds; StartOffsetSec is redundant with
/// StartOffsetMs but Audnexus emits both.
/// </summary>
public class AudnexusChapter
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("lengthMs")]
    public long LengthMs { get; set; }

    [JsonPropertyName("startOffsetMs")]
    public long StartOffsetMs { get; set; }

    [JsonPropertyName("startOffsetSec")]
    public double StartOffsetSec { get; set; }
}

/// <summary>
/// Success body for GET /api/search/chapters. Passed through verbatim
/// to stdout by the lookup command — agents use isAccurate and the
/// runtime fields to decide whether to apply.
/// </summary>
public class ChaptersLookupResponse
{
    [JsonPropertyName("asin")]
    public string Asin { get; set; } = "";

    [JsonPropertyName("brandIntroDurationMs")]
    public long BrandIntroDurationMs { get; set; }

    [JsonPropertyName("brandOutroDurationMs")]
    public long BrandOutroDurationMs { get; set; }

    [JsonPropertyName("chapters")]
    public List<AudnexusChapter> Chapters { get; set; } = new();

    [JsonPropertyName("isAccurate")]
    public bool IsAccurate { get; set; }

    [JsonPropertyName("runtimeLengthMs")]
    public long RuntimeLengthMs { get; set; }

    [JsonPropertyName("runtimeLengthSec")]
    public double RuntimeLengthSec { get; set; }
}

/// <summary>
/// In-band error shape ABS returns on HTTP 200 from
/// GET /api/search/chapters for: no Audnexus match, invalid ASIN,
/// invalid region. The presence of a non-null Error field is the
/// discriminator the service uses to detect the failure case.
/// </summary>
public class ChaptersLookupError
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("stringKey")]
    public string? StringKey { get; set; }
}

/// <summary>
/// CLI-internal discriminated wrapper returned by ChaptersService.LookupAsync.
/// Exactly one of Success / Error is non-null. Not serialized over the wire.
/// </summary>
public class ChaptersLookupResult
{
    public ChaptersLookupResponse? Success { get; set; }
    public ChaptersLookupError? Error { get; set; }
}

/// <summary>
/// Single chapter entry in the write shape — the body POST
/// /api/items/:id/chapters expects. Times are in seconds (note the
/// units gap vs AudnexusChapter — conversion is the agent's job).
/// </summary>
public class ChapterWriteEntry
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }
}

/// <summary>
/// Full request body for POST /api/items/:id/chapters. Also serves
/// as the input shape: --input / --stdin payloads deserialize into
/// this type, and a deserialization failure is the CLI's only
/// pre-HTTP validation.
/// </summary>
public class ChaptersSetRequest
{
    [JsonPropertyName("chapters")]
    public List<ChapterWriteEntry> Chapters { get; set; } = new();
}

/// <summary>
/// Response body for POST /api/items/:id/chapters. Updated is false
/// when the posted chapters byte-for-byte match the existing array
/// (same length, same title/start/end per index — see
/// LibraryItemController.js:869-889).
/// </summary>
public class ChaptersSetResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("updated")]
    public bool Updated { get; set; }
}

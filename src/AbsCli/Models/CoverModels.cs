using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/items/:id/cover (URL or multipart) and
/// PATCH /api/items/:id/cover (existing server-side path).
/// Server returns { success: true, cover: "<server-path>" }.
/// </summary>
public class CoverApplyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("cover")]
    public string Cover { get; set; } = "";
}

/// <summary>
/// Body for POST /api/items/:id/cover when applying from a URL.
/// ABS server downloads the URL on receipt.
/// </summary>
public class CoverApplyByUrlRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

/// <summary>
/// Body for PATCH /api/items/:id/cover when pointing to a file already on
/// the ABS server's filesystem. Path must not start with http:/https:.
/// </summary>
public class CoverLinkExistingRequest
{
    [JsonPropertyName("cover")]
    public string Cover { get; set; } = "";
}

/// <summary>
/// Stdout descriptor written by `items cover get --output &lt;file&gt;` after
/// the bytes are saved. Not present when --output is "-" (binary-to-stdout
/// mode emits nothing else on stdout).
/// </summary>
public class CoverFileSavedDescriptor
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }
}

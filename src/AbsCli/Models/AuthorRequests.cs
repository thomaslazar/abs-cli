using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for POST /api/authors/:id/match. Either <see cref="Q"/> (name) or
/// <see cref="Asin"/> is supplied; never both. <see cref="Region"/> is
/// optional — ABS defaults to "us" when absent.
/// </summary>
public class AuthorMatchRequest
{
    [JsonPropertyName("q")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Q { get; set; }

    [JsonPropertyName("asin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Asin { get; set; }

    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; set; }
}

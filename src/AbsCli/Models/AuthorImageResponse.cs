using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/authors/:id/image and DELETE /api/authors/:id/image.
/// Server returns { author: { ... } }.
/// </summary>
public class AuthorImageResponse
{
    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}

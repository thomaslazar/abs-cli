using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/authors/:id/match.
/// Server returns { updated: bool, author: { ... } }.
/// </summary>
public class AuthorMatchResponse
{
    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}

/// <summary>
/// Response from PATCH /api/authors/:id. Two response shapes share this
/// type because the merge path returns a different discriminator:
///   normal: { updated: bool, author: {...} }
///   merge:  { merged: true, author: <existingAuthor> }
/// One of <see cref="Updated"/> / <see cref="Merged"/> is present per
/// response. Consumers read whichever bool is non-null.
/// </summary>
public class AuthorUpdateResponse
{
    [JsonPropertyName("updated")]
    public bool? Updated { get; set; }

    [JsonPropertyName("merged")]
    public bool? Merged { get; set; }

    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}

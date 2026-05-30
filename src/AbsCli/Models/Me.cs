using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// DTO matching <c>User.toOldJSONForBrowser()</c> returned by
/// <c>GET /api/me</c>. Includes the full <see cref="MediaProgress"/> array
/// (can be MB-size on power users; server has no slim variant). The
/// <see cref="Bookmarks"/> array is round-tripped as raw JSON because the
/// CLI doesn't model bookmark shape in detail.
/// </summary>
public class Me
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("isOldToken")]
    public bool IsOldToken { get; set; }

    [JsonPropertyName("permissions")]
    public UserPermissions? Permissions { get; set; }

    [JsonPropertyName("librariesAccessible")]
    public List<string> LibrariesAccessible { get; set; } = new();

    [JsonPropertyName("itemTagsSelected")]
    public List<string> ItemTagsSelected { get; set; } = new();

    [JsonPropertyName("mediaProgress")]
    public List<MediaProgress> MediaProgress { get; set; } = new();

    [JsonPropertyName("bookmarks")]
    public List<JsonElement> Bookmarks { get; set; } = new();

    [JsonPropertyName("seriesHideFromContinueListening")]
    public List<string> SeriesHideFromContinueListening { get; set; } = new();

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("lastSeen")]
    public long? LastSeen { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("hasOpenIDLink")]
    public bool HasOpenIDLink { get; set; }
}

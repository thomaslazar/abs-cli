using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class UserResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("permissions")]
    public UserPermissions? Permissions { get; set; }
}

public class UserPermissions
{
    [JsonPropertyName("download")]
    public bool Download { get; set; }

    [JsonPropertyName("update")]
    public bool Update { get; set; }

    [JsonPropertyName("delete")]
    public bool Delete { get; set; }

    [JsonPropertyName("upload")]
    public bool Upload { get; set; }

    [JsonPropertyName("accessAllLibraries")]
    public bool AccessAllLibraries { get; set; }

    [JsonPropertyName("accessAllTags")]
    public bool AccessAllTags { get; set; }

    [JsonPropertyName("accessExplicitContent")]
    public bool AccessExplicitContent { get; set; }
}

using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class LoginResponse
{
    [JsonPropertyName("user")]
    public UserResponse User { get; set; } = new();

    [JsonPropertyName("userDefaultLibraryId")]
    public string? UserDefaultLibraryId { get; set; }

    [JsonPropertyName("serverSettings")]
    public ServerSettings? ServerSettings { get; set; }
}

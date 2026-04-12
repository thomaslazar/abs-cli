using System.Text.Json.Serialization;

namespace AbsCli.Configuration;

public class AppConfig
{
    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("defaultLibrary")]
    public string? DefaultLibrary { get; set; }
}

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

    // Written by the runtime version check, not by `config set`. See
    // docs/specs/2026-08-12-server-version-check-cadence-design.md.
    [JsonPropertyName("lastVersionCheck")]
    public DateTimeOffset? LastVersionCheck { get; set; }

    [JsonPropertyName("lastServerVersion")]
    public string? LastServerVersion { get; set; }
}

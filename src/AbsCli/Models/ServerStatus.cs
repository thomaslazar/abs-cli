using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// GET /status — unauthenticated, and the only field we need is the version.
/// ABS also returns app, isInit, language and auth settings; ignored here.
/// </summary>
public class ServerStatus
{
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; set; }
}

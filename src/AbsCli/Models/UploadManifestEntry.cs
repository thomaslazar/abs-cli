using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class UploadManifestEntry
{
    [JsonPropertyName("src")]
    public string Src { get; set; } = "";

    [JsonPropertyName("as")]
    public string TargetName { get; set; } = "";
}

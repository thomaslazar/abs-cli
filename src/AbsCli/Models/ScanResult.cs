using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class ScanResult
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BatchUpdateItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("mediaPayload")]
    public JsonElement MediaPayload { get; set; }
}

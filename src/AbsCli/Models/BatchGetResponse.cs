using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BatchGetResponse
{
    [JsonPropertyName("libraryItems")]
    public List<JsonElement> LibraryItems { get; set; } = new();
}

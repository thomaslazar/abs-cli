using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class UpdateMediaResponse
{
    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("libraryItem")]
    public JsonElement LibraryItem { get; set; }
}

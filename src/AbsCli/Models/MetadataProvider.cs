using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class ProviderEntry
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class MetadataProviderGroups
{
    [JsonPropertyName("books")]
    public List<ProviderEntry> Books { get; set; } = new();
    [JsonPropertyName("booksCovers")]
    public List<ProviderEntry> BooksCovers { get; set; } = new();
    [JsonPropertyName("podcasts")]
    public List<ProviderEntry> Podcasts { get; set; } = new();
}

public class MetadataProvidersResponse
{
    [JsonPropertyName("providers")]
    public MetadataProviderGroups Providers { get; set; } = new();
}

public class CoverSearchResponse
{
    [JsonPropertyName("results")]
    public List<string> Results { get; set; } = new();
}

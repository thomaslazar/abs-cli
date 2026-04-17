using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class SearchResult
{
    [JsonPropertyName("book")]
    public List<JsonElement>? Book { get; set; }

    [JsonPropertyName("podcast")]
    public List<JsonElement>? Podcast { get; set; }

    [JsonPropertyName("narrators")]
    public List<JsonElement>? Narrators { get; set; }

    [JsonPropertyName("tags")]
    public List<JsonElement>? Tags { get; set; }

    [JsonPropertyName("genres")]
    public List<JsonElement>? Genres { get; set; }

    [JsonPropertyName("series")]
    public List<JsonElement>? Series { get; set; }

    [JsonPropertyName("authors")]
    public List<JsonElement>? Authors { get; set; }
}

/// <summary>
/// Help-shape wrapper for entries in <see cref="SearchResult.Book"/> and
/// <see cref="SearchResult.Podcast"/>. ABS returns each as
/// <c>{ libraryItem: LibraryItemMinified }</c>. Not serialised at runtime —
/// the live response deserialises into the JsonElement arrays above; this
/// type exists purely so the codegen walker can render the real shape.
/// </summary>
public class SearchItemMatch
{
    [JsonPropertyName("libraryItem")]
    public LibraryItemMinified LibraryItem { get; set; } = new();
}

public class SearchSeriesMatch
{
    [JsonPropertyName("series")]
    public SeriesItem Series { get; set; } = new();

    [JsonPropertyName("books")]
    public List<LibraryItemMinified> Books { get; set; } = new();
}

public class SearchNarratorMatch
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("numBooks")]
    public int NumBooks { get; set; }
}

public class SearchTagGenreMatch
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("numItems")]
    public int NumItems { get; set; }
}

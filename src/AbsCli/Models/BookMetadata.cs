using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BookMetadataMinified
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("titleIgnorePrefix")]
    public string? TitleIgnorePrefix { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("authorName")]
    public string? AuthorName { get; set; }

    [JsonPropertyName("authorNameLF")]
    public string? AuthorNameLF { get; set; }

    [JsonPropertyName("narratorName")]
    public string? NarratorName { get; set; }

    [JsonPropertyName("seriesName")]
    public string? SeriesName { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    [JsonPropertyName("publishedYear")]
    public string? PublishedYear { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("abridged")]
    public bool Abridged { get; set; }
}

public class BookMetadataExpanded
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("authors")]
    public List<AuthorRef> Authors { get; set; } = new();

    [JsonPropertyName("narrators")]
    public List<string> Narrators { get; set; } = new();

    [JsonPropertyName("series")]
    public List<SeriesRef> Series { get; set; } = new();

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    [JsonPropertyName("publishedYear")]
    public string? PublishedYear { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("abridged")]
    public bool Abridged { get; set; }
}

public class AuthorRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class SeriesRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sequence")]
    public string? Sequence { get; set; }
}

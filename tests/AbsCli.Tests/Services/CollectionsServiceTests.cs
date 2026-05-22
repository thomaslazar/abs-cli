using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class CollectionsServiceTests
{
    [Fact]
    public void Collection_RoundTrip_Minimal()
    {
        var obj = new Collection
        {
            Id = "col_abc",
            LibraryId = "lib_1",
            Name = "Light Novels",
            Description = "Curated set",
            Books = new List<LibraryItemExpanded>(),
            LastUpdate = 1716000000000,
            CreatedAt = 1715000000000,
            RssFeed = null
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Collection);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Collection)!;
        Assert.Equal("col_abc", back.Id);
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal("Light Novels", back.Name);
        Assert.Equal("Curated set", back.Description);
        Assert.Empty(back.Books);
        Assert.Equal(1716000000000, back.LastUpdate);
        Assert.Equal(1715000000000, back.CreatedAt);
        Assert.Null(back.RssFeed);
    }

    [Fact]
    public void Collection_Deserializes_NullDescription()
    {
        var json = """{"id":"col_x","libraryId":"lib_1","name":"n","description":null,"books":[],"lastUpdate":0,"createdAt":0}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Collection)!;
        Assert.Null(back.Description);
    }

    [Fact]
    public void RssFeed_RoundTrip_Tolerant()
    {
        var obj = new RssFeed { Id = "feed_1", Slug = "lightnovels", FeedUrl = "http://abs/feed/lightnovels" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.RssFeed);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.RssFeed)!;
        Assert.Equal("feed_1", back.Id);
        Assert.Equal("lightnovels", back.Slug);
        Assert.Equal("http://abs/feed/lightnovels", back.FeedUrl);
    }

    [Fact]
    public void RssFeed_RoundTrip_PreservesUnknownFields()
    {
        // Simulates ABS's actual feed shape with nested `meta` and other
        // fields not explicitly modeled. They must survive a deserialize +
        // serialize cycle via JsonExtensionData.
        var json = """
        {"id":"feed_1","slug":"s","entityUpdatedAt":1234567890,
         "meta":{"title":"My Feed","author":"X"},"episodes":[],
         "createdAt":111,"updatedAt":222}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.RssFeed)!;
        Assert.Equal("feed_1", back.Id);
        Assert.Equal("s", back.Slug);
        Assert.NotNull(back.Extra);
        Assert.True(back.Extra!.ContainsKey("meta"));
        Assert.True(back.Extra.ContainsKey("entityUpdatedAt"));
        var roundtripped = JsonSerializer.Serialize(back, AppJsonContext.Default.RssFeed);
        Assert.Contains("\"meta\"", roundtripped);
        Assert.Contains("\"entityUpdatedAt\"", roundtripped);
        Assert.Contains("\"episodes\"", roundtripped);
    }

    [Fact]
    public void CollectionCreateRequest_RoundTrip()
    {
        var obj = new CollectionCreateRequest
        {
            LibraryId = "lib_1",
            Name = "Light Novels",
            Description = "Curated set",
            Books = new List<string> { "li_a", "li_b" }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionCreateRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionCreateRequest)!;
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal("Light Novels", back.Name);
        Assert.Equal("Curated set", back.Description);
        Assert.Equal(new[] { "li_a", "li_b" }, back.Books);
    }

    [Fact]
    public void CollectionCreateRequest_OmitsNullDescription()
    {
        var obj = new CollectionCreateRequest
        {
            LibraryId = "lib_1",
            Name = "n",
            Description = null,
            Books = new List<string> { "li_a" }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionCreateRequest);
        Assert.DoesNotContain("description", json);
    }

    [Fact]
    public void CollectionBooksRequest_RoundTrip()
    {
        var obj = new CollectionBooksRequest { Books = new List<string> { "li_a", "li_b", "li_c" } };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionBooksRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionBooksRequest)!;
        Assert.Equal(3, back.Books.Count);
        Assert.Equal("li_a", back.Books[0]);
    }

    [Fact]
    public void CollectionBookRequest_RoundTrip()
    {
        var obj = new CollectionBookRequest { Id = "li_z" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionBookRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionBookRequest)!;
        Assert.Equal("li_z", back.Id);
    }
}

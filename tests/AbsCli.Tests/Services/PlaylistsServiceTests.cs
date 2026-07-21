using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class PlaylistsServiceTests
{
    [Fact]
    public void Playlist_RoundTrip_Minimal()
    {
        var obj = new Playlist
        {
            Id = "pl_abc",
            Name = "Roadtrip",
            LibraryId = "lib_1",
            UserId = "usr_1",
            Description = "Long drives",
            LastUpdate = 1716000000000,
            CreatedAt = 1715000000000,
            Items = new List<PlaylistItem>()
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Playlist);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Playlist)!;
        Assert.Equal("pl_abc", back.Id);
        Assert.Equal("Roadtrip", back.Name);
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal("usr_1", back.UserId);
        Assert.Equal("Long drives", back.Description);
        Assert.Equal(1716000000000, back.LastUpdate);
        Assert.Equal(1715000000000, back.CreatedAt);
        Assert.Empty(back.Items);
    }

    [Fact]
    public void Playlist_Deserializes_BookItem()
    {
        var json = """
        {"id":"pl_x","name":"n","libraryId":"lib_1","userId":"u","description":null,
         "lastUpdate":0,"createdAt":0,
         "items":[{"libraryItemId":"li_a","libraryItem":{"id":"li_a","libraryId":"lib_1"}}]}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Playlist)!;
        Assert.Null(back.Description);
        Assert.Single(back.Items);
        Assert.Equal("li_a", back.Items[0].LibraryItemId);
        Assert.NotNull(back.Items[0].LibraryItem);
        Assert.Equal("li_a", back.Items[0].LibraryItem!.Id);
        Assert.Null(back.Items[0].EpisodeId);
    }

    [Fact]
    public void PlaylistCreateRequest_RoundTrip_AndOmitsNullDescription()
    {
        var obj = new PlaylistCreateRequest
        {
            LibraryId = "lib_1",
            Name = "Roadtrip",
            Description = null,
            Items = new List<PlaylistItemRef>
            {
                new() { LibraryItemId = "li_a" },
                new() { LibraryItemId = "li_b" }
            }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistCreateRequest);
        Assert.DoesNotContain("description", json);
        Assert.Contains("\"libraryItemId\": \"li_a\"", json);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistCreateRequest)!;
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal(2, back.Items.Count);
    }

    [Fact]
    public void PlaylistItemsRequest_RoundTrip()
    {
        var obj = new PlaylistItemsRequest
        {
            Items = new List<PlaylistItemRef> { new() { LibraryItemId = "li_a" } }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistItemsRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistItemsRequest)!;
        Assert.Single(back.Items);
        Assert.Equal("li_a", back.Items[0].LibraryItemId);
    }

    [Fact]
    public void PlaylistItemRef_RoundTrip()
    {
        var obj = new PlaylistItemRef { LibraryItemId = "li_z" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistItemRef);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistItemRef)!;
        Assert.Equal("li_z", back.LibraryItemId);
    }
}

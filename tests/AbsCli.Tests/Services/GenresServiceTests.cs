using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class GenresServiceTests
{
    [Fact]
    public void GenreListResponse_Deserializes()
    {
        var json = """{"genres":["Horror","Mystery"]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreListResponse)!;
        Assert.Equal(new[] { "Horror", "Mystery" }, back.Genres);
    }

    [Fact]
    public void GenreRenameRequest_Serializes_AbsFieldNames()
    {
        var req = new GenreRenameRequest { Genre = "horror", NewGenre = "Horror" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.GenreRenameRequest);
        Assert.Contains("\"genre\": \"horror\"", json);
        Assert.Contains("\"newGenre\": \"Horror\"", json);
    }

    [Fact]
    public void GenreRenameResponse_Deserializes()
    {
        var json = """{"genreMerged":false,"numItemsUpdated":2}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreRenameResponse)!;
        Assert.False(back.GenreMerged);
        Assert.Equal(2, back.NumItemsUpdated);
    }

    [Fact]
    public void GenreDeleteResponse_Deserializes()
    {
        var json = """{"numItemsUpdated":0}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreDeleteResponse)!;
        Assert.Equal(0, back.NumItemsUpdated);
    }
}

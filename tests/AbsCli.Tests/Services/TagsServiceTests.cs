using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class TagsServiceTests
{
    [Fact]
    public void TagListResponse_Deserializes()
    {
        var json = """{"tags":["Fantasy","Sci-Fi"]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagListResponse)!;
        Assert.Equal(new[] { "Fantasy", "Sci-Fi" }, back.Tags);
    }

    [Fact]
    public void TagRenameRequest_Serializes_AbsFieldNames()
    {
        var req = new TagRenameRequest { Tag = "scifi", NewTag = "Science Fiction" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.TagRenameRequest);
        Assert.Contains("\"tag\": \"scifi\"", json);
        Assert.Contains("\"newTag\": \"Science Fiction\"", json);
    }

    [Fact]
    public void TagRenameResponse_Deserializes()
    {
        var json = """{"tagMerged":true,"numItemsUpdated":3}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagRenameResponse)!;
        Assert.True(back.TagMerged);
        Assert.Equal(3, back.NumItemsUpdated);
    }

    [Fact]
    public void TagDeleteResponse_Deserializes()
    {
        var json = """{"numItemsUpdated":5}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagDeleteResponse)!;
        Assert.Equal(5, back.NumItemsUpdated);
    }
}

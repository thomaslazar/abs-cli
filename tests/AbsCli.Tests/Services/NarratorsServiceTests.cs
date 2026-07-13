using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class NarratorsServiceTests
{
    [Fact]
    public void NarratorListResponse_Deserializes()
    {
        var json = """{"narrators":[{"id":"Um9iIEluZ2xpcw==","name":"Rob Inglis","numBooks":3}]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.NarratorListResponse)!;
        Assert.Single(back.Narrators);
        Assert.Equal("Um9iIEluZ2xpcw==", back.Narrators[0].Id);
        Assert.Equal("Rob Inglis", back.Narrators[0].Name);
        Assert.Equal(3, back.Narrators[0].NumBooks);
    }

    [Fact]
    public void NarratorRenameRequest_Serializes_NameField()
    {
        var req = new NarratorRenameRequest { Name = "Robert Inglis" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.NarratorRenameRequest);
        Assert.Contains("\"name\": \"Robert Inglis\"", json);
    }

    [Fact]
    public void NarratorUpdateResponse_Deserializes()
    {
        var json = """{"updated":4}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.NarratorUpdateResponse)!;
        Assert.Equal(4, back.Updated);
    }
}

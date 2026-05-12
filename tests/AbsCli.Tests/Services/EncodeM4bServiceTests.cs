using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class EncodeM4bServiceTests
{
    [Fact]
    public void EncodeM4bOptions_AllFields_RoundTrip()
    {
        var obj = new EncodeM4bOptions { Codec = "aac", Bitrate = "128k", Channels = 2 };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bOptions);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EncodeM4bOptions)!;
        Assert.Equal("aac", back.Codec);
        Assert.Equal("128k", back.Bitrate);
        Assert.Equal(2, back.Channels);
    }

    [Fact]
    public void EncodeM4bOptions_OmitsUnsetFields()
    {
        var obj = new EncodeM4bOptions { Codec = "copy" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bOptions);
        Assert.Contains("\"codec\":\"copy\"", json);
        Assert.DoesNotContain("bitrate", json);
        Assert.DoesNotContain("channels", json);
    }

    [Fact]
    public void EncodeM4bOptions_AllUnset_SerialisesEmpty()
    {
        var obj = new EncodeM4bOptions();
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bOptions);
        Assert.Equal("{}", json);
    }

    [Fact]
    public void EncodeM4bStartReceipt_RoundTrip()
    {
        var obj = new EncodeM4bStartReceipt
        {
            LibraryItemId = "li_abc123",
            Action = "encode-m4b",
            Started = true,
            Options = new EncodeM4bOptions { Codec = "copy" }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bStartReceipt);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EncodeM4bStartReceipt)!;
        Assert.Equal("li_abc123", back.LibraryItemId);
        Assert.Equal("encode-m4b", back.Action);
        Assert.True(back.Started);
        Assert.Equal("copy", back.Options.Codec);
    }
}

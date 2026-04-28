using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class CoversServiceTests
{
    [Fact]
    public void CoverApplyResponse_RoundTrip()
    {
        var obj = new CoverApplyResponse { Success = true, Cover = "/srv/abs/covers/foo.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyResponse);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyResponse)!;
        Assert.True(back.Success);
        Assert.Equal("/srv/abs/covers/foo.jpg", back.Cover);
    }

    [Fact]
    public void CoverApplyByUrlRequest_RoundTrip()
    {
        var obj = new CoverApplyByUrlRequest { Url = "https://example.com/cover.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyByUrlRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyByUrlRequest)!;
        Assert.Equal("https://example.com/cover.jpg", back.Url);
    }

    [Fact]
    public void CoverLinkExistingRequest_RoundTrip()
    {
        var obj = new CoverLinkExistingRequest { Cover = "/srv/abs/library/Author/Title/cover.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverLinkExistingRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverLinkExistingRequest)!;
        Assert.Equal("/srv/abs/library/Author/Title/cover.jpg", back.Cover);
    }

    [Fact]
    public void CoverFileSavedDescriptor_RoundTrip()
    {
        var obj = new CoverFileSavedDescriptor { Path = "/tmp/cover.jpg", Bytes = 12345 };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverFileSavedDescriptor);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverFileSavedDescriptor)!;
        Assert.Equal("/tmp/cover.jpg", back.Path);
        Assert.Equal(12345, back.Bytes);
    }
}

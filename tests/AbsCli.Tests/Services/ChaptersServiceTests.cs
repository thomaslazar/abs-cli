using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class ChaptersServiceTests
{
    [Fact]
    public void ChaptersLookupResponse_RoundTrip()
    {
        var obj = new ChaptersLookupResponse
        {
            Asin = "B07TEST1",
            BrandIntroDurationMs = 100,
            BrandOutroDurationMs = 200,
            Chapters = new List<AudnexusChapter>
            {
                new() { Title = "Ch 1", LengthMs = 12345, StartOffsetMs = 0, StartOffsetSec = 0 }
            },
            IsAccurate = true,
            RuntimeLengthMs = 50000,
            RuntimeLengthSec = 50.0
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersLookupResponse);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupResponse)!;
        Assert.Equal("B07TEST1", back.Asin);
        Assert.Single(back.Chapters);
        Assert.Equal("Ch 1", back.Chapters[0].Title);
        Assert.Equal(12345, back.Chapters[0].LengthMs);
        Assert.True(back.IsAccurate);
    }

    [Fact]
    public void ChaptersLookupError_RoundTrip()
    {
        var obj = new ChaptersLookupError { Error = "Chapters not found", StringKey = "MessageChaptersNotFound" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersLookupError);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupError)!;
        Assert.Equal("Chapters not found", back.Error);
        Assert.Equal("MessageChaptersNotFound", back.StringKey);
    }

    [Fact]
    public void ChaptersLookupError_DeserializesFromSuccessBody_AsAllNull()
    {
        // The discriminator pattern: deserializing a success-shaped body
        // through the error type yields null Error/StringKey (extra fields
        // are silently ignored by STJ). Service code uses Error == null to
        // know the response is the success variant.
        var successJson = "{\"asin\":\"B07T\",\"chapters\":[],\"isAccurate\":true}";
        var back = JsonSerializer.Deserialize(successJson, AppJsonContext.Default.ChaptersLookupError)!;
        Assert.Null(back.Error);
        Assert.Null(back.StringKey);
    }

    [Fact]
    public void ChaptersSetRequest_RoundTrip()
    {
        var obj = new ChaptersSetRequest
        {
            Chapters = new List<ChapterWriteEntry>
            {
                new() { Title = "Intro", Start = 0, End = 1.5 },
                new() { Title = "Outro", Start = 1.5, End = 3.0 }
            }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersSetRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersSetRequest)!;
        Assert.Equal(2, back.Chapters.Count);
        Assert.Equal("Intro", back.Chapters[0].Title);
        Assert.Equal(0, back.Chapters[0].Start);
        Assert.Equal(1.5, back.Chapters[0].End);
    }

    [Fact]
    public void ChaptersSetRequest_MissingNumericFieldsBecomeDefaults()
    {
        // Source-gen does NOT throw on missing numeric fields — they
        // default to 0. The CLI relies on ABS to reject this with 400
        // (the service forwards the body verbatim).
        var malformed = "{\"chapters\":[{\"title\":\"only-title\"}]}";
        var back = JsonSerializer.Deserialize(malformed, AppJsonContext.Default.ChaptersSetRequest)!;
        Assert.Equal("only-title", back.Chapters[0].Title);
        Assert.Equal(0, back.Chapters[0].Start);
        Assert.Equal(0, back.Chapters[0].End);
    }

    [Fact]
    public void ChaptersSetRequest_RejectsWrongTypes()
    {
        // Wrong types do throw at deserialization (e.g. start as string).
        // This is the CLI's primary input-shape gate.
        var wrongTypes = "{\"chapters\":[{\"title\":\"x\",\"start\":\"not-a-number\",\"end\":3.0}]}";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(wrongTypes, AppJsonContext.Default.ChaptersSetRequest));
    }

    [Fact]
    public void ChaptersSetResponse_RoundTrip()
    {
        var obj = new ChaptersSetResponse { Success = true, Updated = false };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersSetResponse);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersSetResponse)!;
        Assert.True(back.Success);
        Assert.False(back.Updated);
    }
}

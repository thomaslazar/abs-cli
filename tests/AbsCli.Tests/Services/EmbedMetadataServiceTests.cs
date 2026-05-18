using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class EmbedMetadataServiceTests
{
    [Fact]
    public void EmbedMetadataOptions_Defaults_BackupOnChaptersOff()
    {
        var obj = new EmbedMetadataOptions();
        Assert.True(obj.Backup);
        Assert.False(obj.ForceEmbedChapters);
    }

    [Fact]
    public void EmbedMetadataOptions_RoundTrip()
    {
        var obj = new EmbedMetadataOptions { Backup = false, ForceEmbedChapters = true };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EmbedMetadataOptions);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EmbedMetadataOptions)!;
        Assert.False(back.Backup);
        Assert.True(back.ForceEmbedChapters);
    }

    [Fact]
    public void EmbedMetadataReceipt_RoundTrip()
    {
        var obj = new EmbedMetadataReceipt
        {
            LibraryItemId = "li_abc123",
            Started = true,
            Options = new EmbedMetadataOptions { Backup = true, ForceEmbedChapters = false }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EmbedMetadataReceipt);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EmbedMetadataReceipt)!;
        Assert.Equal("li_abc123", back.LibraryItemId);
        Assert.Equal("embed-metadata", back.Action);
        Assert.True(back.Started);
        Assert.True(back.Options.Backup);
        Assert.False(back.Options.ForceEmbedChapters);
    }

    [Fact]
    public void BatchEmbedMetadataRequest_RoundTrip()
    {
        var obj = new BatchEmbedMetadataRequest
        {
            LibraryItemIds = new List<string> { "li_a", "li_b" }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BatchEmbedMetadataRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchEmbedMetadataRequest)!;
        Assert.Equal(2, back.LibraryItemIds.Count);
        Assert.Equal("li_a", back.LibraryItemIds[0]);
        Assert.Equal("li_b", back.LibraryItemIds[1]);
    }

    [Fact]
    public void BatchEmbedMetadataRequest_RejectsWrongTypes()
    {
        var wrong = "{\"libraryItemIds\":\"not-an-array\"}";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(wrong, AppJsonContext.Default.BatchEmbedMetadataRequest));
    }

    [Fact]
    public void BatchEmbedMetadataReceipt_RoundTrip()
    {
        var obj = new BatchEmbedMetadataReceipt
        {
            Started = true,
            LibraryItemIds = new List<string> { "li_a", "li_b" },
            Options = new EmbedMetadataOptions { Backup = true, ForceEmbedChapters = true }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BatchEmbedMetadataReceipt);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchEmbedMetadataReceipt)!;
        Assert.Equal("embed-metadata", back.Action);
        Assert.True(back.Started);
        Assert.Equal(2, back.LibraryItemIds.Count);
        Assert.True(back.Options.ForceEmbedChapters);
    }
}

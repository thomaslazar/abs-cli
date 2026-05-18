using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class EbookFileTests
{
    [Fact]
    public void EbookFileStatusReceipt_RoundTrip()
    {
        var obj = new EbookFileStatusReceipt
        {
            LibraryItemId = "li_abc123",
            FileIno = "12345678",
            Toggled = true
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EbookFileStatusReceipt);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EbookFileStatusReceipt)!;
        Assert.Equal("li_abc123", back.LibraryItemId);
        Assert.Equal("12345678", back.FileIno);
        Assert.Equal("toggle-ebook-status", back.Action);
        Assert.True(back.Toggled);
    }
}

using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class LibrariesServiceTests
{
    [Fact]
    public void LibraryCreateRequest_SerializesFoldersAndName()
    {
        var req = new LibraryCreateRequest
        {
            Name = "Audiobooks",
            Folders = new List<LibraryFolderRequest> { new() { FullPath = "/audiobooks" } }
        };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryCreateRequest);
        Assert.Contains("\"name\": \"Audiobooks\"", json);
        Assert.Contains("\"fullPath\": \"/audiobooks\"", json);
        Assert.DoesNotContain("mediaType", json);
        Assert.DoesNotContain("provider", json);
        Assert.DoesNotContain("icon", json);
    }

    [Fact]
    public void LibraryCreateRequest_IncludesOptionalsWhenSet()
    {
        var req = new LibraryCreateRequest
        {
            Name = "Pods",
            Folders = new List<LibraryFolderRequest> { new() { FullPath = "/pods" } },
            MediaType = "podcast",
            Provider = "itunes",
            Icon = "podcast"
        };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryCreateRequest);
        Assert.Contains("\"mediaType\": \"podcast\"", json);
        Assert.Contains("\"provider\": \"itunes\"", json);
        Assert.Contains("\"icon\": \"podcast\"", json);
    }

    [Fact]
    public void LibraryUpdateRequest_OmitsNullFields()
    {
        var req = new LibraryUpdateRequest { Name = "Renamed" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryUpdateRequest);
        Assert.Contains("\"name\": \"Renamed\"", json);
        Assert.DoesNotContain("mediaType", json);
        Assert.DoesNotContain("displayOrder", json);
    }

    [Fact]
    public void LibraryUpdateRequest_IncludesDisplayOrder()
    {
        var req = new LibraryUpdateRequest { DisplayOrder = 3 };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryUpdateRequest);
        Assert.Contains("\"displayOrder\": 3", json);
        Assert.DoesNotContain("\"name\"", json);
    }
}

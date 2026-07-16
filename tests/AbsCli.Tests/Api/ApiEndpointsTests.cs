using AbsCli.Api;
using Xunit;

namespace AbsCli.Tests.Api;

public class ApiEndpointsTests
{
    [Fact]
    public void TagByName_Base64EncodesThenUriEscapes()
    {
        // "a" -> base64 "YQ==" -> URI-escaped "YQ%3D%3D"
        Assert.Equal("api/tags/YQ%3D%3D", ApiEndpoints.TagByName("a"));
    }

    [Fact]
    public void TagByName_HandlesSpecialCharacters()
    {
        // "sci/fi" -> base64 "c2NpL2Zp" (no escapable chars)
        Assert.Equal("api/tags/c2NpL2Zp", ApiEndpoints.TagByName("sci/fi"));
    }

    [Fact]
    public void GenreByName_Base64EncodesThenUriEscapes()
    {
        Assert.Equal("api/genres/YQ%3D%3D", ApiEndpoints.GenreByName("a"));
    }

    [Fact]
    public void TagAndGenreConstants_AreStable()
    {
        Assert.Equal("api/tags", ApiEndpoints.Tags);
        Assert.Equal("api/tags/rename", ApiEndpoints.TagRename);
        Assert.Equal("api/genres", ApiEndpoints.Genres);
        Assert.Equal("api/genres/rename", ApiEndpoints.GenreRename);
    }

    [Fact]
    public void LibraryNarrators_BuildsListPath()
    {
        Assert.Equal("api/libraries/lib_1/narrators", ApiEndpoints.LibraryNarrators("lib_1"));
    }

    [Fact]
    public void LibraryNarratorByName_Base64EncodesThenUriEscapes()
    {
        // "a" -> base64 "YQ==" -> URI-escaped "YQ%3D%3D"
        Assert.Equal("api/libraries/lib_1/narrators/YQ%3D%3D", ApiEndpoints.LibraryNarratorByName("lib_1", "a"));
    }

    [Fact]
    public void ItemFile_BuildsPath()
    {
        Assert.Equal("api/items/li_1/file/12345", ApiEndpoints.ItemFile("li_1", "12345"));
    }

    [Fact]
    public void ItemFileDownload_BuildsPath()
    {
        Assert.Equal("api/items/li_1/file/12345/download", ApiEndpoints.ItemFileDownload("li_1", "12345"));
    }

    [Fact]
    public void ItemFfprobe_BuildsPath()
    {
        Assert.Equal("api/items/li_1/ffprobe/12345", ApiEndpoints.ItemFfprobe("li_1", "12345"));
    }

    [Fact]
    public void LibrariesOrder_IsStable()
    {
        Assert.Equal("api/libraries/order", ApiEndpoints.LibrariesOrder);
    }
}

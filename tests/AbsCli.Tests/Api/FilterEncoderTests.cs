using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class FilterEncoderTests
{
    [Fact]
    public void Encode_GenreFilter_EncodesValueToBase64()
    {
        var result = FilterEncoder.Encode("genres=Sci Fi");

        Assert.Equal("genres.U2NpIEZp", result);
    }

    [Fact]
    public void Encode_LanguageFilter_EncodesValueToBase64()
    {
        var result = FilterEncoder.Encode("languages=English");

        Assert.Equal("languages.RW5nbGlzaA==", result);
    }

    [Fact]
    public void Encode_EmptyValue_EncodesEmptyString()
    {
        var result = FilterEncoder.Encode("languages=");

        Assert.Equal("languages.", result);
    }

    [Fact]
    public void Encode_AlreadyEncoded_PassesThrough()
    {
        var result = FilterEncoder.Encode("genres.U2NpIEZp");

        Assert.Equal("genres.U2NpIEZp", result);
    }

    [Fact]
    public void Encode_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FilterEncoder.Encode("invalid"));
    }
}

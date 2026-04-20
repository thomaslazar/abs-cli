using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class FilenameSanitizerTests
{
    [Fact]
    public void Colon_FirstOccurrenceReplacedWithSeparator()
    {
        Assert.Equal("Alien - 3", FilenameSanitizer.Sanitize("Alien: 3"));
    }

    [Fact]
    public void Colon_OnlyFirstIsReplaced()
    {
        // Second colon is stripped (it's in the illegal-char set).
        Assert.Equal("a - b - c", FilenameSanitizer.Sanitize("a: b - c").Replace("  ", " "));
        // Explicit check: only one " - " pair in output for multi-colon input.
        var input = "a:b:c";
        var output = FilenameSanitizer.Sanitize(input);
        Assert.Equal("a - bc", output);
    }

    [Theory]
    [InlineData("a/b", "ab")]
    [InlineData("a?b", "ab")]
    [InlineData("a<b>c", "abc")]
    [InlineData("a\\b", "ab")]
    [InlineData("a*b", "ab")]
    [InlineData("a|b", "ab")]
    [InlineData("a\"b", "ab")]
    public void IllegalPathChars_Stripped(string input, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(input));
    }

    [Fact]
    public void ControlChars_Stripped()
    {
        // Strips ASCII controls 0x00-0x1f and C1 controls 0x80-0x9f. Note ABS
        // does NOT strip DEL (0x7f) — it's outside both ranges.
        // Using \u escapes (always 4 hex digits) — \x is greedy and swallows
        // the trailing 'b' as a hex digit.
        Assert.Equal("ab", FilenameSanitizer.Sanitize("a\u0001b"));
        Assert.Equal("a\u007fb", FilenameSanitizer.Sanitize("a\u007fb"));
        Assert.Equal("ab", FilenameSanitizer.Sanitize("a\u009fb"));
        // Tab (0x09) is in the ASCII control range → stripped.
        Assert.Equal("ab", FilenameSanitizer.Sanitize("a\tb"));
    }

    [Fact]
    public void LineBreaks_Stripped()
    {
        Assert.Equal("ab", FilenameSanitizer.Sanitize("a\nb"));
        Assert.Equal("ab", FilenameSanitizer.Sanitize("a\rb"));
    }

    [Fact]
    public void DotsOnly_Stripped()
    {
        // Whole-string sequences of dots are removed.
        Assert.Equal("", FilenameSanitizer.Sanitize("..."));
        Assert.Equal("", FilenameSanitizer.Sanitize("."));
    }

    [Theory]
    [InlineData("con")]
    [InlineData("CON")]
    [InlineData("prn")]
    [InlineData("aux")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("com9")]
    [InlineData("lpt1")]
    [InlineData("lpt9")]
    [InlineData("con.txt")]
    public void WindowsReserved_Stripped(string input)
    {
        Assert.Equal("", FilenameSanitizer.Sanitize(input));
    }

    [Fact]
    public void TrailingDotsAndSpaces_Stripped()
    {
        Assert.Equal("J.R.R", FilenameSanitizer.Sanitize("J.R.R."));
        Assert.Equal("a", FilenameSanitizer.Sanitize("a   "));
        Assert.Equal("a", FilenameSanitizer.Sanitize("a. . ."));
    }

    [Fact]
    public void WhitespaceRuns_CollapsedToSingleSpace()
    {
        Assert.Equal("a b c", FilenameSanitizer.Sanitize("a    b   c"));
    }

    [Fact]
    public void NfcNormalisation_AppliedBeforeOtherRules()
    {
        // "é" as NFD (e + combining acute) must be normalised to NFC single char.
        var nfd = "e\u0301";
        var result = FilenameSanitizer.Sanitize(nfd);
        Assert.Equal("é", result);
    }

    [Fact]
    public void LongBasename_TruncatedToByteLimit()
    {
        // UTF-16 is 2 bytes per ASCII char → 300 chars = 600 bytes, exceeds 255.
        var longName = new string('a', 300);
        var result = FilenameSanitizer.Sanitize(longName);
        // MaxFilenameBytes / 2 = 127 chars for ASCII-only.
        Assert.Equal(127, result.Length);
    }

    [Fact]
    public void PredictRelPath_JoinsSanitisedPartsWithSlash()
    {
        Assert.Equal("Author/Series/Title",
            FilenameSanitizer.PredictRelPath("Author", "Series", "Title"));
    }

    [Fact]
    public void PredictRelPath_DropsNullAndEmptyParts()
    {
        Assert.Equal("Author/Title",
            FilenameSanitizer.PredictRelPath("Author", null, "Title"));
        Assert.Equal("Title",
            FilenameSanitizer.PredictRelPath(null, null, "Title"));
    }

    [Fact]
    public void PredictRelPath_SanitisesEachPart()
    {
        // Author "Tolkien, J.R.R." has trailing dot stripped → "Tolkien, J.R.R"
        // Title "Alien: 3" has colon replaced → "Alien - 3"
        Assert.Equal("Tolkien, J.R.R/Alien - 3",
            FilenameSanitizer.PredictRelPath("Tolkien, J.R.R.", null, "Alien: 3"));
    }

    [Fact]
    public void PredictRelPath_DropsPartsThatSanitiseToEmpty()
    {
        // Series is just ":" → becomes "" after sanitise (colon replaced by
        // " - ", then trailing-space strip, then whitespace-collapse, but
        // leading " - " has no trailing anchor to match… actually ": " →
        // " - " after colon replace, then WhitespaceRun normalises to " - ",
        // which is non-empty. So use a surer empty case: ".." sanitises to "".
        Assert.Equal("Author/Title",
            FilenameSanitizer.PredictRelPath("Author", "...", "Title"));
    }
}

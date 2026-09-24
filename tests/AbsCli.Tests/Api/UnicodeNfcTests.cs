using System.Text;
using AbsCli.Api;

namespace AbsCli.Tests.Api;

// The test project does not use InvariantGlobalization, so ICU's
// string.Normalize is live and serves as the reference implementation.
public class UnicodeNfcTests
{
    [Fact]
    public void IcuReference_IsLive()
    {
        Assert.Equal("\u00F6", "o\u0308".Normalize(NormalizationForm.FormC));
    }

    [Fact]
    public void Ascii_ReturnsSameInstance()
    {
        var s = "Plain Title";
        Assert.Same(s, UnicodeNfc.Compose(s));
    }

    [Fact]
    public void EveryScalar_MatchesIcu()
    {
        var failures = new List<string>();
        for (int cp = 0; cp <= 0x10FFFF; cp++)
        {
            if (cp is >= 0xD800 and <= 0xDFFF) continue;
            // ICU's Normalize throws ArgumentException for U+FFFE on this runtime.
            if (cp == 0xFFFE) continue;
            var c = char.ConvertFromUtf32(cp);
            var expected = c.Normalize(NormalizationForm.FormC);
            if (UnicodeNfc.Compose(c) != expected) failures.Add($"U+{cp:X4}");
            if (UnicodeNfc.Compose(c.Normalize(NormalizationForm.FormD)) != expected) failures.Add($"NFD(U+{cp:X4})");
        }
        Assert.True(failures.Count == 0, $"{failures.Count} mismatches: {string.Join(", ", failures.Take(20))}");
    }

    [Theory]
    [InlineData("Die Lo\u0308win von Neetha")]          // issue #97 (DNB)
    [InlineData("Franc\u0327ois Mauriac")]
    [InlineData("Tie\u0302\u0301ng Vie\u0323\u0302t")]  // stacked, canonically ordered
    [InlineData("\u1112\u1161\u11AB\u1100\u1173\u11AF")] // Hangul jamo
    [InlineData("\u212B + \u0301")]                      // singleton
    [InlineData("A\u030A\u0301")]                        // composes twice
    [InlineData("\u0958 test")]                          // composition exclusion
    [InlineData("\u0301 leading mark")]
    public void Corpus_MatchesIcu(string input)
    {
        Assert.Equal(input.Normalize(NormalizationForm.FormC), UnicodeNfc.Compose(input));
        var nfd = input.Normalize(NormalizationForm.FormD);
        Assert.Equal(nfd.Normalize(NormalizationForm.FormC), UnicodeNfc.Compose(nfd));
    }

    // Accepted divergences (no canonical reordering, no ccc-based blocking).
    // Pinned so a future change to either is deliberate.
    [Fact]
    public void KnownGap_OutOfOrderMarks()
    {
        Assert.Equal("\u1EA1\u0301", "a\u0301\u0323".Normalize(NormalizationForm.FormC));
        Assert.Equal("\u00E1\u0323", UnicodeNfc.Compose("a\u0301\u0323"));
    }

    [Fact]
    public void KnownGap_EqualClassBlocking()
    {
        Assert.Equal("a\u0310\u0301", "a\u0310\u0301".Normalize(NormalizationForm.FormC));
        Assert.Equal("\u00E1\u0310", UnicodeNfc.Compose("a\u0310\u0301"));
    }
}

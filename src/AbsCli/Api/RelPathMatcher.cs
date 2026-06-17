using System.Text;

namespace AbsCli.Api;

/// <summary>
/// Matches a CLI-predicted relPath against the relPath ABS actually stored.
/// The two diverge only in the truncated tail of long path segments (ABS
/// truncates each segment to ~255 UTF-16 bytes and re-appends what
/// <c>Path.extname</c> treats as an extension — see
/// <c>temp/audiobookshelf/server/utils/fileUtils.js</c>). We therefore compare
/// per segment: short segments must be equal; long (near-limit) segments may
/// differ in the tail provided they share a long common prefix.
/// </summary>
public static class RelPathMatcher
{
    // UTF-16 byte length at/above which a segment may have been truncated by
    // ABS. ABS's limit is 255; the margin covers the re-appended extension.
    private const int NearTruncationLimitBytes = 247;

    // Minimum common-prefix length (chars) for two differing near-limit
    // segments to count as the same segment truncated differently. Comfortably
    // below the ~127-char truncation point and above any realistic
    // distinguishing prefix (differing authors/series/sequences diverge far
    // earlier — e.g. the "N. -" title prefix diverges at char 0).
    private const int MinTruncatedSegmentPrefix = 100;

    /// <summary>True if a candidate relPath matches the predicted relPath.</summary>
    public static bool IsMatch(string predicted, string actual)
    {
        var p = predicted.Split('/');
        var a = actual.Split('/');
        if (p.Length != a.Length) return false;
        for (int i = 0; i < p.Length; i++)
        {
            if (!SegmentMatch(p[i], a[i])) return false;
        }
        return true;
    }

    /// <summary>All candidate relPaths that match <paramref name="predicted"/>.</summary>
    public static List<string> Matches(string predicted, IEnumerable<string> candidates)
    {
        var result = new List<string>();
        foreach (var c in candidates)
        {
            if (IsMatch(predicted, c)) result.Add(c);
        }
        return result;
    }

    private static bool SegmentMatch(string p, string a)
    {
        if (string.Equals(p, a, StringComparison.Ordinal)) return true;
        // Segments may only legitimately differ if one looks truncated;
        // otherwise they are simply different segments.
        if (!IsNearLimit(p) && !IsNearLimit(a)) return false;
        return CommonPrefixLength(p, a) >= MinTruncatedSegmentPrefix;
    }

    private static bool IsNearLimit(string s) =>
        Encoding.Unicode.GetByteCount(s) >= NearTruncationLimitBytes;

    private static int CommonPrefixLength(string x, string y)
    {
        int n = Math.Min(x.Length, y.Length);
        int i = 0;
        while (i < n && x[i] == y[i]) i++;
        return i;
    }
}

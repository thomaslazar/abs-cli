using System.Text;
using System.Text.RegularExpressions;

namespace AbsCli.Api;

/// <summary>
/// Port of ABS's <c>sanitizeFilename</c> (see
/// <c>temp/audiobookshelf/server/utils/fileUtils.js</c>). ABS applies this to
/// every directory part (author, series, title) on upload, so the CLI runs
/// the same transformation to predict the <c>relPath</c> ABS will assign to
/// the resulting library item.
///
/// Behaviour that must stay in sync with ABS:
///   1. NFC normalise.
///   2. Replace first ':' with ' - ' (configurable separator).
///   3. Strip illegal chars: / ? &lt; &gt; \ * | "
///   4. Strip ASCII control chars (0x00-0x1f) and C1 controls (0x80-0x9f).
///   5. Strip whole-string sequences of '.' only.
///   6. Strip line breaks (\n, \r).
///   7. Strip Windows reserved basenames (con, prn, aux, nul, com[0-9], lpt[0-9]).
///   8. Strip trailing sequences of '.' or ' '.
///   9. Collapse runs of whitespace to a single space.
///  10. If basename in UTF-16 bytes &gt; 255, truncate the basename (ext preserved).
///
/// Drift risk: if ABS changes sanitise rules, CLI predictions desync silently.
/// Smoke-test asserts that upload round-trips produce the same relPath we
/// predicted — catches drift before it ships.
/// </summary>
public static class FilenameSanitizer
{
    private const int MaxFilenameBytes = 255;

    private static readonly Regex IllegalChars = new(@"[/\?<>\\:\*\|""]", RegexOptions.Compiled);
    private static readonly Regex ControlChars = new(@"[\x00-\x1f\x80-\x9f]", RegexOptions.Compiled);
    private static readonly Regex DotsOnly = new(@"^\.+$", RegexOptions.Compiled);
    private static readonly Regex LineBreaks = new(@"[\n\r]", RegexOptions.Compiled);
    private static readonly Regex WindowsReserved = new(@"^(con|prn|aux|nul|com[0-9]|lpt[0-9])(\..*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrailingDotsOrSpaces = new(@"[\. ]+$", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Sanitises a single path component the way ABS does on upload.</summary>
    public static string Sanitize(string filename, string colonReplacement = " - ")
    {
        if (filename == null) throw new ArgumentNullException(nameof(filename));

        var s = filename.Normalize(NormalizationForm.FormC);

        // JS's String.prototype.replace with a non-regex pattern only replaces the first match.
        var firstColon = s.IndexOf(':');
        if (firstColon >= 0)
            s = s[..firstColon] + colonReplacement + s[(firstColon + 1)..];

        s = IllegalChars.Replace(s, "");
        s = ControlChars.Replace(s, "");
        s = DotsOnly.Replace(s, "");
        s = LineBreaks.Replace(s, "");
        s = WindowsReserved.Replace(s, "");
        s = TrailingDotsOrSpaces.Replace(s, "");
        s = WhitespaceRun.Replace(s, " ");

        return TruncateToByteLimit(s);
    }

    /// <summary>
    /// Compute the relPath ABS will assign to an uploaded library item, given
    /// the request's author/series/title values. Parts that are null or empty
    /// after sanitising are dropped (ABS does <c>.filter(Boolean)</c> on the
    /// parts array before joining).
    /// </summary>
    public static string PredictRelPath(string? author, string? series, string title)
    {
        var parts = new List<string>();
        foreach (var raw in new[] { author, series, title })
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var sanitized = Sanitize(raw);
            if (!string.IsNullOrEmpty(sanitized)) parts.Add(sanitized);
        }
        return string.Join('/', parts);
    }

    private static string TruncateToByteLimit(string s)
    {
        // ABS measures basename only (extension preserved). On upload ABS sanitises
        // each directory part separately before Path.join, so there's no extension
        // here — we treat the whole string as basename.
        var byteLen = Encoding.Unicode.GetByteCount(s);
        if (byteLen <= MaxFilenameBytes) return s;

        var sb = new StringBuilder();
        var total = 0;
        foreach (var c in s)
        {
            var charBytes = Encoding.Unicode.GetByteCount(new[] { c });
            if (total + charBytes > MaxFilenameBytes) break;
            sb.Append(c);
            total += charBytes;
        }
        return sb.ToString();
    }
}

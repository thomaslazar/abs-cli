using System.Text;

namespace AbsCli.Services;

public static class ChangelogReader
{
    private const string ResourceName = "CHANGELOG.md";

    public static string ReadAll()
    {
        return ReadEmbeddedResource();
    }

    public static string ReadLatest()
    {
        return ExtractLatest(ReadEmbeddedResource());
    }

    internal static string ExtractLatest(string changelog)
    {
        if (string.IsNullOrEmpty(changelog))
        {
            throw new InvalidOperationException("CHANGELOG.md has no version entries");
        }

        changelog = changelog.Replace("\r\n", "\n");
        var lines = changelog.Split('\n');
        var startIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            throw new InvalidOperationException("CHANGELOG.md has no version entries");
        }

        var endIndex = lines.Length;
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                endIndex = i;
                break;
            }
        }

        var lastNonBlank = endIndex - 1;
        while (lastNonBlank > startIndex && string.IsNullOrWhiteSpace(lines[lastNonBlank]))
        {
            lastNonBlank--;
        }

        var sb = new StringBuilder();
        for (var i = startIndex; i <= lastNonBlank; i++)
        {
            sb.Append(lines[i]);
            if (i < lastNonBlank)
            {
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string ReadEmbeddedResource()
    {
        var assembly = typeof(ChangelogReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException("CHANGELOG.md resource not embedded");
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

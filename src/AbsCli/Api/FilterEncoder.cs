namespace AbsCli.Api;

public static class FilterEncoder
{
    private static readonly HashSet<string> ValidGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "authors", "genres", "tags", "series", "narrators", "publishers",
        "publishedDecades", "missing", "languages", "progress", "tracks",
        "ebooks", "issues", "feed-open"
    };

    public static string Encode(string filter)
    {
        // Already encoded: "group.base64value"
        var dotIndex = filter.IndexOf('.');
        if (dotIndex > 0)
        {
            var maybeGroup = filter[..dotIndex];
            if (ValidGroups.Contains(maybeGroup) && !filter.Contains('='))
                return filter;
        }

        // User-friendly format: "group=value"
        var eqIndex = filter.IndexOf('=');
        if (eqIndex < 0)
            throw new ArgumentException($"Invalid filter format: '{filter}'. Expected 'group=value' (e.g. 'genres=Sci Fi').");

        var group = filter[..eqIndex];
        var value = filter[(eqIndex + 1)..];

        if (!ValidGroups.Contains(group))
            throw new ArgumentException($"Unknown filter group: '{group}'. Valid groups: {string.Join(", ", ValidGroups)}");

        var base64Value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        return $"{group}.{base64Value}";
    }
}

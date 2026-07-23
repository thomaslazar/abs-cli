using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Reads a JSON value that ABS declares as a string but may emit as a bare
/// number. ABS types some columns (e.g. <c>mediaProgress.ebookLocation</c>) as
/// <c>STRING</c>, but SQLite's loose type affinity lets legacy numeric values
/// leak through as JSON numbers, crashing a plain <c>string</c> deserialize
/// (issue #65). String tokens pass through unchanged; number tokens surface as
/// their raw text (e.g. <c>24</c> → <c>"24"</c>); null stays null. Writes
/// normally as a string.
/// </summary>
public class TolerantStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            // Raw numeric text (e.g. 24 -> "24"). ValueSpan is complete because
            // the CLI always deserializes from a fully-read response string.
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for string field")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

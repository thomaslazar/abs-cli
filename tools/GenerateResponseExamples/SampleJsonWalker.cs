using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Tools.GenerateResponseExamples;

/// <summary>
/// Per-property overrides the walker applies when rendering a type.
/// Keyed by (declaring type, C# property name). Overrides are consulted
/// when the walker writes a property; declared type determines the rendering
/// strategy (scalar JsonElement → embedded object; List&lt;JsonElement&gt; →
/// array whose element is the substitute).
/// </summary>
public class PropertyOverrides
{
    public Dictionary<(Type, string), string> Placeholders { get; } = new();
    public Dictionary<(Type, string), Type> TypeSubstitutions { get; } = new();
}

/// <summary>
/// Reflects over a type and emits a pretty-printed JSON sample payload whose
/// shape matches what <see cref="JsonSerializer"/> would produce, with synthetic
/// placeholder values. Used at build time to populate help output.
/// </summary>
public static class SampleJsonWalker
{
    // UnsafeRelaxedJsonEscaping keeps '<', '>' and '&' unescaped so placeholders
    // like "<string>" render literally in help output instead of \u003Cstring\u003E.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Render(Type type, PropertyOverrides? overrides = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteValue(writer, type, new HashSet<Type>(), overrides);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, Type type, HashSet<Type> visiting, PropertyOverrides? overrides = null)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            // Nullable<T> — render the T branch (sample is representative, not a null).
            WriteValue(writer, underlying, visiting, overrides);
            return;
        }

        if (type == typeof(string))
        {
            writer.WriteStringValue("<string>");
            return;
        }
        if (type == typeof(bool)) { writer.WriteBooleanValue(false); return; }
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
            type == typeof(byte) || type == typeof(sbyte))
        {
            writer.WriteNumberValue(0);
            return;
        }
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            writer.WriteNumberValue(0);
            return;
        }

        if (type == typeof(JsonElement) || type == typeof(object))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        // Guard: date/time types. STJ would serialise these as ISO-8601 strings,
        // not objects with Year/Month/etc. If a model adds one, the walker must
        // learn how to emit a representative ISO string — failing loudly is
        // better than silently shipping nonsense in help output.
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(DateOnly) ||
            type == typeof(TimeOnly) || type == typeof(Guid))
        {
            throw new NotSupportedException(
                $"SampleJsonWalker encountered unsupported type '{type}'. Extend the walker " +
                $"to emit the correct placeholder (usually an ISO-8601 string).");
        }

        if (type.IsArray)
        {
            writer.WriteStartArray();
            WriteValue(writer, type.GetElementType()!, visiting, overrides);
            writer.WriteEndArray();
            return;
        }

        if (TryGetDictionaryValue(type, out var valueType))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("<key>");
            WriteValue(writer, valueType, visiting, overrides);
            writer.WriteEndObject();
            return;
        }

        if (TryGetEnumerableElement(type, out var elementType))
        {
            writer.WriteStartArray();
            WriteValue(writer, elementType, visiting, overrides);
            writer.WriteEndArray();
            return;
        }

        if (!visiting.Add(type))
        {
            writer.WriteStringValue("<recursive>");
            return;
        }

        writer.WriteStartObject();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            var name = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            writer.WritePropertyName(name);

            var key = (type, prop.Name);
            if (overrides != null && overrides.Placeholders.TryGetValue(key, out var placeholder))
            {
                writer.WriteStringValue(placeholder);
                continue;
            }
            if (overrides != null && overrides.TypeSubstitutions.TryGetValue(key, out var substitute))
            {
                if (IsJsonElementList(prop.PropertyType))
                {
                    writer.WriteStartArray();
                    WriteValue(writer, substitute, visiting, overrides);
                    writer.WriteEndArray();
                }
                else
                {
                    WriteValue(writer, substitute, visiting, overrides);
                }
                continue;
            }

            if (IsNullableString(prop))
                writer.WriteNullValue();
            else
                WriteValue(writer, prop.PropertyType, visiting, overrides);
        }
        writer.WriteEndObject();
        visiting.Remove(type);
    }

    private static bool IsJsonElementList(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (!underlying.IsGenericType) return false;
        var def = underlying.GetGenericTypeDefinition();
        if (def != typeof(List<>) && def != typeof(IList<>) &&
            def != typeof(IEnumerable<>) && def != typeof(ICollection<>) &&
            def != typeof(IReadOnlyList<>) && def != typeof(IReadOnlyCollection<>)) return false;
        var arg = underlying.GetGenericArguments()[0];
        return arg == typeof(JsonElement) || arg == typeof(JsonElement?);
    }

    private static bool TryGetEnumerableElement(Type type, out Type elementType)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>) ||
                def == typeof(IEnumerable<>) || def == typeof(ICollection<>) ||
                def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }
        elementType = typeof(object);
        return false;
    }

    private static bool TryGetDictionaryValue(Type type, out Type valueType)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) ||
                def == typeof(IReadOnlyDictionary<,>))
            {
                valueType = type.GetGenericArguments()[1];
                return true;
            }
        }
        valueType = typeof(object);
        return false;
    }

    /// <summary>
    /// Returns true only for nullable <see cref="string"/> properties (string?).
    /// Other nullable reference types (e.g. Node?) are rendered via WriteValue so
    /// that recursive self-references produce the "&lt;recursive&gt;" sentinel.
    /// </summary>
    private static bool IsNullableString(PropertyInfo prop)
    {
        if (prop.PropertyType != typeof(string)) return false;
        var ctx = new NullabilityInfoContext();
        var info = ctx.Create(prop);
        return info.ReadState == NullabilityState.Nullable;
    }
}

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Tools.GenerateResponseExamples;

/// <summary>
/// Reflects over a type and emits a pretty-printed JSON sample payload whose
/// shape matches what <see cref="JsonSerializer"/> would produce, with synthetic
/// placeholder values. Used at build time to populate help output.
/// </summary>
public static class SampleJsonWalker
{
    public static string Render(Type type)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteValue(writer, type, new HashSet<Type>());
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, Type type, HashSet<Type> visiting)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            // Nullable<T> — render the T branch (sample is representative, not a null).
            WriteValue(writer, underlying, visiting);
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
            WriteValue(writer, type.GetElementType()!, visiting);
            writer.WriteEndArray();
            return;
        }

        if (TryGetDictionaryValue(type, out var valueType))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("<key>");
            WriteValue(writer, valueType, visiting);
            writer.WriteEndObject();
            return;
        }

        if (TryGetEnumerableElement(type, out var elementType))
        {
            writer.WriteStartArray();
            WriteValue(writer, elementType, visiting);
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

            if (IsNullableString(prop))
                writer.WriteNullValue();
            else
                WriteValue(writer, prop.PropertyType, visiting);
        }
        writer.WriteEndObject();
        visiting.Remove(type);
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

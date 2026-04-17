using System.Text.Json;
using System.Text.Json.Serialization;
using AbsCli.Tools.GenerateResponseExamples;

namespace AbsCli.Tests.Commands;

public class SampleJsonWalkerTests
{
    private static JsonElement Parse(string s) => JsonDocument.Parse(s).RootElement;

    private class Primitives
    {
        [JsonPropertyName("s")] public string S { get; set; } = "";
        [JsonPropertyName("sn")] public string? Sn { get; set; }
        [JsonPropertyName("i")] public int I { get; set; }
        [JsonPropertyName("l")] public long L { get; set; }
        [JsonPropertyName("d")] public double D { get; set; }
        [JsonPropertyName("b")] public bool B { get; set; }
    }

    [Fact]
    public void Primitives_RenderExpectedPlaceholders()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(Primitives)));
        Assert.Equal("<string>", json.GetProperty("s").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("sn").ValueKind);
        Assert.Equal(0, json.GetProperty("i").GetInt32());
        Assert.Equal(0, json.GetProperty("l").GetInt64());
        Assert.Equal(0d, json.GetProperty("d").GetDouble());
        Assert.False(json.GetProperty("b").GetBoolean());
    }

    [Fact]
    public void Placeholders_AreNotUnicodeEscapedInRawText()
    {
        // The generated file contains the raw output as a string literal.
        // If '<' and '>' come out as \u003C / \u003E, help output shows the
        // escape sequences instead of "<string>".
        var raw = SampleJsonWalker.Render(typeof(Primitives));
        Assert.Contains("\"<string>\"", raw);
        Assert.DoesNotContain("\\u003C", raw);
        Assert.DoesNotContain("\\u003E", raw);
    }

    private class WithList
    {
        [JsonPropertyName("items")] public List<Primitives> Items { get; set; } = new();
    }

    [Fact]
    public void List_RendersSingleElement()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(WithList)));
        var items = json.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("<string>", items[0].GetProperty("s").GetString());
    }

    private class WithDict
    {
        [JsonPropertyName("map")] public Dictionary<string, int> Map { get; set; } = new();
    }

    [Fact]
    public void Dictionary_RendersSingleKey()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(WithDict)));
        var map = json.GetProperty("map");
        Assert.Equal(JsonValueKind.Object, map.ValueKind);
        Assert.Equal(0, map.GetProperty("<key>").GetInt32());
    }

    private class WithNested
    {
        [JsonPropertyName("inner")] public Primitives Inner { get; set; } = new();
    }

    [Fact]
    public void NestedClass_RendersRecursively()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(WithNested)));
        Assert.Equal("<string>", json.GetProperty("inner").GetProperty("s").GetString());
    }

    private class WithRaw
    {
        [JsonPropertyName("raw")] public JsonElement Raw { get; set; }
        [JsonPropertyName("rawN")] public JsonElement? RawN { get; set; }
    }

    [Fact]
    public void JsonElement_RendersEmptyObject()
    {
        var rendered = SampleJsonWalker.Render(typeof(WithRaw));
        // Keeping a comment marker is fine, but the value must parse as JSON after
        // the comment is stripped. Simplest contract: value serialises as {}.
        var json = Parse(rendered);
        Assert.Equal(JsonValueKind.Object, json.GetProperty("raw").ValueKind);
        Assert.Empty(json.GetProperty("raw").EnumerateObject());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("rawN").ValueKind);
    }

    private class Node
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("child")] public Node? Child { get; set; }
    }

    [Fact]
    public void Recursive_EmitsRecursiveSentinelString()
    {
        var rendered = SampleJsonWalker.Render(typeof(Node));
        var json = Parse(rendered);
        Assert.Equal("<string>", json.GetProperty("name").GetString());
        Assert.Equal("<recursive>", json.GetProperty("child").GetString());
    }

    private class NoJsonPropertyName
    {
        public string CamelMe { get; set; } = "";
    }

    [Fact]
    public void WithoutAttribute_UsesRawPropertyName()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(NoJsonPropertyName)));
        // No [JsonPropertyName] → emit the raw C# property name verbatim.
        // Matches STJ's default serialisation when no naming policy is set.
        Assert.Equal("<string>", json.GetProperty("CamelMe").GetString());
    }

    private class WithJsonElementScalar
    {
        [JsonPropertyName("raw")] public JsonElement Raw { get; set; }
    }

    [Fact]
    public void PlaceholderOverride_EmitsStringLiteral()
    {
        var overrides = new PropertyOverrides();
        overrides.Placeholders[(typeof(WithJsonElementScalar), nameof(WithJsonElementScalar.Raw))] = "<custom>";
        var json = Parse(SampleJsonWalker.Render(typeof(WithJsonElementScalar), overrides));
        Assert.Equal("<custom>", json.GetProperty("raw").GetString());
    }

    [Fact]
    public void TypeSubstitution_ScalarJsonElement_RendersAsTargetType()
    {
        var overrides = new PropertyOverrides();
        overrides.TypeSubstitutions[(typeof(WithJsonElementScalar), nameof(WithJsonElementScalar.Raw))] = typeof(Primitives);
        var json = Parse(SampleJsonWalker.Render(typeof(WithJsonElementScalar), overrides));
        // Scalar JsonElement substituted → embedded as an object of target type.
        Assert.Equal("<string>", json.GetProperty("raw").GetProperty("s").GetString());
    }

    private class WithJsonElementList
    {
        [JsonPropertyName("items")] public List<JsonElement>? Items { get; set; }
    }

    [Fact]
    public void TypeSubstitution_JsonElementList_RendersAsArrayOfTargetType()
    {
        var overrides = new PropertyOverrides();
        overrides.TypeSubstitutions[(typeof(WithJsonElementList), nameof(WithJsonElementList.Items))] = typeof(Primitives);
        var json = Parse(SampleJsonWalker.Render(typeof(WithJsonElementList), overrides));
        var items = json.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("<string>", items[0].GetProperty("s").GetString());
    }
}

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
    public void WithoutAttribute_UsesCamelCasedPropertyName()
    {
        var json = Parse(SampleJsonWalker.Render(typeof(NoJsonPropertyName)));
        // AppJsonContext uses default STJ naming (PascalCase) only when no
        // naming policy is set. Our walker mirrors that: if no [JsonPropertyName],
        // emit the raw property name. This matches what STJ would serialise.
        Assert.Equal("<string>", json.GetProperty("CamelMe").GetString());
    }
}

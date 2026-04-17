# Help: response-shape examples and derived-resource notices — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enrich `abs-cli --help` with (a) a generated `Response shape:` JSON sample under every command that emits a typed JSON response and (b) a `Notes:` block on `authors` and `series` explaining those resources are lifecycle-driven by item metadata.

**Architecture:** A build-time codegen tool under `tools/GenerateResponseExamples/` reflects over `AbsCli.Models` types and emits `src/AbsCli/Commands/ResponseExamples.g.cs`, a static `ResponseExamples.For(Type)` lookup. The generated file is checked in; an MSBuild `BeforeBuild` target regenerates it when models change; a drift test fails CI if the checked-in file is stale. `HelpExtensions` gains top/bottom section positioning plus `AddResponseExample<T>()` / `AddResponseExample(Type envelope, Type element)` helpers. Commands call these alongside existing `AddExamples()`. `AuthorsCommand` and `SeriesCommand` gain top-positioned `Notes:` sections. This keeps the shipped AOT binary reflection-free while eliminating drift.

**Tech Stack:** C# / .NET 8 / System.CommandLine 2.0-beta4 / xUnit 2.5 / `System.Text.Json` source-generated context (`AppJsonContext`). Target CLI is published with `PublishAot=true`; codegen tool is non-AOT.

**Spec:** [docs/specs/2026-04-17-help-response-shapes-and-derived-notices.md](../specs/2026-04-17-help-response-shapes-and-derived-notices.md)

---

## File Structure

**New files:**

- `tools/GenerateResponseExamples/GenerateResponseExamples.csproj` — codegen tool project (net8.0, no AOT, references `AbsCli`)
- `tools/GenerateResponseExamples/Program.cs` — tool entry point, writes `ResponseExamples.g.cs`
- `tools/GenerateResponseExamples/SampleJsonWalker.cs` — the walker that turns a `Type` into a JSON string
- `src/AbsCli/Commands/ResponseExamples.g.cs` — generated, checked in, static lookup class
- `tests/AbsCli.Tests/Commands/SampleJsonWalkerTests.cs` — walker unit tests
- `tests/AbsCli.Tests/Commands/ResponseExamplesDriftTest.cs` — drift test
- `tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs` — sample-parse test
- `tests/AbsCli.Tests/Commands/HelpOutputTests.cs` — help-output integration tests
- `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs` — positioning unit tests

**Modified files:**

- `src/AbsCli/Commands/HelpExtensions.cs` — add `HelpSectionPosition`, `position` parameter, `AddResponseExample<T>`, `AddResponseExample(Type, Type)` helpers; update `WriteSections` placement
- `src/AbsCli/Program.cs` — change help rendering hook so top-positioned sections can run before default layout (possibly replace `HelpBuilder.Default.GetLayout()` with a prepended delegate)
- `src/AbsCli/Commands/AuthorsCommand.cs` — add `Notes:` top section + response examples on `list`/`get`
- `src/AbsCli/Commands/SeriesCommand.cs` — add `Notes:` top section + response examples on `list`/`get`
- `src/AbsCli/Commands/ItemsCommand.cs` — response examples on `list`, `get`, `search`, `update`, `batch-update`, `batch-get`, `scan`
- `src/AbsCli/Commands/LibrariesCommand.cs` — response examples on `list`, `get`
- `src/AbsCli/Commands/BackupCommand.cs` — response examples on `create`, `list`, `delete`, `upload`
- `src/AbsCli/Commands/TasksCommand.cs` — response example on `list`
- `src/AbsCli/Commands/MetadataCommand.cs` — response examples on `providers`, `covers`
- `src/AbsCli/Commands/SearchCommand.cs` — response example
- `src/AbsCli/AbsCli.csproj` — `BeforeBuild` target invoking the codegen tool with Inputs/Outputs
- `AbsCli.sln` — add `tools/GenerateResponseExamples` project under a new `tools` solution folder (or omit the tool from the sln entirely — decide in Task 3)

**Out of scope (WriteRawJson commands):** `metadata search`, `backup apply`. Their response shape is provider-dependent and not typed in our models. A follow-up plan can hand-author examples if needed.

---

## Task 1: Extend `HelpExtensions` with section positioning

TDD approach: tests drive the new enum and positioning behaviour.

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Create: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`

- [ ] **Step 1: Write the failing test for top vs bottom placement**

Create `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`:

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class HelpExtensionsTests
{
    private static string RenderHelp(Command command)
    {
        var console = new TestConsole();
        var root = new RootCommand { command };
        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .UseCustomHelpSections()
            .Build();
        parser.Invoke(new[] { command.Name, "--help" }, console);
        return console.Out.ToString() ?? "";
    }

    [Fact]
    public void TopSection_RendersBeforeOptions()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Notes", HelpSectionPosition.Top, "Top-placed content");

        var output = RenderHelp(cmd);

        var notesIdx = output.IndexOf("Notes:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(notesIdx >= 0, "Notes section missing");
        Assert.True(optionsIdx >= 0, "Options section missing");
        Assert.True(notesIdx < optionsIdx, "Notes should render before Options");
    }

    [Fact]
    public void BottomSection_RendersAfterOptions()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Examples", HelpSectionPosition.Bottom, "abs-cli demo");

        var output = RenderHelp(cmd);

        var examplesIdx = output.IndexOf("Examples:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(examplesIdx > optionsIdx, "Examples should render after Options");
    }

    [Fact]
    public void ExistingOverload_DefaultsToBottom()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Examples", "abs-cli demo");

        var output = RenderHelp(cmd);

        var examplesIdx = output.IndexOf("Examples:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(examplesIdx > optionsIdx, "Default overload must remain Bottom-placed");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpExtensionsTests"`
Expected: FAIL — `HelpSectionPosition` does not exist, `AddHelpSection` does not accept a position parameter.

- [ ] **Step 3: Implement positioning in `HelpExtensions.cs`**

Replace the body of `src/AbsCli/Commands/HelpExtensions.cs` with:

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;

namespace AbsCli.Commands;

public enum HelpSectionPosition { Top, Bottom }

/// <summary>
/// Adds custom sections (Notes, Examples, Filter groups, etc.) to help output.
/// Top-positioned sections render before the default layout; Bottom-positioned
/// sections render after Options in registration order.
/// </summary>
public static class HelpExtensions
{
    private record Section(string Title, string[] Lines, HelpSectionPosition Position);

    private static readonly Dictionary<Command, List<Section>> CommandSections = new();

    public static void AddHelpSection(this Command command, string title, params string[] lines)
        => command.AddHelpSection(title, HelpSectionPosition.Bottom, lines);

    public static void AddHelpSection(this Command command, string title, HelpSectionPosition position, params string[] lines)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
        {
            sections = new List<Section>();
            CommandSections[command] = sections;
        }
        sections.Add(new Section(title, lines, position));
    }

    public static void AddExamples(this Command command, params string[] examples)
        => command.AddHelpSection("Examples", HelpSectionPosition.Bottom, examples);

    public static int GetExampleCount(this Command command)
    {
        if (!CommandSections.TryGetValue(command, out var sections))
            return 0;
        return sections
            .Where(s => s.Title == "Examples")
            .SelectMany(s => s.Lines)
            .Count();
    }

    public static CommandLineBuilder UseCustomHelpSections(this CommandLineBuilder builder)
    {
        builder.UseHelp(ctx =>
        {
            ctx.HelpBuilder.CustomizeLayout(_ =>
            {
                var defaultLayout = HelpBuilder.Default.GetLayout().ToList();
                var withTop = new List<HelpSectionDelegate> { helpCtx => WriteSections(helpCtx, HelpSectionPosition.Top) };
                withTop.AddRange(defaultLayout);
                withTop.Add(helpCtx => WriteSections(helpCtx, HelpSectionPosition.Bottom));
                return withTop;
            });
        });
        return builder;
    }

    private static void WriteSections(HelpContext ctx, HelpSectionPosition position)
    {
        if (ctx.Command is not Command command) return;
        if (!CommandSections.TryGetValue(command, out var sections)) return;

        foreach (var section in sections.Where(s => s.Position == position))
        {
            ctx.Output.WriteLine($"{section.Title}:");
            foreach (var line in section.Lines)
                ctx.Output.WriteLine($"  {line}");
            ctx.Output.WriteLine();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpExtensionsTests"`
Expected: PASS (3 tests)

Also run the full test suite to confirm existing help tests still pass:
Run: `dotnet test tests/AbsCli.Tests`
Expected: PASS (no regressions)

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs
git commit -m "feat: add top/bottom positioning to help sections"
```

---

## Task 2: Implement `SampleJsonWalker` with TDD

The walker is pure C# logic (no codegen wiring yet), living in the codegen tool project. We develop it with unit tests first.

**Files:**
- Create: `tools/GenerateResponseExamples/GenerateResponseExamples.csproj`
- Create: `tools/GenerateResponseExamples/SampleJsonWalker.cs`
- Create: `tests/AbsCli.Tests/Commands/SampleJsonWalkerTests.cs`
- Modify: `tests/AbsCli.Tests/AbsCli.Tests.csproj` (add project reference to the tool)

- [ ] **Step 1: Create the tool project**

Create `tools/GenerateResponseExamples/GenerateResponseExamples.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AbsCli.Tools.GenerateResponseExamples</RootNamespace>
    <AssemblyName>GenerateResponseExamples</AssemblyName>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AbsCli\AbsCli.csproj" />
  </ItemGroup>
</Project>
```

Create a placeholder `tools/GenerateResponseExamples/Program.cs`:

```csharp
namespace AbsCli.Tools.GenerateResponseExamples;

internal static class Program
{
    public static int Main(string[] args) => 0;
}
```

Verify: `dotnet build tools/GenerateResponseExamples`
Expected: build succeeds (empty tool).

- [ ] **Step 2: Add project reference from the test project to the tool**

Modify `tests/AbsCli.Tests/AbsCli.Tests.csproj`, add inside the existing `<ItemGroup>` containing the `AbsCli` project reference:

```xml
<ProjectReference Include="..\..\tools\GenerateResponseExamples\GenerateResponseExamples.csproj" />
```

Verify: `dotnet build tests/AbsCli.Tests`
Expected: build succeeds.

- [ ] **Step 3: Write the failing walker tests**

Create `tests/AbsCli.Tests/Commands/SampleJsonWalkerTests.cs`:

```csharp
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
        Assert.Equal(0, json.GetProperty("raw").EnumerateObject().Count());
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
```

- [ ] **Step 4: Run tests, verify compilation failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~SampleJsonWalkerTests"`
Expected: FAIL — `SampleJsonWalker` type does not exist.

- [ ] **Step 5: Implement `SampleJsonWalker`**

Create `tools/GenerateResponseExamples/SampleJsonWalker.cs`:

```csharp
using System.Collections;
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

        if (TryGetEnumerableElement(type, out var elementType))
        {
            if (TryGetDictionaryValue(type, out var valueType))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("<key>");
                WriteValue(writer, valueType, visiting);
                writer.WriteEndObject();
                return;
            }
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

            if (IsNullableReference(prop))
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
            if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) ||
                def == typeof(IReadOnlyDictionary<,>))
            {
                elementType = type.GetGenericArguments()[1];
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

    private static bool IsNullableReference(PropertyInfo prop)
    {
        if (prop.PropertyType.IsValueType) return false;
        // Read NullableAttribute via NullabilityInfoContext (works on net8.0).
        var ctx = new NullabilityInfoContext();
        var info = ctx.Create(prop);
        return info.ReadState == NullabilityState.Nullable;
    }
}
```

- [ ] **Step 6: Run tests until they pass**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~SampleJsonWalkerTests"`
Expected: PASS (7 tests). If any fail, adjust the walker. In particular, verify:
- `NullabilityInfoContext` correctly reports `string?` as Nullable — if not, fall back to checking parameter nullability attribute or emit non-null for all reference types (less accurate but deterministic). Update test expectations accordingly and document.

- [ ] **Step 7: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/ tests/AbsCli.Tests/AbsCli.Tests.csproj tests/AbsCli.Tests/Commands/SampleJsonWalkerTests.cs
git commit -m "feat: add SampleJsonWalker for response-shape codegen"
```

---

## Task 3: Write the codegen tool entrypoint and generate `ResponseExamples.g.cs`

The tool enumerates `[JsonSerializable]` types on `AppJsonContext`, calls the walker for each, and writes a single generated C# file.

**Files:**
- Modify: `tools/GenerateResponseExamples/Program.cs`
- Create: `src/AbsCli/Commands/ResponseExamples.g.cs` (generated)
- Modify: `AbsCli.sln` — register the tool project under a `tools` solution folder

- [ ] **Step 1: Replace `Program.cs` with the generator entry point**

Overwrite `tools/GenerateResponseExamples/Program.cs`:

```csharp
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using AbsCli.Models;

namespace AbsCli.Tools.GenerateResponseExamples;

internal static class Program
{
    /// <summary>
    /// Usage: GenerateResponseExamples &lt;output path&gt;
    /// Emits a static class with a Type→JSON-sample dictionary for every type
    /// registered on AppJsonContext via [JsonSerializable], skipping types that
    /// are not meaningful response payloads (auth request bodies, Dictionary
    /// helper types, collection helper types).
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: GenerateResponseExamples <output path>");
            return 1;
        }
        var outputPath = args[0];
        var types = DiscoverResponseTypes();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by tools/GenerateResponseExamples.");
        sb.AppendLine("//   Do not edit by hand — run `dotnet run --project tools/GenerateResponseExamples -- <path>`.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace AbsCli.Commands;");
        sb.AppendLine();
        sb.AppendLine("internal static class ResponseExamples");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<Type, string> Samples = new()");
        sb.AppendLine("    {");
        foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var sample = SampleJsonWalker.Render(type);
            sb.Append("        { typeof(").Append(FullTypeName(type)).AppendLine("),");
            sb.Append("          ").Append(Quote(sample)).AppendLine(" },");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    public static string For(Type type)");
        sb.AppendLine("        => Samples.TryGetValue(type, out var sample) ? sample : throw new KeyNotFoundException($\"No response example registered for {type.FullName}\");");
        sb.AppendLine();
        sb.AppendLine("    public static IReadOnlyDictionary<Type, string> All => Samples;");
        sb.AppendLine("}");

        // Normalise line endings so checked-in output is stable cross-platform.
        var content = sb.ToString().Replace("\r\n", "\n");
        File.WriteAllText(outputPath, content);
        Console.WriteLine($"Wrote {types.Count} samples to {outputPath}");
        return 0;
    }

    private static List<Type> DiscoverResponseTypes()
    {
        // Reflect over AppJsonContext's [JsonSerializable] attributes — the
        // single source of truth for types that cross the CLI↔server boundary.
        // Exclude types that aren't response payloads:
        //  - LoginRequest (request body only)
        //  - AppConfig (local config, never a response)
        //  - raw Dictionary/List helper registrations (not command responses)
        var excluded = new HashSet<Type>
        {
            typeof(LoginRequest),
            typeof(Configuration.AppConfig),
        };

        var attrs = typeof(AppJsonContext)
            .GetCustomAttributes<JsonSerializableAttribute>(inherit: false);
        return attrs
            .Select(a => a.Type)
            .Where(t => !excluded.Contains(t))
            .Where(t => !IsCollectionHelperType(t))
            .ToList();
    }

    private static bool IsCollectionHelperType(Type t)
    {
        // Skip registrations like Dictionary<string,string> or List<UploadManifestEntry>
        // — those exist to let STJ serialise helper shapes, not response payloads.
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Dictionary<,>) || def == typeof(List<>);
    }

    private static string FullTypeName(Type t)
    {
        // Emit without global:: prefix, with nested types using '.' syntax.
        var ns = t.Namespace ?? "";
        var name = t.Name;
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string Quote(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
}
```

- [ ] **Step 2: Run the generator manually for the first time**

```bash
dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs
```

Expected: prints `Wrote N samples to src/AbsCli/Commands/ResponseExamples.g.cs` where N is roughly 20.

- [ ] **Step 3: Inspect the generated file**

Open `src/AbsCli/Commands/ResponseExamples.g.cs`. Verify:
- Contains entries for `AuthorItem`, `AuthorListResponse`, `SeriesItem`, `LibraryItemMinified`, `PaginatedResponse`, `SearchResult`, `LibraryListResponse`, `Library`, `UpdateMediaResponse`, `BatchUpdateResponse`, `BatchGetResponse`, `BackupItem`, `BackupListResponse`, `ScanResult`, `TaskItem`, `TaskListResponse`, `MetadataProvidersResponse`, `CoverSearchResponse`, `LoginResponse`.
- `LoginRequest` and `AppConfig` are NOT present.
- No `Dictionary<string,string>` or `List<UploadManifestEntry>` entries.
- Each sample is valid JSON when the string literal is interpreted (single-line escape `\n` format).

- [ ] **Step 4: Verify `AbsCli` still builds with the new file**

Run: `dotnet build src/AbsCli`
Expected: build succeeds. `ResponseExamples.g.cs` compiles cleanly.

- [ ] **Step 5: Register the tool in the solution file**

Manually edit `AbsCli.sln` — add a new solution folder `tools` and nest the tool project under it, mirroring the existing `src` / `tests` folders. If editing raw sln is fragile, skip and add via:

```bash
dotnet sln AbsCli.sln add --solution-folder tools tools/GenerateResponseExamples/GenerateResponseExamples.csproj
```

Verify: `dotnet sln AbsCli.sln list` shows the tool project.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS (walker tests + existing suite + help extensions tests).

- [ ] **Step 7: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/Program.cs src/AbsCli/Commands/ResponseExamples.g.cs AbsCli.sln
git commit -m "feat: add codegen tool emitting ResponseExamples.g.cs"
```

---

## Task 4: Wire `BeforeBuild` MSBuild target and add drift test

**Files:**
- Modify: `src/AbsCli/AbsCli.csproj`
- Create: `tests/AbsCli.Tests/Commands/ResponseExamplesDriftTest.cs`
- Create: `tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs`

- [ ] **Step 1: Add `BeforeBuild` target to `AbsCli.csproj`**

Insert this `<Target>` element near the end of `src/AbsCli/AbsCli.csproj`, just before the closing `</Project>` tag:

```xml
<Target Name="RegenerateResponseExamples"
        BeforeTargets="CoreCompile"
        Inputs="@(Compile);$(MSBuildThisFileFullPath);$(MSBuildProjectDirectory)\..\..\tools\GenerateResponseExamples\**\*.cs"
        Outputs="$(MSBuildProjectDirectory)\Commands\ResponseExamples.g.cs">
  <Exec Command="dotnet run --project &quot;$(MSBuildProjectDirectory)\..\..\tools\GenerateResponseExamples\GenerateResponseExamples.csproj&quot; -- &quot;$(MSBuildProjectDirectory)\Commands\ResponseExamples.g.cs&quot;"
        ConsoleToMSBuild="true" />
</Target>
```

- [ ] **Step 2: Verify incremental build behaviour**

```bash
touch src/AbsCli/Commands/ResponseExamples.g.cs  # make output newer than inputs
dotnet build src/AbsCli  # should skip codegen
# Then:
touch src/AbsCli/Models/Author.cs
dotnet build src/AbsCli  # should re-run codegen
```

Expected: first build shows no "Wrote N samples" line; second build shows the line. If the target always runs, refine `Inputs` so only `.cs` files under `Models/` are inputs.

- [ ] **Step 3: Write the drift test**

Create `tests/AbsCli.Tests/Commands/ResponseExamplesDriftTest.cs`:

```csharp
using System.IO;
using System.Text;
using AbsCli.Tools.GenerateResponseExamples;

namespace AbsCli.Tests.Commands;

public class ResponseExamplesDriftTest
{
    [Fact]
    public void CheckedInFile_MatchesFreshGeneration()
    {
        // Locate the repo root from the test binary's runtime directory.
        var repoRoot = RepoRoot();
        var checkedInPath = Path.Combine(repoRoot, "src", "AbsCli", "Commands", "ResponseExamples.g.cs");
        Assert.True(File.Exists(checkedInPath), $"Missing generated file: {checkedInPath}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"response-examples-{Guid.NewGuid():N}.g.cs");
        try
        {
            var exitCode = Program.Main(new[] { tempPath });
            Assert.Equal(0, exitCode);

            var expected = File.ReadAllText(checkedInPath).Replace("\r\n", "\n");
            var actual = File.ReadAllText(tempPath).Replace("\r\n", "\n");
            Assert.True(
                expected == actual,
                "ResponseExamples.g.cs is stale. Regenerate with: " +
                "dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AbsCli.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
```

- [ ] **Step 4: Make `Program.Main` callable from tests**

Tests call `Program.Main(args)` — the `Program` class is already `internal`, but the test project already references the tool via project reference, so it has visibility. Verify by running:

```bash
dotnet build tests/AbsCli.Tests
```

Expected: build succeeds. If it fails because `Program` is not accessible, add `<InternalsVisibleTo Include="AbsCli.Tests" />` inside an `<ItemGroup>` in the tool csproj.

- [ ] **Step 5: Write the sample-JSON-valid test**

Create `tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs`:

```csharp
using System.Text.Json;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class ResponseExamplesJsonValidTest
{
    [Fact]
    public void EveryRegisteredSample_ParsesAsJson()
    {
        Assert.NotEmpty(ResponseExamples.All);
        foreach (var (type, json) in ResponseExamples.All)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                Assert.Fail($"Sample for {type.FullName} is not valid JSON: {ex.Message}\n{json}");
            }
        }
    }
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/AbsCli.Tests`
Expected: PASS — drift test confirms the checked-in file matches fresh generation; JSON-valid test confirms every sample parses.

- [ ] **Step 7: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/AbsCli.csproj tests/AbsCli.Tests/Commands/ResponseExamplesDriftTest.cs tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs
git commit -m "feat: regenerate ResponseExamples.g.cs on build, add drift tests"
```

---

## Task 5: Add `AddResponseExample` helpers to `HelpExtensions`

Two overloads: `AddResponseExample<T>()` for a simple typed response, and `AddResponseExample(Type envelope, Type element)` for paginated responses where `PaginatedResponse.Results` holds an untyped `List<JsonElement>` but the command knows the real element type.

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Modify: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`

- [ ] **Step 1: Write failing tests for both overloads**

Append to `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`:

```csharp
    [Fact]
    public void AddResponseExample_Generic_RendersResponseShapeSection()
    {
        var cmd = new Command("demo", "Demo");
        cmd.AddResponseExample<AbsCli.Models.AuthorItem>();

        var output = RenderHelp(cmd);
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"numBooks\"", output);
    }

    [Fact]
    public void AddResponseExample_EnvelopeAndElement_SubstitutesResultsArray()
    {
        var cmd = new Command("demo", "Demo");
        cmd.AddResponseExample(
            typeof(AbsCli.Models.PaginatedResponse),
            typeof(AbsCli.Models.LibraryItemMinified));

        var output = RenderHelp(cmd);
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        // The element type's fields should appear inside results.
        Assert.Contains("\"mediaType\"", output);
        // The untyped JsonElement placeholder from PaginatedResponse.Results
        // should NOT leak through.
        Assert.DoesNotContain("\"results\": [\n      {}\n    ]", output);
    }
```

- [ ] **Step 2: Run tests, verify failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpExtensionsTests"`
Expected: FAIL — `AddResponseExample` methods do not exist.

- [ ] **Step 3: Implement the helpers**

Append inside the `HelpExtensions` class in `src/AbsCli/Commands/HelpExtensions.cs`:

```csharp
    /// <summary>
    /// Register a generated response-shape sample for the given type as a
    /// "Response shape:" help section. The sample is produced at build time
    /// from the C# model class; see tools/GenerateResponseExamples.
    /// </summary>
    public static void AddResponseExample<T>(this Command command)
        => command.AddResponseExampleInternal(ResponseExamples.For(typeof(T)));

    /// <summary>
    /// Register a response-shape sample for a paginated envelope whose
    /// <c>results</c> array is typed as <c>List&lt;JsonElement&gt;</c>. The
    /// element sample is spliced into the envelope's results array.
    /// </summary>
    public static void AddResponseExample(this Command command, Type envelopeType, Type elementType)
    {
        var envelopeJson = ResponseExamples.For(envelopeType);
        var elementJson = ResponseExamples.For(elementType);
        // Splice: find the results array in the envelope ({}) and replace it
        // with [<element>]. The envelope sample is produced with List<JsonElement>
        // which renders as an empty object placeholder — see SampleJsonWalker.
        var spliced = SpliceResultsArray(envelopeJson, elementJson);
        command.AddResponseExampleInternal(spliced);
    }

    private static void AddResponseExampleInternal(this Command command, string json)
    {
        var lines = json.Split('\n');
        command.AddHelpSection("Response shape", HelpSectionPosition.Bottom, lines);
    }

    private static string SpliceResultsArray(string envelopeJson, string elementJson)
    {
        // Look for a line like `  "results": [` (whitespace, quoted key, bracket).
        // Replace up to the matching `]` with [\n<indented element>\n]. Done
        // textually to avoid a JSON round-trip that would lose indentation.
        var lines = envelopeJson.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("\"results\":", StringComparison.Ordinal)) continue;

            var leadingSpaces = lines[i].Length - trimmed.Length;
            var indent = new string(' ', leadingSpaces);

            // Find matching closing bracket (assumes envelope uses square brackets
            // only for results — true for PaginatedResponse today).
            int closeIdx = i;
            while (closeIdx < lines.Length &&
                   !lines[closeIdx].TrimStart().StartsWith("]", StringComparison.Ordinal))
                closeIdx++;
            if (closeIdx == lines.Length) break;

            var elementIndented = string.Join("\n",
                elementJson.Split('\n').Select(l => indent + "  " + l));
            var trailing = lines[closeIdx].TrimStart().StartsWith("],", StringComparison.Ordinal) ? "]," : "]";
            var replacement = new[]
            {
                $"{indent}\"results\": [",
                elementIndented,
                $"{indent}{trailing}"
            };

            var output = new List<string>();
            output.AddRange(lines.Take(i));
            output.AddRange(replacement);
            output.AddRange(lines.Skip(closeIdx + 1));
            return string.Join('\n', output);
        }
        return envelopeJson;
    }
```

- [ ] **Step 4: Run tests to verify passing**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpExtensionsTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs
git commit -m "feat: add AddResponseExample helpers to HelpExtensions"
```

---

## Task 6: Register response examples and notes on `AuthorsCommand` and `SeriesCommand`

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Modify: `src/AbsCli/Commands/SeriesCommand.cs`
- Create: `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`

- [ ] **Step 1: Write failing integration tests**

Create `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`:

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class HelpOutputTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.AddCommand(AuthorsCommand.Create());
        root.AddCommand(SeriesCommand.Create());
        root.AddCommand(ItemsCommand.Create());
        root.AddCommand(LibrariesCommand.Create());
        root.AddCommand(BackupCommand.Create());
        root.AddCommand(TasksCommand.Create());
        root.AddCommand(MetadataCommand.Create());
        root.AddCommand(SearchCommand.Create());

        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .UseCustomHelpSections()
            .Build();

        var console = new TestConsole();
        var args = path.Concat(new[] { "--help" }).ToArray();
        parser.Invoke(args, console);
        return console.Out.ToString() ?? "";
    }

    [Fact]
    public void AuthorsCommand_TopLevelHelp_ShowsNotes()
    {
        var output = RenderHelp("authors");
        Assert.Contains("Notes:", output);
        Assert.Contains("derived from book metadata", output);
        // Appears ABOVE Options: so agents see it on first scan.
        Assert.True(output.IndexOf("Notes:") < output.IndexOf("Commands:"));
    }

    [Fact]
    public void SeriesCommand_TopLevelHelp_ShowsNotes()
    {
        var output = RenderHelp("series");
        Assert.Contains("Notes:", output);
        Assert.Contains("derived from book metadata", output);
    }

    [Fact]
    public void AuthorsList_Help_ShowsResponseShapeWithNumBooks()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"numBooks\"", output);
        Assert.Contains("\"authors\"", output);
    }

    [Fact]
    public void SeriesList_Help_ShowsResponseShapeWithResults()
    {
        var output = RenderHelp("series", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"name\"", output);
    }
}
```

- [ ] **Step 2: Run tests, verify failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpOutputTests"`
Expected: FAIL — Notes section and response examples not registered yet.

- [ ] **Step 3: Modify `AuthorsCommand.cs`**

Replace the body of `CreateListCommand` and `CreateGetCommand` in `src/AbsCli/Commands/AuthorsCommand.cs` — add `AddResponseExample` calls. Also modify `Create()` to register the top-level `Notes:` section:

```csharp
public static Command Create()
{
    var command = new Command("authors", "Manage authors");
    command.AddHelpSection("Notes", HelpSectionPosition.Top,
        "Authors are derived from book metadata. An author record exists while at",
        "least one library item references it. When the last referencing item is",
        "removed or re-tagged, the scanner deletes the author on its next run",
        "(unless a custom image is set). To remove an author, update the books",
        "that reference it.");
    command.AddCommand(CreateListCommand());
    command.AddCommand(CreateGetCommand());
    return command;
}
```

In `CreateListCommand`, after the existing `AddExamples(...)` call:

```csharp
command.AddResponseExample<AuthorListResponse>();
```

In `CreateGetCommand`, after `AddExamples(...)`:

```csharp
command.AddResponseExample<AuthorItem>();
```

- [ ] **Step 4: Modify `SeriesCommand.cs`**

In `src/AbsCli/Commands/SeriesCommand.cs`, update `Create()`:

```csharp
public static Command Create()
{
    var command = new Command("series", "Manage series");
    command.AddHelpSection("Notes", HelpSectionPosition.Top,
        "Series are derived from book metadata. A series exists while at least one",
        "library item references it. When the last referencing item is removed or",
        "re-tagged, the scanner deletes the series on its next run. To remove a",
        "series, update the books that reference it.");
    command.AddCommand(CreateListCommand());
    command.AddCommand(CreateGetCommand());
    return command;
}
```

In `CreateListCommand`, after `AddExamples(...)`:

```csharp
command.AddResponseExample(typeof(PaginatedResponse), typeof(SeriesItem));
```

In `CreateGetCommand`, after `AddExamples(...)`:

```csharp
command.AddResponseExample<SeriesItem>();
```

- [ ] **Step 5: Run tests to verify passing**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpOutputTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/AuthorsCommand.cs src/AbsCli/Commands/SeriesCommand.cs tests/AbsCli.Tests/Commands/HelpOutputTests.cs
git commit -m "feat: add notes and response-shape examples to authors and series"
```

---

## Task 7: Register response examples on `ItemsCommand`

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs`
- Modify: `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`

- [ ] **Step 1: Extend `HelpOutputTests` with items assertions**

Append to `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`:

```csharp
    [Fact]
    public void ItemsList_Help_ShowsPaginatedResponseWithLibraryItemFields()
    {
        var output = RenderHelp("items", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"mediaType\"", output);
        Assert.Contains("\"libraryId\"", output);
    }

    [Fact]
    public void ItemsGet_Help_ShowsLibraryItemMinified()
    {
        var output = RenderHelp("items", "get");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"mediaType\"", output);
        Assert.Contains("\"birthtimeMs\"", output);
    }

    [Fact]
    public void ItemsSearch_Help_ShowsSearchResult()
    {
        var output = RenderHelp("items", "search");
        Assert.Contains("Response shape:", output);
    }

    [Fact]
    public void ItemsUpdate_Help_ShowsUpdateMediaResponse()
    {
        var output = RenderHelp("items", "update");
        Assert.Contains("Response shape:", output);
    }

    [Fact]
    public void ItemsBatchUpdate_Help_ShowsBatchUpdateResponse()
    {
        var output = RenderHelp("items", "batch-update");
        Assert.Contains("Response shape:", output);
    }

    [Fact]
    public void ItemsBatchGet_Help_ShowsBatchGetResponse()
    {
        var output = RenderHelp("items", "batch-get");
        Assert.Contains("Response shape:", output);
    }

    [Fact]
    public void ItemsScan_Help_ShowsScanResult()
    {
        var output = RenderHelp("items", "scan");
        Assert.Contains("Response shape:", output);
    }
```

- [ ] **Step 2: Run tests, verify failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpOutputTests"`
Expected: 7 new tests FAIL.

- [ ] **Step 3: Register examples in `ItemsCommand.cs`**

Add the following after each subcommand's existing `AddExamples(...)` call:

- `CreateListCommand` → `command.AddResponseExample(typeof(PaginatedResponse), typeof(LibraryItemMinified));`
- `CreateGetCommand` → `command.AddResponseExample<LibraryItemMinified>();`
- `CreateSearchCommand` → `command.AddResponseExample<SearchResult>();`
- `CreateUpdateCommand` → `command.AddResponseExample<UpdateMediaResponse>();`
- `CreateBatchUpdateCommand` → `command.AddResponseExample<BatchUpdateResponse>();`
- `CreateBatchGetCommand` → `command.AddResponseExample<BatchGetResponse>();`
- `CreateScanCommand` → `command.AddResponseExample<ScanResult>();`

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpOutputTests"`
Expected: all PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs tests/AbsCli.Tests/Commands/HelpOutputTests.cs
git commit -m "feat: add response-shape examples to items commands"
```

---

## Task 8: Register response examples on remaining commands

**Files:**
- Modify: `src/AbsCli/Commands/LibrariesCommand.cs`
- Modify: `src/AbsCli/Commands/BackupCommand.cs`
- Modify: `src/AbsCli/Commands/TasksCommand.cs`
- Modify: `src/AbsCli/Commands/MetadataCommand.cs`
- Modify: `src/AbsCli/Commands/SearchCommand.cs`
- Modify: `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`

- [ ] **Step 1: Extend tests**

Append to `HelpOutputTests.cs`:

```csharp
    [Theory]
    [InlineData(new[] { "libraries", "list" })]
    [InlineData(new[] { "libraries", "get" })]
    [InlineData(new[] { "backup", "create" })]
    [InlineData(new[] { "backup", "list" })]
    [InlineData(new[] { "backup", "delete" })]
    [InlineData(new[] { "backup", "upload" })]
    [InlineData(new[] { "tasks", "list" })]
    [InlineData(new[] { "metadata", "providers" })]
    [InlineData(new[] { "metadata", "covers" })]
    [InlineData(new[] { "search" })]
    public void Command_Help_IncludesResponseShape(string[] path)
    {
        var output = RenderHelp(path);
        Assert.Contains("Response shape:", output);
    }

    [Theory]
    [InlineData(new[] { "metadata", "search" })]
    [InlineData(new[] { "backup", "apply" })]
    [InlineData(new[] { "backup", "download" })]
    [InlineData(new[] { "libraries", "scan" })]
    public void WriteRawJsonCommands_DoNotHaveResponseShape(string[] path)
    {
        // These commands either emit provider-dependent JSON or no JSON at all —
        // they're explicitly out of scope per the spec's non-goals.
        var output = RenderHelp(path);
        Assert.DoesNotContain("Response shape:", output);
    }
```

- [ ] **Step 2: Run, verify failure**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpOutputTests"`
Expected: 10 Theory cases FAIL on the Response-shape assertion.

- [ ] **Step 3: Add the examples**

In each file, append after `AddExamples(...)`:

- `LibrariesCommand.CreateListCommand` → `command.AddResponseExample<LibraryListResponse>();`
- `LibrariesCommand.CreateGetCommand` → `command.AddResponseExample<Library>();`
- `BackupCommand.CreateCreateCommand` → `command.AddResponseExample<BackupListResponse>();`
- `BackupCommand.CreateListCommand` → `command.AddResponseExample<BackupListResponse>();`
- `BackupCommand.CreateDeleteCommand` → `command.AddResponseExample<BackupListResponse>();`
- `BackupCommand.CreateUploadCommand` → `command.AddResponseExample<BackupListResponse>();`
- `TasksCommand.CreateListCommand` → `command.AddResponseExample<TaskListResponse>();`
- `MetadataCommand.CreateProvidersCommand` → `command.AddResponseExample<MetadataProvidersResponse>();`
- `MetadataCommand.CreateCoversCommand` → `command.AddResponseExample<CoverSearchResponse>();`
- `SearchCommand.Create` (no subcommands) → `command.AddResponseExample<SearchResult>();`

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AbsCli.Tests`
Expected: all PASS (including drift, JSON-valid, help-positioning, help-output theories).

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/LibrariesCommand.cs src/AbsCli/Commands/BackupCommand.cs src/AbsCli/Commands/TasksCommand.cs src/AbsCli/Commands/MetadataCommand.cs src/AbsCli/Commands/SearchCommand.cs tests/AbsCli.Tests/Commands/HelpOutputTests.cs
git commit -m "feat: add response-shape examples to libraries/backup/tasks/metadata/search"
```

---

## Task 9: Verify AOT publish and manual end-to-end check

**Files:**
- None modified — verification only

- [ ] **Step 1: AOT publish**

```bash
dotnet publish src/AbsCli -c Release -r linux-x64 -o /tmp/abs-cli-aot-check
```

Expected: publish succeeds with no new AOT warnings. `ResponseExamples.g.cs` is plain `Dictionary<Type, string>` constants — no reflection — so AOT should be clean.

If new warnings appear, verify they're already present on `main` (regression check):

```bash
git stash && dotnet publish src/AbsCli -c Release -r linux-x64 -o /tmp/abs-cli-aot-baseline 2>&1 | grep -i "warning IL"
git stash pop
```

- [ ] **Step 2: Run the binary and spot-check help output**

```bash
/tmp/abs-cli-aot-check/abs-cli authors --help
```

Expected: `Notes:` section visible above `Options:`/`Commands:` with the full text.

```bash
/tmp/abs-cli-aot-check/abs-cli series --help
```

Expected: `Notes:` section for series.

```bash
/tmp/abs-cli-aot-check/abs-cli items get --help
```

Expected: `Response shape:` section at the bottom showing `LibraryItemMinified` fields (`id`, `libraryId`, `mediaType`, `birthtimeMs`, etc.) with `"<string>"` / `0` / `null` placeholders.

```bash
/tmp/abs-cli-aot-check/abs-cli items list --help
```

Expected: `Response shape:` with `{ "results": [ { "id": "<string>", ... } ], "total": 0, ... }`.

- [ ] **Step 3: Agent smoke test**

Save `abs-cli items get --help` output to a file, then (in a separate ad-hoc session) ask an agent to write a `jq` query that extracts the title and author name from the response. Verify the suggested query uses `.media.metadata.title` / `.media.metadata.authorName` patterns that match actual responses. Document the result in the PR description (pass/fail). Note: `.media` is typed as `JsonElement?` in `LibraryItemMinified`, so the generated sample will show `"media": {}` — the agent will still need access to ABS's `media.metadata` shape from elsewhere. If this becomes a real pain point, raise a follow-up task to hand-author `media` content into a post-generation override dictionary.

- [ ] **Step 4: Commit any documentation updates**

If Step 3 reveals the need for a CLAUDE.md line like "response-shape examples live in help output — run `abs-cli <cmd> --help` to see fields," add it:

```bash
git add CLAUDE.md
git commit -m "docs: note response-shape examples in command help output"
```

Otherwise skip the commit.

---

## Self-review checklist (run after implementation, before handing off)

- [ ] All tasks' commit messages follow Conventional Commits (`feat:`, `fix:`, etc.) per `CLAUDE.md`.
- [ ] No `Co-Authored-By` lines in any commit.
- [ ] `dotnet format AbsCli.sln` has been run and produced no changes on a clean run.
- [ ] `dotnet test` passes (including drift, JSON-valid, help positioning, help output).
- [ ] `dotnet publish -c Release -r linux-x64` produces no new AOT warnings vs `main`.
- [ ] `abs-cli authors --help` shows the `Notes:` block above `Commands:`.
- [ ] `abs-cli items get --help` shows a `Response shape:` block with at least 15 fields visible.
- [ ] `docs/specs/2026-04-17-help-response-shapes-and-derived-notices.md` is unchanged from the approved spec.

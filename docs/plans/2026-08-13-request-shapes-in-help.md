# Request shapes in help — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every command that takes a JSON body documents that body's shape under `--help-full`, generated from a C# type rather than hand-written prose.

**Architecture:** A new `AddRequestExample<T>()` registers a `Request shape` section tagged `IsShape: true`, so it renders under `--help-full` beside the existing response shapes. Samples come from the existing `tools/GenerateResponseExamples` generator, which stops excluding request types. Commands parse `--input`/`--stdin` into the type as a validation gate and then send the **original bytes** — never a re-serialisation. The 32 shape-carrying option descriptions collapse to one uniform pair.

**Tech Stack:** C# / .NET 10, System.CommandLine 2.0.7, System.Text.Json source-generated contexts (Native AOT), xUnit.

**Spec:** `docs/specs/2026-08-13-request-shapes-in-help-design.md`

---

## Baseline & conventions

- `dotnet format AbsCli.sln` after every C# edit (CI enforces `--verify-no-changes`).
- Conventional Commits, imperative, lowercase, no trailing period. **No `Co-Authored-By` and no AI attribution.** Do NOT touch `CHANGELOG.md`.
- Tests: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj`. Baseline **429 passing**.
- AOT check: `dotnet run --project src/AbsCli -- self-test`. Baseline **72 passing**.
- **Any new test that makes production code log must be in `[Collection("NLog")]`** — NLog config is process-global and a stray line fails a log-asserting test on count. This broke a release CI run; see `tests/AbsCli.Tests/NLogCollection.cs`.
- `src/AbsCli/Commands/ResponseExamples.g.cs` is **tracked and generated**. It is rewritten on every build; commit it whenever a task changes the types it covers, in that task's commit.
- No new verb or flag → the README Commands table does not change.

## Call-order rule (found during Task 2)

`WriteSections` renders sections in **registration order** — there is no
request/response ordering logic in the engine. So in every command, call
`AddRequestExample<T>()` **before** `AddResponseExample<T>()`, which is the order an
agent needs them: construct the body, then read what comes back. Tasks 3-8 must
follow this at each call site.

## The three command classes (from the spec)

- **Class A — pass-through**: body reaches ABS verbatim. Type documents the ABS wire shape; parse then forward the original string.
- **Class B — CLI-transformed**: `playlists reorder`, `batch-add`, `batch-remove`. The CLI accepts `{books:["li_a",…]}` and `PlaylistsService.SerializeItems` emits ABS's `{items:[{libraryItemId}]}`. The type documents the **CLI's** contract. **Do not "fix" these toward ABS's shape** — the current help text is correct and an agent following an `items` shape here would get a 400.
- **Class C — free-form**: `items update`. ABS validates nothing (`const mediaPayload = req.body`), so the parse is a syntax check only and no field is required.

## File structure

| File | Responsibility | Action |
|---|---|---|
| `tools/GenerateResponseExamples/Program.cs` | drop request-type exclusions; render nullable refs as placeholders; rename class | Modify |
| `src/AbsCli/Commands/HelpExtensions.cs` | `AddRequestExample<T>()`, hint wording, `JsonExamples` rename | Modify |
| `src/AbsCli/Models/RequestShapes.cs` | new request types (one file, they are small) | Create |
| `src/AbsCli/Models/JsonContext.cs` | register the new types | Modify |
| `src/AbsCli/Commands/ItemsCommand.cs` | 7 shape blocks; chapters off canonicalisation; description cleanup | Modify |
| `src/AbsCli/Commands/CollectionsCommand.cs` | 4 shape blocks + cleanup | Modify |
| `src/AbsCli/Commands/PlaylistsCommand.cs` | 3 shape blocks (class B) + cleanup | Modify |
| `src/AbsCli/Commands/LibrariesCommand.cs` | 1 shape block + cleanup | Modify |
| `src/AbsCli/Commands/SelfTestCommand.cs` | round-trip each new type | Modify |
| `docs/input-output.md`, `docs/abs-compatibility.md` | docs + the ABS-bump checklist | Modify |

---

## Task 1: Generator — placeholders for nullable refs, and the rename

**Files:**
- Modify: `tools/GenerateResponseExamples/Program.cs`
- Modify: `src/AbsCli/Commands/HelpExtensions.cs` (two `ResponseExamples.` call sites)

- [ ] **Step 1: Make nullable reference properties render as placeholders**

Read `SampleJsonWalker.Render` in `tools/GenerateResponseExamples/Program.cs`. Today a nullable `string?` renders as `null` (see `ServerStatus` → `{"serverVersion": null}`), which tells a reader the key but not the type. Change the walker so a nullable reference type renders the same placeholder as its non-nullable form — `"<string>"` for strings, and the element placeholder for nullable collections.

Do not change how non-nullable value types render (`0`, `false`) or how the `LibraryItemMinified.media` override behaves.

- [ ] **Step 2: Regenerate and eyeball the diff**

```bash
dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs
git diff --stat src/AbsCli/Commands/ResponseExamples.g.cs
```
Expected: many `null` → `"<string>"` changes, no structural changes. Confirm `ServerStatus` now reads `{"serverVersion": "<string>"}`.

- [ ] **Step 3: Rename the generated class**

In the generator, change the emitted class name `ResponseExamples` → `JsonExamples` (the two `sb.AppendLine` lines that write `internal static class ResponseExamples` and the file header comment). The output file name stays `ResponseExamples.g.cs` for now to keep the diff small; note that in the header comment.

Update the two call sites in `src/AbsCli/Commands/HelpExtensions.cs` (`ResponseExamples.For(...)`, and any `ResponseExamples.All`).

- [ ] **Step 4: Verify**

```bash
dotnet build AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet run --project src/AbsCli -- self-test 2>&1 | tail -3
```
Expected: build clean; **429 passing**; self-test **72 passing**. If a test asserts on a literal `null` in a sample, update the assertion — the new placeholder is the intended output.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/Program.cs src/AbsCli/Commands/HelpExtensions.cs src/AbsCli/Commands/ResponseExamples.g.cs tests/
git commit -m "refactor: render nullable fields as typed placeholders in json samples"
```

---

## Task 2: `AddRequestExample<T>()` and the help hint

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Test: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `HelpExtensionsTests.cs`. Match the file's existing render-helper pattern (it already has helpers for `--help` and `--help-full`; reuse them rather than inventing new ones):

```csharp
    [Fact]
    public void RequestShape_AppearsUnderHelpFull()
    {
        var command = new Command("demo", "Demo");
        command.AddRequestExample<ChaptersSetRequest>();
        var output = RenderHelpFull(command);
        Assert.Contains("Request shape", output);
        Assert.Contains("chapters", output);
    }

    [Fact]
    public void RequestShape_HiddenFromPlainHelp()
    {
        var command = new Command("demo", "Demo");
        command.AddRequestExample<ChaptersSetRequest>();
        var output = RenderHelp(command);
        Assert.DoesNotContain("Request shape", output);
    }

    [Fact]
    public void PlainHelp_HintMentionsRequestShapes()
    {
        var command = new Command("demo", "Demo");
        command.AddRequestExample<ChaptersSetRequest>();
        var output = RenderHelp(command);
        Assert.Contains("--help-full", output);
        Assert.Contains("request", output, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run to verify failure**

`dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~HelpExtensionsTests"`
Expected: compile error — no `AddRequestExample`.

- [ ] **Step 3: Implement**

In `HelpExtensions.cs`, beside `AddResponseExample<T>()`:

```csharp
    /// <summary>
    /// Registers the request-body shape for a command that reads JSON from
    /// --input/--stdin. Tagged as a shape section so it renders only under
    /// --help-full, keeping plain --help terse.
    /// </summary>
    public static void AddRequestExample<T>(this Command command)
        => command.AddShapeSection("Request shape", JsonExamples.For(typeof(T)).Split('\n'));
```

Then update the hint (currently `"Run --help-full to see response shape(s)."` around `:170`) to cover both — e.g. `"Run --help-full to see request/response shape(s)."`. Keep it one line.

- [ ] **Step 4: Verify**

`dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj` → expected **432 passing**, 0 failed.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs
git commit -m "feat: add request-shape help sections behind --help-full"
```

---

## Task 3: `items chapters set` — the pilot, and the canonicalisation bug

`ChaptersSetRequest` already exists and is already used as an input gate, so this task proves the pattern end to end and fixes a real defect.

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs` (around `:685-700`)
- Modify: `src/AbsCli/Services/ChaptersService.cs` (doc comment at `:44-53`)
- Test: `tests/AbsCli.Tests/Commands/ItemsChaptersCommandTests.cs`

- [ ] **Step 1: Write the failing test**

The bug: `ChapterWriteEntry.End` is a non-nullable `double`, so a body missing `end` deserialises to `0`, canonicalises to `"end": 0`, and ABS accepts a zero-length chapter instead of returning 400.

Add a test asserting the body sent is the caller's original bytes, not a canonicalisation. Follow the file's existing pattern for asserting on the outgoing body; if it has no such harness, assert instead that the parse gate rejects nothing it accepts today and add the behaviour test at the service boundary.

```csharp
    [Fact]
    public void ChaptersSet_ForwardsOriginalBytes_NotCanonicalised()
    {
        // A body missing "end" must reach ABS unchanged so ABS can 400 it,
        // rather than being silently filled with end: 0.
        const string body = "{\"chapters\":[{\"title\":\"One\",\"start\":0}]}";
        var forwarded = ItemsCommand.PrepareChaptersBody(body);
        Assert.Equal(body, forwarded);
        Assert.DoesNotContain("\"end\"", forwarded);
    }
```

This requires extracting the parse gate into a testable `internal static string PrepareChaptersBody(string jsonBody)` that validates and returns the original string. Do that extraction as part of Step 3.

- [ ] **Step 2: Run to verify failure**

`dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~ItemsChapters"`
Expected: compile error — no `PrepareChaptersBody`.

- [ ] **Step 3: Implement**

In `ItemsCommand.cs`, replace the canonicalisation:

```csharp
            var canonical = JsonSerializer.Serialize(parsed, AppJsonContext.Default.ChaptersSetRequest);
```

with forwarding the original body. Extract the gate:

```csharp
    /// <summary>
    /// Validates a chapters body and returns it unchanged. Parsing is the gate;
    /// the original bytes are what gets sent, so a field the type does not model
    /// is passed through and a missing one is ABS's 400 to give rather than
    /// something we silently default (End is a non-nullable double, so
    /// re-serialising a body without "end" would fill in 0).
    /// </summary>
    internal static string PrepareChaptersBody(string jsonBody)
    {
        JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ChaptersSetRequest);
        return jsonBody;
    }
```

Keep the existing `try/catch (JsonException)` → one error line → `Environment.Exit(1)` at the call site. Pass the result to `service.SetAsync(id, …)`.

Add the shape block to the `set` command registration:

```csharp
        command.AddRequestExample<ChaptersSetRequest>();
```

Update the `ChaptersService.SetAsync` doc comment, which currently says the body "has already been deserialised + re-serialised through ChaptersSetRequest" — that is no longer true.

- [ ] **Step 4: Verify**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet run --project src/AbsCli -- items chapters set --help-full 2>&1 | grep -A6 "Request shape"
```
Expected: tests pass; the shape block shows `chapters` with `title`/`start`/`end`.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs src/AbsCli/Services/ChaptersService.cs tests/
git commit -m "fix: forward the original chapters body instead of a canonicalisation"
```

---

## Task 4: `items update` — the free-form type

**Files:**
- Create: `src/AbsCli/Models/RequestShapes.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Modify: `src/AbsCli/Commands/ItemsCommand.cs` (update command, around `:161`)
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`

- [ ] **Step 1: Create the type**

Fields taken from `Book.updateFromRequest` (`temp/audiobookshelf/server/models/Book.js:370-440`) plus the series handling in `LibraryItemController.updateMedia:233`. ABS accepts exactly these; anything else is ignored.

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for PATCH /api/items/:id/media. ABS validates nothing here
/// (LibraryItemController.updateMedia is `const mediaPayload = req.body`) and
/// applies only the fields below, ignoring the rest — so every field is
/// optional and this type documents what has an effect rather than gating it.
/// Fields per Book.updateFromRequest.
/// </summary>
public class ItemMediaUpdateRequest
{
    [JsonPropertyName("metadata")]
    public ItemMediaUpdateMetadata? Metadata { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Metadata sub-object. String fields accept a number too (ABS coerces it), and
/// null clears the field. `series` is handled separately by the controller
/// (updateSeriesFromRequest) and takes objects, not strings.
/// </summary>
public class ItemMediaUpdateMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("publishedYear")]
    public string? PublishedYear { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("abridged")]
    public bool? Abridged { get; set; }

    [JsonPropertyName("narrators")]
    public List<string>? Narrators { get; set; }

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }
}
```

- [ ] **Step 2: Register both types**

In `src/AbsCli/Models/JsonContext.cs`:

```csharp
[JsonSerializable(typeof(ItemMediaUpdateRequest))]
[JsonSerializable(typeof(ItemMediaUpdateMetadata))]
```

- [ ] **Step 3: Take them off the generator's exclusion list**

`tools/GenerateResponseExamples/Program.cs:112-122` excludes request bodies by name. Request types must now be **included**, so remove `LoginRequest` and the `*RenameRequest` / `Library*Request` entries from `excluded` — but keep `AppConfig` and `UploadManifestEntry`, which are genuinely not wire shapes.

- [ ] **Step 4: Wire the shape block and the syntax gate**

In the `update` command registration add:

```csharp
        command.AddRequestExample<ItemMediaUpdateRequest>();
```

The gate is a **syntax check only** — ABS requires nothing, so require nothing:

```csharp
    /// <summary>
    /// Validates that an items-update body is syntactically JSON and returns it
    /// unchanged. ABS requires no field here, so neither do we — inventing a
    /// requirement it does not have would be client-side policy.
    /// </summary>
    internal static string PrepareMediaUpdateBody(string jsonBody)
    {
        JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ItemMediaUpdateRequest);
        return jsonBody;
    }
```

Call it where the body is read, with the existing `catch (JsonException)` → error → exit 1 shape. **Send the original string.**

- [ ] **Step 5: Add the self-test round-trip**

In `SelfTestCommand.cs`, in the DTO section, following the neighbouring pattern:

```csharp
            Check("ItemMediaUpdateRequest round-trip", () =>
            {
                var obj = new ItemMediaUpdateRequest
                {
                    Metadata = new ItemMediaUpdateMetadata { Title = "T", Genres = new List<string> { "G" } },
                    Tags = new List<string> { "tag" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ItemMediaUpdateRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ItemMediaUpdateRequest)!;
                Assert(back.Metadata!.Title == "T", "title mismatch");
                Assert(back.Tags!.Count == 1, "tags mismatch");
            });
```

- [ ] **Step 6: Write the gate tests**

```csharp
    [Fact]
    public void MediaUpdateBody_EmptyObject_IsAccepted()
    {
        // ABS accepts {} — we must not invent a requirement.
        Assert.Equal("{}", ItemsCommand.PrepareMediaUpdateBody("{}"));
    }

    [Fact]
    public void MediaUpdateBody_UnknownField_IsForwardedUnchanged()
    {
        const string body = "{\"metadata\":{\"title\":\"T\"},\"somethingNew\":1}";
        Assert.Equal(body, ItemsCommand.PrepareMediaUpdateBody(body));
    }

    [Fact]
    public void MediaUpdateBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareMediaUpdateBody("{not json"));
    }
```

- [ ] **Step 7: Verify and commit**

```bash
dotnet format AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet run --project src/AbsCli -- self-test 2>&1 | tail -3
dotnet run --project src/AbsCli -- items update --help-full 2>&1 | grep -A8 "Request shape"
git add -A src tests
git commit -m "feat: document the items update request shape"
```

---

## Task 5: the `items` batch bodies

**Files:**
- Modify: `src/AbsCli/Models/RequestShapes.cs`, `src/AbsCli/Models/JsonContext.cs`
- Modify: `src/AbsCli/Commands/ItemsCommand.cs`
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`
- Test: `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`

Five commands. Shapes and the checks ABS itself performs:

| Command | Type | Body | ABS requires |
|---|---|---|---|
| `batch-update` | `ItemsBatchUpdateEntry` (used as `List<>`) | **bare array** of `{id, metadata?, tags?}` | non-empty array; every element has `id`; ids unique (`LibraryItemController.js:633-640`) |
| `batch-get` | `LibraryItemIdsRequest` | `{libraryItemIds:[…]}` | non-empty array |
| `batch-delete` | `LibraryItemIdsRequest` | `{libraryItemIds:[…]}` | non-empty array |
| `batch-embed-metadata` | `LibraryItemIdsRequest` | `{libraryItemIds:[…]}` | non-empty array |
| `batch-update-progress` | `ProgressBatchEntry` (used as `List<>`) | array of `{libraryItemId, …progress}` | array |

- [ ] **Step 1: Add the types**

`LibraryItemIdsRequest` with `[JsonPropertyName("libraryItemIds")] public List<string> LibraryItemIds { get; set; } = new();`. `ItemsBatchUpdateEntry` with `id` plus the same optional `metadata`/`tags` as Task 4 (reuse `ItemMediaUpdateMetadata`). For `batch-update-progress`, check whether `ProgressUpdateRequest` in `Models/ProgressUpdateRequest.cs` already covers the element shape and reuse it if so — do not create a duplicate.

Register each on `AppJsonContext`. For the two array-bodied commands, also register `List<ItemsBatchUpdateEntry>` / `List<ProgressBatchEntry>` if the generator needs the collection type to emit an array sample; verify by regenerating and reading the output.

- [ ] **Step 2: Add gates that check only what ABS checks**

One `internal static string Prepare…Body` per command, each parsing then returning the original string, and additionally rejecting what ABS rejects. Worked example for the hardest one — follow this exact shape for the other four, adjusting only the requirement being checked:

```csharp
    /// <summary>
    /// Validates a batch-update body and returns it unchanged. ABS requires a
    /// non-empty array whose entries each carry a unique id
    /// (LibraryItemController.js:633-640); we check exactly that and nothing
    /// more. The original bytes are what gets sent, so fields this type does not
    /// model still reach ABS.
    /// </summary>
    internal static string PrepareBatchUpdateBody(string jsonBody)
    {
        var entries = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ListItemsBatchUpdateEntry);
        if (entries is null || entries.Count == 0)
            throw new ArgumentException("batch-update requires a non-empty JSON array of update objects");
        if (entries.Any(e => string.IsNullOrEmpty(e.Id)))
            throw new ArgumentException("every batch-update entry needs an \"id\"");
        if (entries.Select(e => e.Id).Distinct().Count() != entries.Count)
            throw new ArgumentException("batch-update entry ids must be unique");
        return jsonBody;
    }
```

At the call site, catch `JsonException` and `ArgumentException`, log the message as one error line, and `Environment.Exit(1)` — matching the existing pattern in the `chapters set` action.

- [ ] **Step 3: Write the tests first for each gate**

Per command: valid body accepted and returned unchanged; empty array rejected; `batch-update` with a duplicate `id` rejected; malformed JSON throws. Worked example — follow this shape for the rest:

```csharp
    [Fact]
    public void BatchUpdateBody_Valid_IsForwardedUnchanged()
    {
        const string body = "[{\"id\":\"li_a\",\"tags\":[\"x\"]}]";
        Assert.Equal(body, ItemsCommand.PrepareBatchUpdateBody(body));
    }

    [Fact]
    public void BatchUpdateBody_EmptyArray_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareBatchUpdateBody("[]"));
    }

    [Fact]
    public void BatchUpdateBody_DuplicateIds_Rejected()
    {
        Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody("[{\"id\":\"li_a\"},{\"id\":\"li_a\"}]"));
    }

    [Fact]
    public void BatchUpdateBody_MissingId_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareBatchUpdateBody("[{\"tags\":[\"x\"]}]"));
    }

    [Fact]
    public void BatchUpdateBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareBatchUpdateBody("[{"));
    }
```

Note `AppJsonContext.Default.ListItemsBatchUpdateEntry` requires `[JsonSerializable(typeof(List<ItemsBatchUpdateEntry>))]` on the context — the generated property name follows that registration, so confirm the actual name after building rather than assuming.

- [ ] **Step 4: Register the shape blocks**

`command.AddRequestExample<…>()` on each of the five.

- [ ] **Step 5: Self-test round-trips for each new type**, following Task 4 Step 5's pattern.

- [ ] **Step 6: Verify and commit**

```bash
dotnet format AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet run --project src/AbsCli -- self-test 2>&1 | tail -3
for c in batch-update batch-get batch-delete batch-update-progress; do dotnet run --project src/AbsCli -- items $c --help-full 2>&1 | grep -q "Request shape" && echo "$c ok" || echo "$c MISSING"; done
git add -A src tests
git commit -m "feat: document the items batch request shapes"
```

---

## Task 6: `collections` bodies (class A)

**Files:** `RequestShapes.cs`, `JsonContext.cs`, `CollectionsCommand.cs`, `SelfTestCommand.cs`, tests.

| Command | Body | ABS requires |
|---|---|---|
| `create` | `{libraryId, name, description?, books?}` | `name` non-empty after tag-stripping, `libraryId` present (`CollectionController.js:36-40`) |
| `reorder` | `{books:[…]}` | non-empty (`:170`) |
| `batch-add` | `{books:[…]}` | at least one string id (`:320-323`) |
| `batch-remove` | `{books:[…]}` | at least one string id (`:382`) |

- [ ] **Step 1: Add `CollectionCreateRequest` and reuse one `BooksRequest`** (`{books:[…]}`) for the other three — check `Models/CollectionRequests.cs` first, since some of these may already exist; reuse rather than duplicate.
- [ ] **Step 2: Tests first** — valid, empty `books`, missing `name`, missing `libraryId`, malformed.
- [ ] **Step 3: Gates** parsing then returning the original string, rejecting only what ABS rejects.
- [ ] **Step 4: `AddRequestExample<T>()` on all four**, plus self-test round-trips.
- [ ] **Step 5: Verify and commit** — `git commit -m "feat: document the collections request shapes"`

---

## Task 7: `playlists` bodies (class B — read this carefully)

**Files:** `RequestShapes.cs`, `JsonContext.cs`, `PlaylistsCommand.cs`, `SelfTestCommand.cs`, tests.

These three do **not** pass the body through. `PlaylistsCommand.ReadBooksAsync` parses `{books:["li_a",…]}` into a `List<string>` and `PlaylistsService.SerializeItems` emits ABS's `{items:[{libraryItemId}]}`.

- [ ] **Step 1: Do not change the transformation.** `reorder`, `batch-add` and `batch-remove` keep accepting `{books:[…]}` and keep emitting `items`. The existing `{"books":[...]}` descriptions are **correct** and must not be rewritten toward ABS's shape.
- [ ] **Step 2: Document the CLI contract.** Reuse the same `BooksRequest` type from Task 6 and register `AddRequestExample<BooksRequest>()` on all three. Add a one-line note to each command's help that the CLI accepts book ids and sends ABS's item shape, since that asymmetry is exactly the kind of non-obvious caveat the help rules require at the call site.
- [ ] **Step 3: Write a transformation test** asserting the documented contract still yields the expected wire body:

```csharp
    [Fact]
    public void Playlists_BooksContract_ProducesItemsWireBody()
    {
        var wire = PlaylistsService.SerializeItems(new List<string> { "li_a", "li_b" });
        Assert.Contains("\"items\"", wire);
        Assert.Contains("\"libraryItemId\":\"li_a\"", wire.Replace(" ", ""));
        Assert.DoesNotContain("\"books\"", wire);
    }
```

`SerializeItems` may need widening from `private` to `internal` for this. That is the point of the test: the documented input contract and the wire body cannot drift apart silently.

- [ ] **Step 4: Verify and commit** — `git commit -m "feat: document the playlists request contract"`

---

## Task 8: `libraries reorder`

**Files:** `RequestShapes.cs`, `JsonContext.cs`, `LibrariesCommand.cs`, `SelfTestCommand.cs`, tests.

- [ ] **Step 1:** Add `LibraryOrderEntry` — `{id, newOrder}` (`id` string, `newOrder` int) — used as an array body. Check `Models/LibraryRequests.cs` first for an existing type.
- [ ] **Step 2:** Tests first — valid array, empty array, malformed.
- [ ] **Step 3:** Gate parsing then returning the original string; `AddRequestExample<…>()`; self-test round-trip.
- [ ] **Step 4:** Verify and commit — `git commit -m "feat: document the libraries reorder request shape"`

---

## Task 9: Help-text cleanup — 32 descriptions

**Files:** `ItemsCommand.cs`, `CollectionsCommand.cs`, `PlaylistsCommand.cs`, `LibrariesCommand.cs`

- [ ] **Step 1: Replace every shape-carrying description with the uniform pair**

For all 16 sites:

```csharp
        var inputOption = new Option<string?>("--input") { Description = "JSON file with the request body (see --help-full)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the request body from stdin" };
```

This deletes: `JSON file path` ×2, `JSON file with libraryItemIds` (**factually wrong** — that body is a bare array), `JSON file with array body`, `JSON file with {"libraryItemIds":[...]}`, `JSON file with an array of {id, newOrder}`, and the `{"books":[...]}` string repeated across 8 sites.

- [ ] **Step 2: Do NOT touch the `AddExamples` bodies.** e.g. `echo '{"metadata":{"title":"New Title"}}' | abs-cli items update --stdin`. Those are executable one-liners, they appear in plain `--help` where the shape block does not, and they are the fastest thing for an agent to copy.

- [ ] **Step 3: Verify no shape prose survives**

```bash
grep -rn '"--input"\|"--stdin"' --include=*.cs src/AbsCli/Commands | grep -vE "request body" 
```
Expected: no output.

- [ ] **Step 4: Verify and commit**

```bash
dotnet format AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
git add -A src tests
git commit -m "docs: collapse per-option body prose now that shapes are documented"
```

Any test asserting on an old description string needs updating — that is expected, not a regression.

---

## Task 10: Docs and the ABS-bump checklist

**Files:** `docs/input-output.md`, `docs/abs-compatibility.md`

- [ ] **Step 1: `docs/input-output.md`** — the "Input for Updates" section currently says the format "matches what the ABS API expects … No custom schema". Replace that with a statement that each command documents its body shape under `--help-full`, keeping the existing `--input`/`--stdin` examples.

- [ ] **Step 2: Make step 3 of Handling ABS Updates bidirectional.** It currently reads `3. **Update DTOs** if response shapes changed`. Replace with:

```markdown
3. **Update DTOs if request *or* response shapes changed.** For every command with a
   documented request shape, re-read its controller method and confirm the type's
   fields still match — required keys, types, and nesting. Update the type, rebuild
   (which regenerates the samples), and spot-check the affected `--help-full` output.
   A drifted request shape is a correctness bug, not stale docs: agents construct
   payloads from it.
```

- [ ] **Step 3: Add the missing controllers to the diff command** at `docs/abs-compatibility.md:49-56`. The CLI calls endpoints served by `CollectionController`, `PlaylistController`, `MeController`, `ToolsController`, `MiscController`, `CacheController` and `BackupController`, none of which are in the list today — so a bump could change `api/collections/:id/batch/add` and the diff would never show it.

- [ ] **Step 4: Commit** — `git commit -m "docs: require request-shape review on ABS version bumps"`

---

## Task 11: Final verification and PR

- [ ] **Step 1: Commit the spec and plan** (they land with the code, per `CLAUDE.md`)

```bash
git add docs/specs/2026-08-13-request-shapes-in-help-design.md docs/plans/2026-08-13-request-shapes-in-help.md
git commit -m "docs: add request-shapes-in-help spec and plan"
```

- [ ] **Step 2: Full local verification**

```bash
dotnet format AbsCli.sln --verify-no-changes
dotnet test AbsCli.sln
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o ./publish
./publish/abs-cli self-test
```
Expected: format clean; tests 0 failed; self-test 0 failed.

- [ ] **Step 3: Eyeball the actual help output for all 16 commands**

```bash
for c in "items update" "items batch-update" "items batch-get" "items batch-delete" \
         "items batch-update-progress" "items chapters set" "items batch-embed-metadata" \
         "collections create" "collections reorder" "collections batch-add" "collections batch-remove" \
         "playlists reorder" "playlists batch-add" "playlists batch-remove" "libraries reorder"; do
  ./publish/abs-cli $c --help-full 2>&1 | grep -q "Request shape" && echo "OK  $c" || echo "MISSING  $c"
done
```
Every line must read `OK`. This is the acceptance criterion for the whole change.

- [ ] **Step 4: Full smoke on a freshly seeded stack** (required before any PR)

```bash
docker compose -f docker/docker-compose.yml down -v && docker compose -f docker/docker-compose.yml up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
until curl -sf "http://$IP:80/healthcheck" >/dev/null; do sleep 1; done
ABS_URL=http://$IP:80 bash docker/seed.sh
CLI=./publish/abs-cli ABS_URL=http://$IP:80 bash docker/smoke-test.sh 2>&1 | tail -6
docker compose -f docker/docker-compose.yml down -v && rm -rf publish/
```
Expected: `338 passed, 0 failed, 0 skipped`. The suite exercises `items update`, `chapters set` and the batch verbs over real HTTP, so it is the gate that catches a broken gate or a changed wire body.

- [ ] **Step 5: PR** with the real numbers from Steps 2-4. Then watch CI to terminal state and report.

---

## Notes for the implementer

- **Never reject unknown fields.** ABS ignores what it does not recognise; mirroring server policy client-side is ruled out by `CLAUDE.md`.
- **Never send a re-serialisation** for class A. The original bytes go over the wire.
- **Class B (`playlists`) is the trap.** Its `{books:[…]}` help text is correct; "fixing" it toward ABS's `items` shape would make the help actively wrong.
- **Do not add a CHANGELOG entry** — that file belongs to the release process.
- Regenerated `ResponseExamples.g.cs` belongs in the commit of whichever task changed the types.

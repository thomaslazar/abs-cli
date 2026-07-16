# Playlists Command Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `playlists` command group to abs-cli that mirrors the `collections` surface (books-only) plus the playlist-only `create-from-collection` verb.

**Architecture:** A thin `PlaylistsCommand` (System.CommandLine verbs) delegates to a `PlaylistsService` that calls the ABS HTTP API via `AbsApiClient`. Models are AOT-serialized through `AppJsonContext`. Playlists are user-owned, so **no permission tags or `permissionHint` strings** appear anywhere. The CLI accepts the same `{"books":[...]}` input shape as `collections`, and the service maps it to ABS's `items:[{libraryItemId}]` body.

**Tech Stack:** C# / .NET 10, System.CommandLine, System.Text.Json source-gen (AOT), xUnit.

**Spec:** `docs/specs/2026-07-16-playlists-command-design.md`

**Key ABS facts (verified against `temp/audiobookshelf` v2.35.1):**
- Endpoints: `POST /api/playlists`, `GET /api/libraries/:id/playlists` (per-library, non-deprecated), `GET/PATCH/DELETE /api/playlists/:id`, `POST /api/playlists/:id/item`, `DELETE /api/playlists/:id/item/:libraryItemId`, `POST /api/playlists/:id/batch/add`, `POST /api/playlists/:id/batch/remove`, `POST /api/playlists/collection/:collectionId`.
- `create` allows an **empty** playlist (items optional).
- `PATCH /playlists/:id` handles BOTH name/description edits AND reordering (via `items`). Reorder is pure reorder: the `items` length must equal current membership.
- `update` ignores empty-string name/description (so **description cannot be cleared**) and rejects `libraryId`/`userId`.
- `removeItem` and `removeBatch` **delete the playlist when its last item is removed**; both still return the (pre-deletion) expanded JSON.
- `create-from-collection` copies the collection name/description + all books; 400 if the collection has no books; books-only.
- Per-library list returns `{results, total, limit, page}` — the existing `PaginatedResponse` shape.
- No permission checks beyond ownership + library access (enforced server-side in middleware).

**Response JSON shape (`Playlist.toOldJSONExpanded`):**
```json
{ "id": "...", "name": "...", "libraryId": "...", "userId": "...",
  "description": null, "lastUpdate": 0, "createdAt": 0,
  "items": [ { "libraryItemId": "...", "libraryItem": { <expanded> } } ] }
```

---

## Task 0: Create feature branch

- [ ] **Step 1: Branch off main**

```bash
git checkout -b feat/playlists
```

(No commit. The spec + this plan can be committed on this branch once it exists — see Task 9's note.)

---

## Task 1: Playlist response models + JSON registration

**Files:**
- Create: `src/AbsCli/Models/Playlist.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs` (add `[JsonSerializable]` lines)
- Test: `tests/AbsCli.Tests/Services/PlaylistsServiceTests.cs`

- [ ] **Step 1: Write the failing round-trip test**

Create `tests/AbsCli.Tests/Services/PlaylistsServiceTests.cs`:

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class PlaylistsServiceTests
{
    [Fact]
    public void Playlist_RoundTrip_Minimal()
    {
        var obj = new Playlist
        {
            Id = "pl_abc",
            Name = "Roadtrip",
            LibraryId = "lib_1",
            UserId = "usr_1",
            Description = "Long drives",
            LastUpdate = 1716000000000,
            CreatedAt = 1715000000000,
            Items = new List<PlaylistItem>()
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Playlist);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Playlist)!;
        Assert.Equal("pl_abc", back.Id);
        Assert.Equal("Roadtrip", back.Name);
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal("usr_1", back.UserId);
        Assert.Equal("Long drives", back.Description);
        Assert.Equal(1716000000000, back.LastUpdate);
        Assert.Equal(1715000000000, back.CreatedAt);
        Assert.Empty(back.Items);
    }

    [Fact]
    public void Playlist_Deserializes_BookItem()
    {
        var json = """
        {"id":"pl_x","name":"n","libraryId":"lib_1","userId":"u","description":null,
         "lastUpdate":0,"createdAt":0,
         "items":[{"libraryItemId":"li_a","libraryItem":{"id":"li_a","libraryId":"lib_1"}}]}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Playlist)!;
        Assert.Null(back.Description);
        Assert.Single(back.Items);
        Assert.Equal("li_a", back.Items[0].LibraryItemId);
        Assert.NotNull(back.Items[0].LibraryItem);
        Assert.Equal("li_a", back.Items[0].LibraryItem!.Id);
        Assert.Null(back.Items[0].EpisodeId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsServiceTests`
Expected: FAIL to compile — `Playlist` / `PlaylistItem` don't exist.

- [ ] **Step 3: Create the models**

Create `src/AbsCli/Models/Playlist.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Expanded playlist shape returned by ABS — matches
/// <c>Playlist.toOldJSONExpanded()</c> in <c>server/models/Playlist.js</c>.
/// Playlists are user-owned; <see cref="Items"/> are in playlist order.
/// </summary>
public class Playlist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("lastUpdate")]
    public long LastUpdate { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("items")]
    public List<PlaylistItem> Items { get; set; } = new();
}

/// <summary>
/// One entry in a playlist. For books <see cref="LibraryItem"/> holds the
/// expanded item. <see cref="EpisodeId"/> / <see cref="Episode"/> are
/// populated only for podcast playlists — this CLI never creates those, but
/// they are preserved on read so a podcast playlist round-trips faithfully.
/// </summary>
public class PlaylistItem
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";

    [JsonPropertyName("libraryItem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryItemExpanded? LibraryItem { get; set; }

    [JsonPropertyName("episodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EpisodeId { get; set; }

    [JsonPropertyName("episode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Episode { get; set; }
}
```

- [ ] **Step 4: Register the response types in `AppJsonContext`**

In `src/AbsCli/Models/JsonContext.cs`, add after the `CollectionBookRequest` line (line ~75):

```csharp
[JsonSerializable(typeof(Playlist))]
[JsonSerializable(typeof(PlaylistItem))]
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsServiceTests`
Expected: PASS (both facts).

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Models/Playlist.cs src/AbsCli/Models/JsonContext.cs tests/AbsCli.Tests/Services/PlaylistsServiceTests.cs
git commit -m "feat: add playlist response models"
```

---

## Task 2: Playlist request models

**Files:**
- Create: `src/AbsCli/Models/PlaylistRequests.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Test: `tests/AbsCli.Tests/Services/PlaylistsServiceTests.cs` (append)

- [ ] **Step 1: Add failing request round-trip tests**

Append these facts to `PlaylistsServiceTests`:

```csharp
    [Fact]
    public void PlaylistCreateRequest_RoundTrip_AndOmitsNullDescription()
    {
        var obj = new PlaylistCreateRequest
        {
            LibraryId = "lib_1",
            Name = "Roadtrip",
            Description = null,
            Items = new List<PlaylistItemRef>
            {
                new() { LibraryItemId = "li_a" },
                new() { LibraryItemId = "li_b" }
            }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistCreateRequest);
        Assert.DoesNotContain("description", json);
        Assert.Contains("\"libraryItemId\": \"li_a\"", json);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistCreateRequest)!;
        Assert.Equal("lib_1", back.LibraryId);
        Assert.Equal(2, back.Items.Count);
    }

    [Fact]
    public void PlaylistItemsRequest_RoundTrip()
    {
        var obj = new PlaylistItemsRequest
        {
            Items = new List<PlaylistItemRef> { new() { LibraryItemId = "li_a" } }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistItemsRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistItemsRequest)!;
        Assert.Single(back.Items);
        Assert.Equal("li_a", back.Items[0].LibraryItemId);
    }

    [Fact]
    public void PlaylistItemRef_RoundTrip()
    {
        var obj = new PlaylistItemRef { LibraryItemId = "li_z" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PlaylistItemRef);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PlaylistItemRef)!;
        Assert.Equal("li_z", back.LibraryItemId);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsServiceTests`
Expected: FAIL to compile — request types don't exist.

- [ ] **Step 3: Create the request models**

Create `src/AbsCli/Models/PlaylistRequests.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for <c>POST /api/playlists</c>. <see cref="Items"/> may be empty —
/// ABS allows creating an empty playlist (unlike collections).
/// <see cref="Description"/> is omitted when null.
/// </summary>
public class PlaylistCreateRequest
{
    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    public List<PlaylistItemRef> Items { get; set; } = new();
}

/// <summary>
/// Body for reorder (<c>PATCH /api/playlists/:id</c>), batch-add, and
/// batch-remove. For reorder this must be the FULL current membership in the
/// desired order — ABS rejects a length mismatch with 400.
/// </summary>
public class PlaylistItemsRequest
{
    [JsonPropertyName("items")]
    public List<PlaylistItemRef> Items { get; set; } = new();
}

/// <summary>
/// A single playlist item reference. Books-only for this CLI, so just
/// <see cref="LibraryItemId"/>. Used as the <c>POST /playlists/:id/item</c>
/// body and as the elements of the arrays above.
/// </summary>
public class PlaylistItemRef
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = "";
}
```

- [ ] **Step 4: Register the request types in `AppJsonContext`**

In `src/AbsCli/Models/JsonContext.cs`, add after the two Playlist response lines from Task 1:

```csharp
[JsonSerializable(typeof(PlaylistCreateRequest))]
[JsonSerializable(typeof(PlaylistItemsRequest))]
[JsonSerializable(typeof(PlaylistItemRef))]
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsServiceTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Models/PlaylistRequests.cs src/AbsCli/Models/JsonContext.cs tests/AbsCli.Tests/Services/PlaylistsServiceTests.cs
git commit -m "feat: add playlist request models"
```

---

## Task 3: Regenerate response examples

Registering new types in `AppJsonContext` makes `ResponseExamplesDriftTest` fail until `ResponseExamples.g.cs` is regenerated (the generator reflects over `[JsonSerializable]` attributes).

**Files:**
- Modify (generated): `src/AbsCli/Commands/ResponseExamples.g.cs`

- [ ] **Step 1: Confirm the drift test currently fails**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~ResponseExamplesDriftTest`
Expected: FAIL — "ResponseExamples.g.cs is stale."

- [ ] **Step 2: Regenerate**

Run: `dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs`
Expected: exit 0. New `Playlist`, `PlaylistItem`, `PlaylistCreateRequest`, `PlaylistItemsRequest`, `PlaylistItemRef` entries appear in the file.

- [ ] **Step 3: Verify the drift + json-valid tests pass**

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~ResponseExamplesDriftTest|FullyQualifiedName~ResponseExamplesJsonValidTest"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "chore: regenerate response examples for playlist types"
```

---

## Task 4: API endpoints

**Files:**
- Modify: `src/AbsCli/Api/ApiEndpoints.cs` (add after the Collections block, ~line 73)
- Test: `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` (append)

- [ ] **Step 1: Write failing endpoint tests**

Append to `ApiEndpointsTests.cs` (inside the existing test class):

```csharp
    [Fact]
    public void Playlist_Endpoints_AreCorrect()
    {
        Assert.Equal("api/playlists", ApiEndpoints.Playlists);
        Assert.Equal("api/playlists/pl_1", ApiEndpoints.Playlist("pl_1"));
        Assert.Equal("api/libraries/lib_1/playlists", ApiEndpoints.LibraryPlaylists("lib_1"));
        Assert.Equal("api/playlists/pl_1/item", ApiEndpoints.PlaylistItem("pl_1"));
        Assert.Equal("api/playlists/pl_1/item/li_2", ApiEndpoints.PlaylistItemById("pl_1", "li_2"));
        Assert.Equal("api/playlists/pl_1/batch/add", ApiEndpoints.PlaylistBatchAdd("pl_1"));
        Assert.Equal("api/playlists/pl_1/batch/remove", ApiEndpoints.PlaylistBatchRemove("pl_1"));
        Assert.Equal("api/playlists/collection/col_9", ApiEndpoints.PlaylistFromCollection("col_9"));
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~ApiEndpointsTests`
Expected: FAIL to compile — members don't exist.

- [ ] **Step 3: Add the endpoints**

In `src/AbsCli/Api/ApiEndpoints.cs`, after line 73 (`LibraryCollections`), add:

```csharp
    // Playlists
    public const string Playlists = "api/playlists";
    public static string Playlist(string id) => $"api/playlists/{id}";
    public static string LibraryPlaylists(string libraryId) => $"api/libraries/{libraryId}/playlists";
    public static string PlaylistItem(string id) => $"api/playlists/{id}/item";
    public static string PlaylistItemById(string id, string libraryItemId) => $"api/playlists/{id}/item/{libraryItemId}";
    public static string PlaylistBatchAdd(string id) => $"api/playlists/{id}/batch/add";
    public static string PlaylistBatchRemove(string id) => $"api/playlists/{id}/batch/remove";
    public static string PlaylistFromCollection(string collectionId) => $"api/playlists/collection/{collectionId}";
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~ApiEndpointsTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Api/ApiEndpoints.cs tests/AbsCli.Tests/Api/ApiEndpointsTests.cs
git commit -m "feat: add playlist api endpoints"
```

---

## Task 5: PlaylistsService

**Files:**
- Create: `src/AbsCli/Services/PlaylistsService.cs`

No new unit test file here — the service is exercised end-to-end by the smoke test (Task 9) and its request/response models are covered in Tasks 1–2. This matches `CollectionsService` (no direct method-level test).

- [ ] **Step 1: Create the service**

Create `src/AbsCli/Services/PlaylistsService.cs`:

```csharp
using System.Text.Json;
using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

/// <summary>
/// Calls the ABS playlist endpoints. Playlists are user-owned; none of these
/// operations require a <c>user.permissions</c> flag, so no permissionHint is
/// passed. The CLI accepts a <c>{"books":[...]}</c> id list (same as
/// collections) and this service maps it to ABS's <c>items:[{libraryItemId}]</c>
/// body shape.
/// </summary>
public class PlaylistsService
{
    private readonly AbsApiClient _client;

    public PlaylistsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<PaginatedResponse> ListAsync(string libraryId, int limit, int? page)
    {
        var query = HttpUtility.ParseQueryString("");
        query["limit"] = limit.ToString();
        query["page"] = (page ?? 0).ToString();
        var url = ApiEndpoints.LibraryPlaylists(libraryId) + "?" + query;
        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }

    public async Task<Playlist> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Playlist(id), AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> CreateAsync(string libraryId, string name, string? description, List<string> books)
    {
        var body = new PlaylistCreateRequest
        {
            LibraryId = libraryId,
            Name = name,
            Description = description,
            Items = books.Select(b => new PlaylistItemRef { LibraryItemId = b }).ToList()
        };
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistCreateRequest);
        return await _client.PostAsync(ApiEndpoints.Playlists, json, AppJsonContext.Default.Playlist);
    }

    /// <summary>PATCH name/description. Empty values are ignored server-side.</summary>
    public async Task<Playlist> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(ApiEndpoints.Playlist(id), json, AppJsonContext.Default.Playlist);
    }

    /// <summary>
    /// PATCH the playlist with a full ordered item list to reshuffle order.
    /// ABS reorders existing membership only; the list length must equal the
    /// current item count.
    /// </summary>
    public async Task<Playlist> ReorderAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PatchAsync(ApiEndpoints.Playlist(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task DeleteAsync(string id)
    {
        await _client.DeleteAsync(ApiEndpoints.Playlist(id));
    }

    public async Task<Playlist> AddBookAsync(string id, string libraryItemId)
    {
        var body = new PlaylistItemRef { LibraryItemId = libraryItemId };
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistItemRef);
        return await _client.PostAsync(ApiEndpoints.PlaylistItem(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> RemoveBookAsync(string id, string libraryItemId)
    {
        return await _client.DeleteAsync(
            ApiEndpoints.PlaylistItemById(id, libraryItemId),
            AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> BatchAddAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PostAsync(ApiEndpoints.PlaylistBatchAdd(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> BatchRemoveAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PostAsync(ApiEndpoints.PlaylistBatchRemove(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> CreateFromCollectionAsync(string collectionId)
    {
        return await _client.PostEmptyAsync(
            ApiEndpoints.PlaylistFromCollection(collectionId),
            AppJsonContext.Default.Playlist);
    }

    private static string SerializeItems(List<string> books)
    {
        var body = new PlaylistItemsRequest
        {
            Items = books.Select(b => new PlaylistItemRef { LibraryItemId = b }).ToList()
        };
        return JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistItemsRequest);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/AbsCli/AbsCli.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Services/PlaylistsService.cs
git commit -m "feat: add playlists service"
```

---

## Task 6: PlaylistsCommand

**Files:**
- Create: `src/AbsCli/Commands/PlaylistsCommand.cs`
- Test: `tests/AbsCli.Tests/Commands/PlaylistsCommandTests.cs`

- [ ] **Step 1: Write failing command tests**

Create `tests/AbsCli.Tests/Commands/PlaylistsCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class PlaylistsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(PlaylistsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Playlists_HasAllElevenSubcommands()
    {
        var verbs = PlaylistsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[]
        {
            "list", "get", "create", "update", "reorder", "delete",
            "add", "remove", "batch-add", "batch-remove", "create-from-collection"
        }, verbs);
    }

    [Fact]
    public void PlaylistsList_Help_DocumentsFlags()
    {
        var output = RenderHelp("playlists", "list");
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
    }

    [Fact]
    public void PlaylistsCreate_Help_DocumentsEmptyAllowed()
    {
        var output = RenderHelp("playlists", "create");
        Assert.Contains("empty playlist", output);
    }

    [Fact]
    public void PlaylistsRemove_Help_DocumentsAutoDelete()
    {
        var output = RenderHelp("playlists", "remove");
        Assert.Contains("last item", output);
        Assert.Contains("deletes the playlist", output);
    }

    [Fact]
    public void PlaylistsReorder_Help_SaysReorderOnly()
    {
        var output = RenderHelp("playlists", "reorder");
        Assert.Contains("Reorders existing items only", output);
        Assert.Contains("does not add or remove", output);
    }

    [Fact]
    public void PlaylistsCreateFromCollection_Help_DocumentsSnapshot()
    {
        var output = RenderHelp("playlists", "create-from-collection");
        Assert.Contains("--collection", output);
        Assert.Contains("snapshot", output);
    }

    [Fact]
    public void Playlists_NoSubcommand_DeclaresPermission()
    {
        // Playlists are user-owned; NO verb should advertise a required
        // permission. Guards against a stray AddPermissionRequired call.
        foreach (var sub in PlaylistsCommand.Create().Subcommands)
        {
            var help = RenderHelp("playlists", sub.Name);
            Assert.DoesNotContain("Required permission", help);
        }
    }
}
```

Note: the last test's assertion string `"Required permission"` must match the header emitted by `AddPermissionRequired`. Before implementing, confirm the exact header text in `src/AbsCli/Commands/HelpExtensions.cs` (search for the permission section title) and adjust the literal if it differs.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsCommandTests`
Expected: FAIL to compile — `PlaylistsCommand` doesn't exist.

- [ ] **Step 3: Create the command**

Create `src/AbsCli/Commands/PlaylistsCommand.cs`. Mirror `CollectionsCommand.cs` structure exactly (option definitions, `--input`/`--stdin` handling, `_logger` error + `Environment.Exit(1)`), with these differences: **no `AddPermissionRequired` calls**, books parsed then passed to the service as `List<string>`, and the notes below.

```csharp
using System.CommandLine;
using System.Text.Json;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class PlaylistsCommand
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static Command Create()
    {
        var command = new Command("playlists", "Manage playlists (your personal ordered lists of book library items)");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Playlists are per-user, library-scoped ordered lists of book",
            "library items. They are private to you — unlike collections,",
            "which are shared. `update` edits metadata, `reorder` shuffles",
            "order, `add` / `remove` / `batch-*` change membership,",
            "`create-from-collection` snapshots a collection into a new playlist.",
            "Podcast episodes are not supported.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateReorderCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateAddCommand());
        command.Subcommands.Add(CreateRemoveCommand());
        command.Subcommands.Add(CreateBatchAddCommand());
        command.Subcommands.Add(CreateBatchRemoveCommand());
        command.Subcommands.Add(CreateFromCollectionCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var command = new Command("list", "List your playlists in a library")
        { libraryOption, limitOption, pageOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Lists only YOUR playlists in the given library. --library falls",
            "back to the configured defaultLibrary. Uses the per-library",
            "endpoint; there is no cross-library listing.");
        command.AddExamples(
            "abs-cli playlists list --library \"lib_1\"",
            "abs-cli playlists list --library \"lib_1\" --limit 20 --page 0");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new PlaylistsService(client);
            var result = await service.ListAsync(libraryId, limit, page);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var command = new Command("get", "Get a single playlist (expanded)") { idOption };
        command.AddExamples("abs-cli playlists get --id \"pl_abc\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.GetAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateCreateCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var nameOption = new Option<string>("--name") { Description = "Playlist name", Required = true };
        var descriptionOption = new Option<string?>("--description") { Description = "Optional description" };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("create", "Create a playlist")
        { libraryOption, nameOption, descriptionOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Books are optional — omit --input/--stdin to create an empty",
            "playlist (ABS allows this). --library falls back to the configured",
            "defaultLibrary. Items must be books in that library; podcast",
            "episodes are not supported. Input shape: `{\"books\":[\"lid\",...]}`.");
        command.AddExamples(
            "abs-cli playlists create --library \"lib_1\" --name \"Roadtrip\"",
            "abs-cli playlists create --library \"lib_1\" --name \"Roadtrip\" --input books.json");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var name = parseResult.GetValue(nameOption)!;
            var description = parseResult.GetValue(descriptionOption);
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            List<string> books;
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            if (stdin || input != null)
            {
                var booksJson = stdin
                    ? await Console.In.ReadToEndAsync(cancellationToken)
                    : CommandHelper.ReadJsonInput(input!);
                if (!TryParseBooks(booksJson, out books)) { Environment.Exit(1); return 1; }
            }
            else
            {
                books = new List<string>();
            }
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new PlaylistsService(client);
            var result = await service.CreateAsync(libraryId, name, description, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (empty string is rejected)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description (cannot be cleared — empty string is ignored by ABS)" };
        var command = new Command("update", "Edit a playlist's name and/or description")
        { idOption, nameOption, descriptionOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Edits metadata only. Use `reorder` to change item order; use",
            "`add` / `remove` / `batch-*` to change membership. Empty --name",
            "is rejected. Unlike collections, an empty --description does NOT",
            "clear the field — ABS ignores it. Library and owner cannot change.");
        command.AddExamples(
            "abs-cli playlists update --id \"pl_abc\" --name \"Renamed\"",
            "abs-cli playlists update --id \"pl_abc\" --description \"New notes\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
                return 1;
            }
            var body = BuildUpdateBody(name, description);
            if (body.Count == 0)
            {
                _logger.Error("Specify at least one of --name, --description");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateReorderCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("reorder", "Reorder existing items in a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Reorders existing items only — does not add or remove. Pass the",
            "FULL current membership in the desired order; ABS rejects a",
            "length mismatch with 400.",
            "",
            "Example for a 3-item playlist: `{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}`",
            "moves li_c to position 1.");
        command.AddExamples(
            "abs-cli playlists reorder --id \"pl_abc\" --input order.json",
            "echo '{\"books\":[\"li_c\",\"li_a\",\"li_b\"]}' | abs-cli playlists reorder --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            if (!ReadBooks(parseResult, inputOption, stdinOption, cancellationToken, out var booksTask)) { Environment.Exit(1); return 1; }
            var books = await booksTask;
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.ReorderAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var command = new Command("delete", "Delete a playlist") { idOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Hard delete of the playlist record and its membership rows. No",
            "confirmation prompt — playlists are yours and cheap to recreate.");
        command.AddExamples("abs-cli playlists delete --id \"pl_abc\"");
        command.AddShapeSection("Response shape", "{ \"success\": \"true\" }");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
            return 0;
        });
        return command;
    }

    private static Command CreateAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to add", Required = true };
        var command = new Command("add", "Add a single book to a playlist")
        { idOption, bookOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "The book must be in the SAME library as the playlist (400",
            "otherwise). Podcast episodes are not supported.");
        command.AddExamples("abs-cli playlists add --id \"pl_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.AddBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var bookOption = new Option<string>("--book") { Description = "Library item ID to remove", Required = true };
        var command = new Command("remove", "Remove a single book from a playlist")
        { idOption, bookOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removing the last item deletes the playlist. The response is the",
            "playlist state prior to deletion.");
        command.AddExamples("abs-cli playlists remove --id \"pl_abc\" --book \"li_xyz\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var book = parseResult.GetValue(bookOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.RemoveBookAsync(id, book);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchAddCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("batch-add", "Add multiple books to a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Silently skips books already in the playlist. Books must be in",
            "the same library as the playlist.");
        command.AddExamples(
            "abs-cli playlists batch-add --id \"pl_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli playlists batch-add --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            if (!ReadBooks(parseResult, inputOption, stdinOption, cancellationToken, out var booksTask)) { Environment.Exit(1); return 1; }
            var books = await booksTask;
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.BatchAddAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateBatchRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Playlist ID", Required = true };
        var inputOption = new Option<string?>("--input") { Description = "JSON file with books array (`{\"books\":[\"lid\",...]}`)" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read books JSON from stdin" };
        var command = new Command("batch-remove", "Remove multiple books from a playlist")
        { idOption, inputOption, stdinOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Tolerates books not in the playlist (no-op for those). Removing",
            "the playlist's last item deletes the playlist.");
        command.AddExamples(
            "abs-cli playlists batch-remove --id \"pl_abc\" --input books.json",
            "echo '{\"books\":[\"li_a\",\"li_b\"]}' | abs-cli playlists batch-remove --id \"pl_abc\" --stdin");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            if (!ReadBooks(parseResult, inputOption, stdinOption, cancellationToken, out var booksTask)) { Environment.Exit(1); return 1; }
            var books = await booksTask;
            if (books is null) { Environment.Exit(1); return 1; }
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.BatchRemoveAsync(id, books);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    private static Command CreateFromCollectionCommand()
    {
        var collectionOption = new Option<string>("--collection") { Description = "Source collection ID", Required = true };
        var command = new Command("create-from-collection", "Create a playlist from a collection")
        { collectionOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Snapshot: copies the collection's name, description, and all its",
            "books (in collection order) into a NEW playlist you own. Books",
            "only. 400 if the collection has no books. No live link — later",
            "changes to the collection do not propagate.");
        command.AddExamples("abs-cli playlists create-from-collection --collection \"col_abc\"");
        command.AddResponseExample<Playlist>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var collectionId = parseResult.GetValue(collectionOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new PlaylistsService(client);
            var result = await service.CreateFromCollectionAsync(collectionId);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Playlist);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Resolve --input/--stdin into a parsed books list. Returns false and
    /// logs on argument-usage errors; on success <paramref name="booksTask"/>
    /// yields the list, or null if the JSON was invalid (already logged).
    /// </summary>
    private static bool ReadBooks(
        System.CommandLine.ParseResult parseResult,
        Option<string?> inputOption,
        Option<bool> stdinOption,
        CancellationToken cancellationToken,
        out Task<List<string>?> booksTask)
    {
        var input = parseResult.GetValue(inputOption);
        var stdin = parseResult.GetValue(stdinOption);
        if (stdin && input != null)
        {
            _logger.Error("Provide --input or --stdin, not both.");
            booksTask = Task.FromResult<List<string>?>(null);
            return false;
        }
        if (!stdin && input == null)
        {
            _logger.Error("Provide --input <file> or --stdin.");
            booksTask = Task.FromResult<List<string>?>(null);
            return false;
        }
        booksTask = ReadBooksAsync(stdin, input, cancellationToken);
        return true;
    }

    private static async Task<List<string>?> ReadBooksAsync(bool stdin, string? input, CancellationToken cancellationToken)
    {
        var booksJson = stdin
            ? await Console.In.ReadToEndAsync(cancellationToken)
            : CommandHelper.ReadJsonInput(input!);
        return TryParseBooks(booksJson, out var books) ? books : null;
    }

    private static bool TryParseBooks(string booksJson, out List<string> books)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize(booksJson, AppJsonContext.Default.CollectionBooksRequest);
            books = parsed?.Books ?? new List<string>();
            return true;
        }
        catch (JsonException ex)
        {
            _logger.Error($"Invalid JSON input: {ex.Message}");
            books = new List<string>();
            return false;
        }
    }

    /// <summary>Mirrors <c>CollectionsCommand.BuildUpdateBody</c>.</summary>
    internal static Dictionary<string, string> BuildUpdateBody(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (name is not null) body["name"] = name;
        if (description is not null) body["description"] = description;
        return body;
    }
}
```

Implementation note: the `ReadBooks` helper exists to avoid the `out`-parameter-with-`await` limitation while keeping each verb compact. If it fights the compiler, fall back to the inline `string booksJson; if (stdin && input != null) {...}` block used verbatim in `CollectionsCommand` reorder/batch actions — behavior must be identical either way (error + `Environment.Exit(1)` on bad args or invalid JSON).

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter FullyQualifiedName~PlaylistsCommandTests`
Expected: PASS (all facts).

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Commands/PlaylistsCommand.cs tests/AbsCli.Tests/Commands/PlaylistsCommandTests.cs
git commit -m "feat: add playlists command"
```

---

## Task 7: Wire into the root command

**Files:**
- Modify: `src/AbsCli/Program.cs:29` (next to the `CollectionsCommand` registration)

- [ ] **Step 1: Register the command**

In `src/AbsCli/Program.cs`, immediately after the line
`rootCommand.Subcommands.Add(CollectionsCommand.Create());` add:

```csharp
rootCommand.Subcommands.Add(PlaylistsCommand.Create());
```

- [ ] **Step 2: Verify it's reachable**

Run: `dotnet run --project src/AbsCli -- playlists --help`
Expected: help lists all 11 subcommands (`list`, `get`, `create`, `update`, `reorder`, `delete`, `add`, `remove`, `batch-add`, `batch-remove`, `create-from-collection`).

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Program.cs
git commit -m "feat: register playlists command"
```

---

## Task 8: README Commands table

**Files:**
- Modify: `README.md` (Commands table, after the `collections batch-remove` row, ~line 236)

- [ ] **Step 1: Add the playlist rows**

Insert after the last `collections` row:

```markdown
| `playlists list [--library <id>] [--limit] [--page]` | List your playlists in a library (paginated; per-library) |
| `playlists get --id <id>` | Get a single playlist (expanded) |
| `playlists create [--library <id>] --name <n> [--description <d>] [{--input \| --stdin}]` | Create a playlist (books optional — empty allowed) |
| `playlists update --id <id> [--name <n>] [--description <d>]` | Edit name and/or description (description cannot be cleared) |
| `playlists reorder --id <id> {--input \| --stdin}` | Reorder existing items (full ordered list; does NOT add/remove) |
| `playlists delete --id <id>` | Delete a playlist |
| `playlists add --id <id> --book <lid>` | Add a single book (must be in the playlist's library) |
| `playlists remove --id <id> --book <lid>` | Remove a single book (emptying deletes the playlist) |
| `playlists batch-add --id <id> {--input \| --stdin}` | Add multiple books (silently skips duplicates) |
| `playlists batch-remove --id <id> {--input \| --stdin}` | Remove multiple books (emptying deletes the playlist) |
| `playlists create-from-collection --collection <id>` | Snapshot a collection into a new playlist (books only) |
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add playlists to commands table"
```

---

## Task 9: Formatting, full test suite, and smoke test

**Files:**
- Modify: `docker/smoke-test.sh` (add a playlist lifecycle block)
- Modify: `docker/seed.sh` only if a seeded collection with books is not already available for the `create-from-collection` assertion.

- [ ] **Step 1: Format**

Run: `dotnet format AbsCli.sln`
Expected: exits clean; no residual diff on second run.

- [ ] **Step 2: Full unit test suite**

Run: `dotnet test AbsCli.sln`
Expected: all tests pass (includes `ResponseExamplesDriftTest`, `PermissionSectionTests`, `RootHelpTests`).

- [ ] **Step 3: Add a playlist lifecycle to the smoke test**

Read `docker/smoke-test.sh` and follow its existing collections block as the template. Add a block that, against the live stack, exercises (capturing ids with the same `jq` pattern the script already uses):

1. `playlists create --library "$LIB" --name "Smoke Playlist"` → capture `id`.
2. `playlists add --id "$PID" --book "$ITEM1"` → assert `items` length 1.
3. `playlists add --id "$PID" --book "$ITEM2"` → assert length 2.
4. `playlists reorder --id "$PID"` with `{"books":["<item2>","<item1>"]}` → assert first item is item2.
5. `playlists list --library "$LIB"` → assert the playlist appears in `results`.
6. `playlists get --id "$PID"` → assert name/order.
7. `playlists remove --id "$PID" --book "$ITEM1"` → assert length 1.
8. `playlists remove --id "$PID" --book "$ITEM2"` (last item) → then `playlists get --id "$PID"` should fail / 404 (auto-deleted). Assert the playlist is gone.
9. `playlists create-from-collection --collection "$COL"` against a seeded collection that has ≥1 book → assert a new playlist with matching item count; clean it up with `playlists delete`.

If the seed has no collection with books, extend `docker/seed.sh` to create one (do not drop the `create-from-collection` assertion silently — see the "Extend seed for 403" convention).

- [ ] **Step 4: Run the smoke test against the local stack**

Per `CLAUDE.md`:
```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh      # if freshly created
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: smoke test reports success, including the new playlist assertions.

- [ ] **Step 5: Commit**

```bash
git add docker/smoke-test.sh docker/seed.sh
git commit -m "test: add playlist lifecycle to smoke test"
```

- [ ] **Step 6: Open the PR** (only after the user confirms)

Follow `CLAUDE.md` Git Conventions and Post-PR verification. Mark "smoke test passed" only because Step 4 actually ran. Present the PR URL as a clickable link and watch CI to green.

---

## Notes on repo conventions honored by this plan

- **No `AddPermissionRequired` and no `permissionHint`** anywhere — playlists are user-owned. A dedicated test (`Playlists_NoSubcommand_DeclaresPermission`) guards this.
- **CHANGELOG.md is untouched** — owned by the release workflow.
- **Roadmap is untouched** — no mid-milestone item-ticking.
- **Spec/plan commits:** this plan and the spec live on the `feat/playlists` branch and merge via the PR; do not detour to a direct main commit.
- **`--help` documents every quirk** — empty-create, last-item auto-delete, reorder-only, same-library add, no-clear description, collection snapshot.
- Every step that commits assumes the branch from Task 0 exists.

## Self-review (completed)

- **Spec coverage:** every command-surface row and every "sharp edge" in the spec maps to a task (models T1–2, endpoints T4, service T5, verbs + help caveats T6, wiring T7, README T8, smoke T9). ✓
- **Placeholders:** none — all code and commands are concrete. ✓
- **Type consistency:** `Playlist`, `PlaylistItem`, `PlaylistCreateRequest`, `PlaylistItemsRequest`, `PlaylistItemRef`, and every `ApiEndpoints`/`PlaylistsService` member name are used identically across tasks. ✓
- **Ambiguity:** reorder/batch bodies are explicitly `{"books":[...]}` on input, mapped to `items:[{libraryItemId}]` in the service; list reuses `PaginatedResponse`. ✓

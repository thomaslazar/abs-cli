# Tags & Genres Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `tags` and `genres` top-level commands (list / rename / delete) that pass through to Audiobookshelf's tag & genre management endpoints.

**Architecture:** Two thin command classes (`TagsCommand`, `GenresCommand`) mirroring `AuthorsCommand`, each backed by a service (`TagsService`, `GenresService`) that calls `AbsApiClient`. Rename/delete take positional arguments (like `config set`). All six subcommands require admin. Response/request shapes are plain AOT-registered models; the delete path param is base64-then-URI-encoded because ABS decodes it via `Buffer.from(decodeURIComponent(param), 'base64')`.

**Tech Stack:** C# / .NET, System.CommandLine, System.Text.Json source-generation (`AppJsonContext`), xUnit.

**Spec:** `docs/specs/2026-07-10-tags-genres-management-design.md`

**Conventions to honor:** No unnecessary blank lines in method bodies. Run `dotnet format AbsCli.sln` before each commit. Do NOT edit `CHANGELOG.md` (release-owned). Ask before any `git commit` if working with a human; agents follow the commit steps.

---

## File Structure

New:
- `src/AbsCli/Models/TagModels.cs` — `TagListResponse`, `TagRenameRequest`, `TagRenameResponse`, `TagDeleteResponse`
- `src/AbsCli/Models/GenreModels.cs` — `GenreListResponse`, `GenreRenameRequest`, `GenreRenameResponse`, `GenreDeleteResponse`
- `src/AbsCli/Services/TagsService.cs`
- `src/AbsCli/Services/GenresService.cs`
- `src/AbsCli/Commands/TagsCommand.cs`
- `src/AbsCli/Commands/GenresCommand.cs`
- `tests/AbsCli.Tests/Services/TagsServiceTests.cs`
- `tests/AbsCli.Tests/Services/GenresServiceTests.cs`
- `tests/AbsCli.Tests/Commands/TagsCommandTests.cs`
- `tests/AbsCli.Tests/Commands/GenresCommandTests.cs`

Modified:
- `src/AbsCli/Api/ApiEndpoints.cs` — six endpoints + `EncodePathValue` helper
- `src/AbsCli/Models/JsonContext.cs` — register the 8 new types
- `tools/GenerateResponseExamples/Program.cs` — exclude the two request types
- `src/AbsCli/Commands/ResponseExamples.g.cs` — regenerated (do not hand-edit)
- `src/AbsCli/Program.cs` — register the two commands
- `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` — new file if absent (see Task 2)
- `README.md` — Commands table
- `docs/abs-api-coverage.md` — fix GET permission
- `docker/seed.sh`, `docker/smoke-test.sh`

---

## Task 1: Endpoint helpers with base64 encoding

**Files:**
- Modify: `src/AbsCli/Api/ApiEndpoints.cs` (append before the final closing `}`)
- Test: `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs`:

```csharp
using AbsCli.Api;
using Xunit;

namespace AbsCli.Tests.Api;

public class ApiEndpointsTests
{
    [Fact]
    public void TagByName_Base64EncodesThenUriEscapes()
    {
        // "a" -> base64 "YQ==" -> URI-escaped "YQ%3D%3D"
        Assert.Equal("api/tags/YQ%3D%3D", ApiEndpoints.TagByName("a"));
    }

    [Fact]
    public void TagByName_HandlesSpecialCharacters()
    {
        // "sci/fi" -> base64 "c2NpL2Zp" (no escapable chars)
        Assert.Equal("api/tags/c2NpL2Zp", ApiEndpoints.TagByName("sci/fi"));
    }

    [Fact]
    public void GenreByName_Base64EncodesThenUriEscapes()
    {
        Assert.Equal("api/genres/YQ%3D%3D", ApiEndpoints.GenreByName("a"));
    }

    [Fact]
    public void TagAndGenreConstants_AreStable()
    {
        Assert.Equal("api/tags", ApiEndpoints.Tags);
        Assert.Equal("api/tags/rename", ApiEndpoints.TagRename);
        Assert.Equal("api/genres", ApiEndpoints.Genres);
        Assert.Equal("api/genres/rename", ApiEndpoints.GenreRename);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: FAIL — `ApiEndpoints` has no `TagByName` / `Tags` / etc. (compile error).

- [ ] **Step 3: Add the endpoints**

In `src/AbsCli/Api/ApiEndpoints.cs`, add before the class's closing brace:

```csharp
    // Tags & Genres (all admin-only — MiscController.js gates every route on isAdminOrUp)
    public const string Tags = "api/tags";
    public const string TagRename = "api/tags/rename";
    public static string TagByName(string tag) => $"api/tags/{EncodePathValue(tag)}";
    public const string Genres = "api/genres";
    public const string GenreRename = "api/genres/rename";
    public static string GenreByName(string genre) => $"api/genres/{EncodePathValue(genre)}";

    // ABS decodes the :tag / :genre param via
    // Buffer.from(decodeURIComponent(param), 'base64'), so base64-encode the
    // value then URI-escape it into the path segment.
    private static string EncodePathValue(string value)
        => Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/ApiEndpoints.cs tests/AbsCli.Tests/Api/ApiEndpointsTests.cs
git commit -m "feat: add tags/genres endpoint helpers with base64 path encoding"
```

---

## Task 2: Models + JSON context registration

**Files:**
- Create: `src/AbsCli/Models/TagModels.cs`, `src/AbsCli/Models/GenreModels.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Test: `tests/AbsCli.Tests/Services/TagsServiceTests.cs`, `tests/AbsCli.Tests/Services/GenresServiceTests.cs` (create — round-trip only for now)

- [ ] **Step 1: Write the failing tests**

Create `tests/AbsCli.Tests/Services/TagsServiceTests.cs`:

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class TagsServiceTests
{
    [Fact]
    public void TagListResponse_Deserializes()
    {
        var json = """{"tags":["Fantasy","Sci-Fi"]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagListResponse)!;
        Assert.Equal(new[] { "Fantasy", "Sci-Fi" }, back.Tags);
    }

    [Fact]
    public void TagRenameRequest_Serializes_AbsFieldNames()
    {
        var req = new TagRenameRequest { Tag = "scifi", NewTag = "Science Fiction" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.TagRenameRequest);
        Assert.Contains("\"tag\":\"scifi\"", json);
        Assert.Contains("\"newTag\":\"Science Fiction\"", json);
    }

    [Fact]
    public void TagRenameResponse_Deserializes()
    {
        var json = """{"tagMerged":true,"numItemsUpdated":3}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagRenameResponse)!;
        Assert.True(back.TagMerged);
        Assert.Equal(3, back.NumItemsUpdated);
    }

    [Fact]
    public void TagDeleteResponse_Deserializes()
    {
        var json = """{"numItemsUpdated":5}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TagDeleteResponse)!;
        Assert.Equal(5, back.NumItemsUpdated);
    }
}
```

Create `tests/AbsCli.Tests/Services/GenresServiceTests.cs`:

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class GenresServiceTests
{
    [Fact]
    public void GenreListResponse_Deserializes()
    {
        var json = """{"genres":["Horror","Mystery"]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreListResponse)!;
        Assert.Equal(new[] { "Horror", "Mystery" }, back.Genres);
    }

    [Fact]
    public void GenreRenameRequest_Serializes_AbsFieldNames()
    {
        var req = new GenreRenameRequest { Genre = "horror", NewGenre = "Horror" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.GenreRenameRequest);
        Assert.Contains("\"genre\":\"horror\"", json);
        Assert.Contains("\"newGenre\":\"Horror\"", json);
    }

    [Fact]
    public void GenreRenameResponse_Deserializes()
    {
        var json = """{"genreMerged":false,"numItemsUpdated":2}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreRenameResponse)!;
        Assert.False(back.GenreMerged);
        Assert.Equal(2, back.NumItemsUpdated);
    }

    [Fact]
    public void GenreDeleteResponse_Deserializes()
    {
        var json = """{"numItemsUpdated":0}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.GenreDeleteResponse)!;
        Assert.Equal(0, back.NumItemsUpdated);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests --filter "TagsServiceTests|GenresServiceTests"`
Expected: FAIL — types and `AppJsonContext.Default.TagListResponse` etc. don't exist (compile error).

- [ ] **Step 3: Create the model files**

`src/AbsCli/Models/TagModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Response from GET /api/tags. Server-sorted case-insensitively.</summary>
public class TagListResponse
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>Request body for POST /api/tags/rename.</summary>
public class TagRenameRequest
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";
    [JsonPropertyName("newTag")]
    public string NewTag { get; set; } = "";
}

/// <summary>Response from POST /api/tags/rename. tagMerged is true when the new name already existed.</summary>
public class TagRenameResponse
{
    [JsonPropertyName("tagMerged")]
    public bool TagMerged { get; set; }
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

/// <summary>Response from DELETE /api/tags/:tag.</summary>
public class TagDeleteResponse
{
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}
```

`src/AbsCli/Models/GenreModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Response from GET /api/genres. Returned in discovery order (NOT sorted).</summary>
public class GenreListResponse
{
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}

/// <summary>Request body for POST /api/genres/rename.</summary>
public class GenreRenameRequest
{
    [JsonPropertyName("genre")]
    public string Genre { get; set; } = "";
    [JsonPropertyName("newGenre")]
    public string NewGenre { get; set; } = "";
}

/// <summary>Response from POST /api/genres/rename. genreMerged is true when the new name already existed.</summary>
public class GenreRenameResponse
{
    [JsonPropertyName("genreMerged")]
    public bool GenreMerged { get; set; }
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}

/// <summary>Response from DELETE /api/genres/:genre.</summary>
public class GenreDeleteResponse
{
    [JsonPropertyName("numItemsUpdated")]
    public int NumItemsUpdated { get; set; }
}
```

- [ ] **Step 4: Register the types in `JsonContext.cs`**

In `src/AbsCli/Models/JsonContext.cs`, add these attribute lines alongside the existing `[JsonSerializable(...)]` block (place near the author entries for locality):

```csharp
[JsonSerializable(typeof(TagListResponse))]
[JsonSerializable(typeof(TagRenameRequest))]
[JsonSerializable(typeof(TagRenameResponse))]
[JsonSerializable(typeof(TagDeleteResponse))]
[JsonSerializable(typeof(GenreListResponse))]
[JsonSerializable(typeof(GenreRenameRequest))]
[JsonSerializable(typeof(GenreRenameResponse))]
[JsonSerializable(typeof(GenreDeleteResponse))]
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests --filter "TagsServiceTests|GenresServiceTests"`
Expected: PASS (8 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Models/TagModels.cs src/AbsCli/Models/GenreModels.cs src/AbsCli/Models/JsonContext.cs tests/AbsCli.Tests/Services/TagsServiceTests.cs tests/AbsCli.Tests/Services/GenresServiceTests.cs
git commit -m "feat: add tag/genre request and response models"
```

---

## Task 3: Services

**Files:**
- Create: `src/AbsCli/Services/TagsService.cs`, `src/AbsCli/Services/GenresService.cs`

No new tests here — services are pure pass-through over `AbsApiClient` (no injectable HTTP seam in this codebase; behavior is covered by the model round-trip tests in Task 2, the endpoint tests in Task 1, and the live smoke test in Task 7). This matches how `AuthorsService`/`CollectionsService` are structured.

- [ ] **Step 1: Create `TagsService.cs`**

```csharp
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class TagsService
{
    private readonly AbsApiClient _client;

    public TagsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<TagListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Tags,
            AppJsonContext.Default.TagListResponse, "admin permission");
    }

    public async Task<TagRenameResponse> RenameAsync(string tag, string newTag)
    {
        var json = JsonSerializer.Serialize(
            new TagRenameRequest { Tag = tag, NewTag = newTag },
            AppJsonContext.Default.TagRenameRequest);
        return await _client.PostAsync(ApiEndpoints.TagRename, json,
            AppJsonContext.Default.TagRenameResponse, "admin permission");
    }

    public async Task<TagDeleteResponse> DeleteAsync(string tag)
    {
        return await _client.DeleteAsync(ApiEndpoints.TagByName(tag),
            AppJsonContext.Default.TagDeleteResponse, "admin permission");
    }
}
```

- [ ] **Step 2: Create `GenresService.cs`**

```csharp
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class GenresService
{
    private readonly AbsApiClient _client;

    public GenresService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<GenreListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Genres,
            AppJsonContext.Default.GenreListResponse, "admin permission");
    }

    public async Task<GenreRenameResponse> RenameAsync(string genre, string newGenre)
    {
        var json = JsonSerializer.Serialize(
            new GenreRenameRequest { Genre = genre, NewGenre = newGenre },
            AppJsonContext.Default.GenreRenameRequest);
        return await _client.PostAsync(ApiEndpoints.GenreRename, json,
            AppJsonContext.Default.GenreRenameResponse, "admin permission");
    }

    public async Task<GenreDeleteResponse> DeleteAsync(string genre)
    {
        return await _client.DeleteAsync(ApiEndpoints.GenreByName(genre),
            AppJsonContext.Default.GenreDeleteResponse, "admin permission");
    }
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build src/AbsCli`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Services/TagsService.cs src/AbsCli/Services/GenresService.cs
git commit -m "feat: add TagsService and GenresService"
```

---

## Task 4: Exclude request types from the response-example generator, regenerate

**Why:** `ResponseExamples.g.cs` is generated by reflecting over every `[JsonSerializable]` type in `AppJsonContext`, minus an exclusion set. `TagRenameRequest` and `GenreRenameRequest` are request bodies, not responses, so they must be excluded (like `LoginRequest`). Then regenerate the file so the response types get samples and `ResponseExamplesDriftTest` stays green.

**Files:**
- Modify: `tools/GenerateResponseExamples/Program.cs`
- Modify (generated): `src/AbsCli/Commands/ResponseExamples.g.cs`

- [ ] **Step 1: Add the request types to the exclusion set**

In `tools/GenerateResponseExamples/Program.cs`, find the `excluded` `HashSet<Type>` in `DiscoverResponseTypes()` and add:

```csharp
            typeof(TagRenameRequest),
            typeof(GenreRenameRequest),
```

(Place them after the existing `typeof(UploadManifestEntry),` line.)

- [ ] **Step 2: Regenerate the file**

Run: `dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs`
Expected: exit 0; `git status` shows `ResponseExamples.g.cs` modified with entries for `TagListResponse`, `TagRenameResponse`, `TagDeleteResponse`, `GenreListResponse`, `GenreRenameResponse`, `GenreDeleteResponse` and NO entries for the two request types.

- [ ] **Step 3: Run the drift test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter ResponseExamplesDriftTest`
Expected: PASS.

- [ ] **Step 4: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/Program.cs src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "chore: regenerate response examples with tag/genre types"
```

---

## Task 5: TagsCommand

**Files:**
- Create: `src/AbsCli/Commands/TagsCommand.cs`
- Modify: `src/AbsCli/Program.cs`
- Test: `tests/AbsCli.Tests/Commands/TagsCommandTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AbsCli.Tests/Commands/TagsCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class TagsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(TagsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Tags_HasThreeSubcommands()
    {
        var verbs = TagsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void TagsRename_UsesPositionalArgs()
    {
        var output = RenderHelp("tags", "rename");
        Assert.Contains("old-tag", output);
        Assert.Contains("new-tag", output);
        Assert.DoesNotContain("--old-tag", output);
    }

    [Fact]
    public void TagsRename_Help_DocumentsMerge()
    {
        var output = RenderHelp("tags", "rename");
        Assert.Contains("merge", output.ToLowerInvariant());
    }

    [Fact]
    public void AllSubcommands_RequireAdmin()
    {
        Assert.Contains("admin", RenderHelp("tags", "list"));
        Assert.Contains("admin", RenderHelp("tags", "rename"));
        Assert.Contains("admin", RenderHelp("tags", "delete"));
        Assert.Contains("Permission required:", RenderHelp("tags", "list"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter TagsCommandTests`
Expected: FAIL — `TagsCommand` does not exist (compile error).

- [ ] **Step 3: Create `TagsCommand.cs`**

```csharp
using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class TagsCommand
{
    public static Command Create()
    {
        var command = new Command("tags", "Manage tags");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "All tag operations require admin.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all tags (server-sorted, case-insensitive)");
        command.AddPermissionRequired("admin");
        command.AddExamples("abs-cli tags list");
        command.AddResponseExample<TagListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldTagArg = new Argument<string>("old-tag") { Description = "Existing tag" };
        var newTagArg = new Argument<string>("new-tag") { Description = "New tag name" };
        var command = new Command("rename", "Rename a tag across all items")
        {
            oldTagArg,
            newTagArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old tag",
            "move onto the existing tag and the response reports tagMerged: true.");
        command.AddExamples("abs-cli tags rename scifi \"Science Fiction\"");
        command.AddResponseExample<TagRenameResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldTag = parseResult.GetValue(oldTagArg)!;
            var newTag = parseResult.GetValue(newTagArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.RenameAsync(oldTag, newTag);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagRenameResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var tagArg = new Argument<string>("tag") { Description = "Tag to delete" };
        var command = new Command("delete", "Remove a tag from every item that has it")
        {
            tagArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the tag from all items and returns numItemsUpdated. No",
            "confirmation prompt.");
        command.AddExamples("abs-cli tags delete scifi");
        command.AddResponseExample<TagDeleteResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var tag = parseResult.GetValue(tagArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new TagsService(client);
            var result = await service.DeleteAsync(tag);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.TagDeleteResponse);
            return 0;
        });
        return command;
    }
}
```

- [ ] **Step 4: Register in `Program.cs`**

In `src/AbsCli/Program.cs`, after `rootCommand.Subcommands.Add(AuthorsCommand.Create());` add:

```csharp
rootCommand.Subcommands.Add(TagsCommand.Create());
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter TagsCommandTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/TagsCommand.cs src/AbsCli/Program.cs tests/AbsCli.Tests/Commands/TagsCommandTests.cs
git commit -m "feat: add tags command (list, rename, delete)"
```

---

## Task 6: GenresCommand

**Files:**
- Create: `src/AbsCli/Commands/GenresCommand.cs`
- Modify: `src/AbsCli/Program.cs`
- Test: `tests/AbsCli.Tests/Commands/GenresCommandTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AbsCli.Tests/Commands/GenresCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class GenresCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(GenresCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Genres_HasThreeSubcommands()
    {
        var verbs = GenresCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void GenresRename_UsesPositionalArgs()
    {
        var output = RenderHelp("genres", "rename");
        Assert.Contains("old-genre", output);
        Assert.Contains("new-genre", output);
        Assert.DoesNotContain("--old-genre", output);
    }

    [Fact]
    public void GenresList_Help_DocumentsUnsorted()
    {
        var output = RenderHelp("genres", "list");
        Assert.Contains("unsorted", output.ToLowerInvariant());
    }

    [Fact]
    public void AllSubcommands_RequireAdmin()
    {
        Assert.Contains("admin", RenderHelp("genres", "list"));
        Assert.Contains("admin", RenderHelp("genres", "rename"));
        Assert.Contains("admin", RenderHelp("genres", "delete"));
        Assert.Contains("Permission required:", RenderHelp("genres", "delete"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter GenresCommandTests`
Expected: FAIL — `GenresCommand` does not exist (compile error).

- [ ] **Step 3: Create `GenresCommand.cs`**

```csharp
using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class GenresCommand
{
    public static Command Create()
    {
        var command = new Command("genres", "Manage genres");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "All genre operations require admin.");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all genres (unsorted — server discovery order)");
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Genres are returned unsorted (server discovery order), unlike tags.");
        command.AddExamples("abs-cli genres list");
        command.AddResponseExample<GenreListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.ListAsync();
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldGenreArg = new Argument<string>("old-genre") { Description = "Existing genre" };
        var newGenreArg = new Argument<string>("new-genre") { Description = "New genre name" };
        var command = new Command("rename", "Rename a genre across all items")
        {
            oldGenreArg,
            newGenreArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old genre",
            "move onto the existing genre and the response reports genreMerged: true.");
        command.AddExamples("abs-cli genres rename scifi \"Science Fiction\"");
        command.AddResponseExample<GenreRenameResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldGenre = parseResult.GetValue(oldGenreArg)!;
            var newGenre = parseResult.GetValue(newGenreArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.RenameAsync(oldGenre, newGenre);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreRenameResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var genreArg = new Argument<string>("genre") { Description = "Genre to delete" };
        var command = new Command("delete", "Remove a genre from every item that has it")
        {
            genreArg
        };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the genre from all items and returns numItemsUpdated. No",
            "confirmation prompt.");
        command.AddExamples("abs-cli genres delete scifi");
        command.AddResponseExample<GenreDeleteResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var genre = parseResult.GetValue(genreArg)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new GenresService(client);
            var result = await service.DeleteAsync(genre);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.GenreDeleteResponse);
            return 0;
        });
        return command;
    }
}
```

- [ ] **Step 4: Register in `Program.cs`**

In `src/AbsCli/Program.cs`, after the `TagsCommand` line added in Task 5, add:

```csharp
rootCommand.Subcommands.Add(GenresCommand.Create());
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter GenresCommandTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/GenresCommand.cs src/AbsCli/Program.cs tests/AbsCli.Tests/Commands/GenresCommandTests.cs
git commit -m "feat: add genres command (list, rename, delete)"
```

---

## Task 7: Docs — README table + coverage-doc fix

**Files:**
- Modify: `README.md`
- Modify: `docs/abs-api-coverage.md`

- [ ] **Step 1: Add README Commands table rows**

In `README.md`, immediately after the `authors image remove ...` row (currently line ~240) and before the `search --query ...` row, insert:

```markdown
| `tags list` | List all tags (admin; server-sorted) |
| `tags rename <old-tag> <new-tag>` | Rename a tag across all items (admin; merges if new name exists) |
| `tags delete <tag>` | Remove a tag from all items (admin) |
| `genres list` | List all genres (admin; unsorted) |
| `genres rename <old-genre> <new-genre>` | Rename a genre across all items (admin; merges if new name exists) |
| `genres delete <genre>` | Remove a genre from all items (admin) |
```

- [ ] **Step 2: Fix the coverage doc**

In `docs/abs-api-coverage.md`, change the two GET rows so the permission column reads `admin` instead of blank:

```markdown
| GET | `/api/tags` | List tags | admin | — |
```
and
```markdown
| GET | `/api/genres` | List genres | admin | — |
```

(The four write rows already show `admin` — leave them.)

- [ ] **Step 3: Verify no other stale references**

Run: `rg -n "api/tags|api/genres" docs/abs-api-coverage.md`
Expected: all six rows now show `admin` in the permission column.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/abs-api-coverage.md
git commit -m "docs: document tags/genres commands and fix GET permission in coverage map"
```

---

## Task 8: Seed + smoke test

**Files:**
- Modify: `docker/seed.sh`
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Ensure seed produces tags & genres**

Inspect `docker/seed.sh`. Books seeded via the ABS API can carry `tags` and `genres` in their media metadata. Confirm at least two distinct tags and two genres exist across the seeded items; if the current seed sets none, add `tags` and `genres` arrays to at least two seeded books' media update payloads (follow the existing update-item pattern in the script). Include one throwaway tag `smoke-temp-tag` and genre `smoke-temp-genre` on a single item so the smoke test can delete them non-destructively.

Run (after bringing the stack up per CLAUDE.md): `ABS_URL=http://<container-ip>:80 bash docker/seed.sh`
Expected: exits 0.

- [ ] **Step 2: Add `tags`/`genres` to the help-examples enumeration**

In `docker/smoke-test.sh`, add `"tags"` and `"genres"` to the parent-command list (the `for cmd in ...` loop around line 113) and add their leaf verbs to the leaf-command list (around line 129), e.g.:

```bash
           "tags list" "tags rename" "tags delete" \
           "genres list" "genres rename" "genres delete" \
```

- [ ] **Step 3: Add a tags/genres assertion section**

In `docker/smoke-test.sh`, after the authors section, add a new section mirroring the existing style:

```bash
# ============================================================
# tags & genres
# ============================================================
output=$("$CLI" tags list 2>&1)
assert_json_key "tags list returns JSON" "tags" "$output"
assert_json_expr "tags list non-empty" "len(d['tags'])>0" "$output"

output=$("$CLI" genres list 2>&1)
assert_json_key "genres list returns JSON" "genres" "$output"
assert_json_expr "genres list non-empty" "len(d['genres'])>0" "$output"

# rename roundtrip (rename then rename back) — proves the write path + response shape
output=$("$CLI" tags rename smoke-temp-tag smoke-temp-tag-renamed 2>&1)
assert_json_key "tags rename returns numItemsUpdated" "numItemsUpdated" "$output"
output=$("$CLI" tags rename smoke-temp-tag-renamed smoke-temp-tag 2>&1)
assert_json_key "tags rename back returns numItemsUpdated" "numItemsUpdated" "$output"

# delete the throwaway tag & genre
output=$("$CLI" tags delete smoke-temp-tag 2>&1)
assert_json_key "tags delete returns numItemsUpdated" "numItemsUpdated" "$output"
output=$("$CLI" genres delete smoke-temp-genre 2>&1)
assert_json_key "genres delete returns numItemsUpdated" "numItemsUpdated" "$output"
```

(Use the same helper names — `assert_json_key`, `assert_json_expr` — already defined at the top of the script. If the seeded test user is not admin, note it: these endpoints require admin, so the smoke user must be admin. The default seeded root user is admin — verify via `reference_abs_instance` context.)

- [ ] **Step 4: Run the smoke test**

Per CLAUDE.md, against the compose stack:
```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: all assertions pass, script exits 0. Only mark "smoke test passed" after seeing this.

- [ ] **Step 5: Commit**

```bash
git add docker/seed.sh docker/smoke-test.sh
git commit -m "test: add tags/genres smoke assertions and seed data"
```

---

## Task 9: Full verification

- [ ] **Step 1: Full unit test run**

Run: `dotnet test AbsCli.sln`
Expected: all tests pass (including `ResponseExamplesDriftTest`, `PermissionSectionTests`, and the new suites).

- [ ] **Step 2: Format check (matches CI)**

Run: `dotnet format AbsCli.sln --verify-no-changes`
Expected: no changes reported. If it fails, run `dotnet format AbsCli.sln`, commit as `chore: fix formatting`, and re-run.

- [ ] **Step 3: Confirm the two commands are wired**

Run: `dotnet run --project src/AbsCli -- --help`
Expected: `tags` and `genres` appear in the command list.

- [ ] **Step 4: Smoke test already run in Task 8**

Confirm it passed there. This is the gate for the PR description checkbox — do not check it unverified.

---

## Self-Review Notes (author checklist — completed during planning)

- **Spec coverage:** all six endpoints (Task 1/3/5/6), admin permission tagging (Task 5/6), base64 delete (Task 1), merge-on-rename help (Task 5/6), genres-unsorted help (Task 6), README + coverage-doc fix (Task 7), seed + smoke (Task 8), YAGNI exclusions honored (no confirm prompt, no client-side sort).
- **Generator coupling:** Task 4 handles the `ResponseExamples.g.cs` drift test — the one non-obvious cross-cutting dependency.
- **Type consistency:** property names (`Tags`/`Genres`/`TagMerged`/`GenreMerged`/`NumItemsUpdated`) and `AppJsonContext.Default.*` accessors match across models, services, commands, and tests.
- **CHANGELOG untouched** (release-owned).

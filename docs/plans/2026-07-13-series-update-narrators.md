# Series Update & Narrator Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `series update` (edit name/description) and a new library-scoped `narrators` command (list/rename/delete), both thin pass-throughs over ABS.

**Architecture:** `series update` extends the existing `SeriesCommand`/`SeriesService`, mirroring `authors update`. `narrators` is a new command + service + models mirroring the tags/genres commands, but library-scoped and gated on `update` (list is unrestricted). Narrator delete/rename base64-encode the name into the path via the existing `ApiEndpoints.EncodePathValue`.

**Tech Stack:** C# / .NET, System.CommandLine, System.Text.Json source-generation (`AppJsonContext`), xUnit.

**Spec:** `docs/specs/2026-07-13-series-update-narrators-design.md`

**Conventions:** No unnecessary blank lines in method bodies. `dotnet format AbsCli.sln` before each commit. Conventional Commits, imperative lowercase, no period; NO `Co-Authored-By`, NO "Generated with Claude Code". Do NOT edit `CHANGELOG.md`.

---

## File Structure

New:
- `src/AbsCli/Models/NarratorModels.cs` — `NarratorItem`, `NarratorListResponse`, `NarratorUpdateResponse`, `NarratorRenameRequest`
- `src/AbsCli/Services/NarratorsService.cs`
- `src/AbsCli/Commands/NarratorsCommand.cs`
- `tests/AbsCli.Tests/Services/NarratorsServiceTests.cs`
- `tests/AbsCli.Tests/Commands/NarratorsCommandTests.cs`

Modified:
- `src/AbsCli/Api/ApiEndpoints.cs` — `LibraryNarrators`, `LibraryNarratorByName`
- `src/AbsCli/Models/JsonContext.cs` — register the 4 narrator types
- `tools/GenerateResponseExamples/Program.cs` — exclude `NarratorRenameRequest`
- `src/AbsCli/Commands/ResponseExamples.g.cs` — regenerated
- `src/AbsCli/Commands/SeriesCommand.cs` — add `update` subcommand + `BuildUpdateBodyForTesting`
- `src/AbsCli/Services/SeriesService.cs` — add `UpdateAsync`
- `src/AbsCli/Program.cs` — register `NarratorsCommand`
- `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` — narrator encoding tests
- `tests/AbsCli.Tests/Commands/SeriesCommandTests.cs` — create
- `README.md`, `docs/abs-api-coverage.md`, `docker/seed.sh`, `docker/smoke-test.sh`

---

## Task 1: Narrator endpoint helpers

**Files:**
- Modify: `src/AbsCli/Api/ApiEndpoints.cs`
- Test: `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` (append to existing file)

- [ ] **Step 1: Add failing tests**

Append to `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` inside the `ApiEndpointsTests` class:

```csharp
    [Fact]
    public void LibraryNarrators_BuildsListPath()
    {
        Assert.Equal("api/libraries/lib_1/narrators", ApiEndpoints.LibraryNarrators("lib_1"));
    }

    [Fact]
    public void LibraryNarratorByName_Base64EncodesThenUriEscapes()
    {
        // "a" -> base64 "YQ==" -> URI-escaped "YQ%3D%3D"
        Assert.Equal("api/libraries/lib_1/narrators/YQ%3D%3D", ApiEndpoints.LibraryNarratorByName("lib_1", "a"));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: FAIL (compile error — members don't exist).

- [ ] **Step 3: Add the endpoints**

In `src/AbsCli/Api/ApiEndpoints.cs`, add near the other tag/genre helpers (the `EncodePathValue` helper already exists and is reused):

```csharp
    // Narrators (library-scoped; list is unrestricted, rename/delete need 'update')
    public static string LibraryNarrators(string libraryId) => $"api/libraries/{libraryId}/narrators";
    public static string LibraryNarratorByName(string libraryId, string name) => $"api/libraries/{libraryId}/narrators/{EncodePathValue(name)}";
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/ApiEndpoints.cs tests/AbsCli.Tests/Api/ApiEndpointsTests.cs
git commit -m "feat: add narrator endpoint helpers"
```

---

## Task 2: Narrator models + JSON context

**Files:**
- Create: `src/AbsCli/Models/NarratorModels.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Test: `tests/AbsCli.Tests/Services/NarratorsServiceTests.cs`

- [ ] **Step 1: Add failing tests**

Create `tests/AbsCli.Tests/Services/NarratorsServiceTests.cs`:

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class NarratorsServiceTests
{
    [Fact]
    public void NarratorListResponse_Deserializes()
    {
        var json = """{"narrators":[{"id":"Um9iIEluZ2xpcw==","name":"Rob Inglis","numBooks":3}]}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.NarratorListResponse)!;
        Assert.Single(back.Narrators);
        Assert.Equal("Um9iIEluZ2xpcw==", back.Narrators[0].Id);
        Assert.Equal("Rob Inglis", back.Narrators[0].Name);
        Assert.Equal(3, back.Narrators[0].NumBooks);
    }

    [Fact]
    public void NarratorRenameRequest_Serializes_NameField()
    {
        var req = new NarratorRenameRequest { Name = "Robert Inglis" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.NarratorRenameRequest);
        Assert.Contains("\"name\": \"Robert Inglis\"", json);
    }

    [Fact]
    public void NarratorUpdateResponse_Deserializes()
    {
        var json = """{"updated":4}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.NarratorUpdateResponse)!;
        Assert.Equal(4, back.Updated);
    }
}
```

Note: `AppJsonContext` uses `WriteIndented = true`, so serialized JSON has a space after the colon (`"name": "..."`). That is why the assertion includes the space.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter NarratorsServiceTests`
Expected: FAIL (types don't exist).

- [ ] **Step 3: Create the model file**

`src/AbsCli/Models/NarratorModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>One narrator from GET /api/libraries/:id/narrators. id is the URI-encoded base64 of the name.</summary>
public class NarratorItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("numBooks")]
    public int NumBooks { get; set; }
}

/// <summary>Response from GET /api/libraries/:id/narrators. Natural-sorted by name.</summary>
public class NarratorListResponse
{
    [JsonPropertyName("narrators")]
    public List<NarratorItem> Narrators { get; set; } = new();
}

/// <summary>Request body for PATCH /api/libraries/:id/narrators/:narratorId.</summary>
public class NarratorRenameRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>Response from PATCH/DELETE narrator — number of items whose narrator list changed.</summary>
public class NarratorUpdateResponse
{
    [JsonPropertyName("updated")]
    public int Updated { get; set; }
}
```

- [ ] **Step 4: Register in `JsonContext.cs`**

Add to the `[JsonSerializable(...)]` block on `AppJsonContext` (near the tag/genre entries):

```csharp
[JsonSerializable(typeof(NarratorItem))]
[JsonSerializable(typeof(NarratorListResponse))]
[JsonSerializable(typeof(NarratorRenameRequest))]
[JsonSerializable(typeof(NarratorUpdateResponse))]
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter NarratorsServiceTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Models/NarratorModels.cs src/AbsCli/Models/JsonContext.cs tests/AbsCli.Tests/Services/NarratorsServiceTests.cs
git commit -m "feat: add narrator models"
```

---

## Task 3: Exclude narrator request type from generator, regenerate

**Why:** `ResponseExamples.g.cs` is generated by reflecting over `[JsonSerializable]` types minus an exclusion set. `NarratorRenameRequest` is a request body, not a response — exclude it (like `TagRenameRequest`/`GenreRenameRequest`).

**Files:**
- Modify: `tools/GenerateResponseExamples/Program.cs`
- Modify (generated): `src/AbsCli/Commands/ResponseExamples.g.cs`

- [ ] **Step 1: Add to the exclusion set**

In `tools/GenerateResponseExamples/Program.cs`, in the `excluded` `HashSet<Type>` in `DiscoverResponseTypes()` (already contains `TagRenameRequest`, `GenreRenameRequest`), add after those:

```csharp
            typeof(NarratorRenameRequest),
```

- [ ] **Step 2: Regenerate**

Run: `dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs`
Expected: exit 0.

- [ ] **Step 3: Verify contents**

```bash
grep -c "NarratorRenameRequest" src/AbsCli/Commands/ResponseExamples.g.cs   # expect 0
grep -c "NarratorListResponse\|NarratorUpdateResponse\|NarratorItem" src/AbsCli/Commands/ResponseExamples.g.cs   # expect 3
```

- [ ] **Step 4: Run drift test**

Run: `dotnet test tests/AbsCli.Tests --filter ResponseExamplesDriftTest`
Expected: PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/Program.cs src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "chore: regenerate response examples with narrator types"
```

---

## Task 4: NarratorsService

**Files:**
- Create: `src/AbsCli/Services/NarratorsService.cs`

Thin pass-through, no new tests (covered by model tests + endpoint tests + live smoke), matching the `TagsService`/`GenresService` pattern.

- [ ] **Step 1: Create the service**

`src/AbsCli/Services/NarratorsService.cs`:

```csharp
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class NarratorsService
{
    private readonly AbsApiClient _client;

    public NarratorsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<NarratorListResponse> ListAsync(string libraryId)
    {
        return await _client.GetAsync(ApiEndpoints.LibraryNarrators(libraryId),
            AppJsonContext.Default.NarratorListResponse);
    }

    public async Task<NarratorUpdateResponse> RenameAsync(string libraryId, string oldName, string newName)
    {
        var json = JsonSerializer.Serialize(
            new NarratorRenameRequest { Name = newName },
            AppJsonContext.Default.NarratorRenameRequest);
        return await _client.PatchAsync(ApiEndpoints.LibraryNarratorByName(libraryId, oldName), json,
            AppJsonContext.Default.NarratorUpdateResponse, "'update' permission");
    }

    public async Task<NarratorUpdateResponse> DeleteAsync(string libraryId, string name)
    {
        return await _client.DeleteAsync(ApiEndpoints.LibraryNarratorByName(libraryId, name),
            AppJsonContext.Default.NarratorUpdateResponse, "'update' permission");
    }
}
```

Verify `AbsApiClient.PatchAsync<T>(endpoint, jsonBody, JsonTypeInfo<T>, permissionHint?)` and `DeleteAsync<T>(endpoint, JsonTypeInfo<T>, permissionHint?)` signatures match (they are used identically in `AuthorsService`/`TagsService`). Adjust if the real order differs.

- [ ] **Step 2: Build**

Run: `dotnet build src/AbsCli`
Expected: 0 errors.

- [ ] **Step 3: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Services/NarratorsService.cs
git commit -m "feat: add NarratorsService"
```

---

## Task 5: NarratorsCommand

**Files:**
- Create: `src/AbsCli/Commands/NarratorsCommand.cs`
- Modify: `src/AbsCli/Program.cs`
- Test: `tests/AbsCli.Tests/Commands/NarratorsCommandTests.cs`

- [ ] **Step 1: Add failing test**

Create `tests/AbsCli.Tests/Commands/NarratorsCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class NarratorsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(NarratorsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Narrators_HasThreeSubcommands()
    {
        var verbs = NarratorsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "rename", "delete" }, verbs);
    }

    [Fact]
    public void NarratorsRename_UsesPositionalArgs()
    {
        var output = RenderHelp("narrators", "rename");
        Assert.Contains("old-narrator", output);
        Assert.Contains("new-narrator", output);
        Assert.DoesNotContain("--old-narrator", output);
    }

    [Fact]
    public void NarratorsRenameAndDelete_RequireUpdate()
    {
        Assert.Contains("update", RenderHelp("narrators", "rename"));
        Assert.Contains("Permission required:", RenderHelp("narrators", "rename"));
        Assert.Contains("update", RenderHelp("narrators", "delete"));
        Assert.Contains("Permission required:", RenderHelp("narrators", "delete"));
    }

    [Fact]
    public void NarratorsList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("narrators", "list"));
    }

    [Fact]
    public void NarratorsDelete_Help_DocumentsUpdateNotDelete()
    {
        var output = RenderHelp("narrators", "delete").ToLowerInvariant();
        Assert.Contains("update", output);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter NarratorsCommandTests`
Expected: FAIL (NarratorsCommand missing).

- [ ] **Step 3: Create `NarratorsCommand.cs`**

```csharp
using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class NarratorsCommand
{
    public static Command Create()
    {
        var command = new Command("narrators", "Manage narrators");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Narrators are derived from book metadata and scoped to a library.",
            "'rename' and 'delete' both require the 'update' permission (delete",
            "does NOT need 'delete').");
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("list", "List narrators in a library (natural-sorted by name)")
        { libraryOption };
        command.AddExamples("abs-cli narrators list");
        command.AddResponseExample<NarratorListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.ListAsync(libraryId);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorListResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldArg = new Argument<string>("old-narrator") { Description = "Existing narrator name" };
        var newArg = new Argument<string>("new-narrator") { Description = "New narrator name" };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("rename", "Rename a narrator across all items in a library")
        {
            oldArg,
            newArg,
            libraryOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Renaming to a name that already exists MERGES: items on the old name",
            "move onto the existing narrator. Returns the count of items updated.");
        command.AddExamples("abs-cli narrators rename \"Rob Inglis\" \"Robert Inglis\"");
        command.AddResponseExample<NarratorUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var oldName = parseResult.GetValue(oldArg)!;
            var newName = parseResult.GetValue(newArg)!;
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.RenameAsync(libraryId, oldName, newName);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorUpdateResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var narratorArg = new Argument<string>("narrator") { Description = "Narrator name to remove" };
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var command = new Command("delete", "Remove a narrator from every item in a library that has it")
        {
            narratorArg,
            libraryOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Requires the 'update' permission (NOT 'delete'). Removes the narrator",
            "from all items and returns the count of items updated. No confirmation",
            "prompt.");
        command.AddExamples("abs-cli narrators delete \"Rob Inglis\"");
        command.AddResponseExample<NarratorUpdateResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var narrator = parseResult.GetValue(narratorArg)!;
            var library = parseResult.GetValue(libraryOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new NarratorsService(client);
            var result = await service.DeleteAsync(libraryId, narrator);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.NarratorUpdateResponse);
            return 0;
        });
        return command;
    }
}
```

Verify against `SeriesCommand.CreateListCommand` that `CommandHelper.BuildClient(libraryOverride:)` and `CommandHelper.RequireLibrary(config)` exist with these signatures. Verify `AddPermissionRequired`, `AddHelpSection`, `AddResponseExample<T>` as used in `TagsCommand`.

- [ ] **Step 4: Register in `Program.cs`**

In `src/AbsCli/Program.cs`, after `rootCommand.Subcommands.Add(GenresCommand.Create());` add:

```csharp
rootCommand.Subcommands.Add(NarratorsCommand.Create());
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter NarratorsCommandTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/NarratorsCommand.cs src/AbsCli/Program.cs tests/AbsCli.Tests/Commands/NarratorsCommandTests.cs
git status --short   # if ResponseExamples.g.cs shows modified, revert it: git checkout src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "feat: add narrators command (list, rename, delete)"
```

---

## Task 6: Series update

**Files:**
- Modify: `src/AbsCli/Services/SeriesService.cs`, `src/AbsCli/Commands/SeriesCommand.cs`
- Test: `tests/AbsCli.Tests/Commands/SeriesCommandTests.cs` (create)

- [ ] **Step 1: Add failing tests**

Create `tests/AbsCli.Tests/Commands/SeriesCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class SeriesCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(SeriesCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Series_HasListGetUpdate()
    {
        var verbs = SeriesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "update" }, verbs);
    }

    [Fact]
    public void SeriesUpdate_RequiresUpdatePermission()
    {
        var output = RenderHelp("series", "update");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void SeriesUpdate_Help_DocumentsNoMerge()
    {
        var output = RenderHelp("series", "update").ToLowerInvariant();
        Assert.Contains("duplicate", output);
    }

    [Fact]
    public void BuildUpdateBody_OmitsUnsetKeys()
    {
        var body = SeriesCommand.BuildUpdateBodyForTesting("New Name", null);
        Assert.True(body.ContainsKey("name"));
        Assert.False(body.ContainsKey("description"));
        Assert.Equal("New Name", body["name"]);
    }

    [Fact]
    public void BuildUpdateBody_IncludesEmptyDescription()
    {
        var body = SeriesCommand.BuildUpdateBodyForTesting(null, "");
        Assert.True(body.ContainsKey("description"));
        Assert.Equal("", body["description"]);
        Assert.False(body.ContainsKey("name"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests --filter SeriesCommandTests`
Expected: FAIL (no `update` subcommand / no `BuildUpdateBodyForTesting`).

- [ ] **Step 3: Add `UpdateAsync` to `SeriesService.cs`**

Add `using System.Text.Json;` to the top of `src/AbsCli/Services/SeriesService.cs`, then add this method to the class:

```csharp
    public async Task<SeriesItem> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(
            ApiEndpoints.SeriesById(id),
            json,
            AppJsonContext.Default.SeriesItem,
            "'update' permission");
    }
```

(Confirm `AppJsonContext.Default.DictionaryStringString` exists — it is used by `AuthorsService.UpdateAsync`.)

- [ ] **Step 4: Add the `update` subcommand to `SeriesCommand.cs`**

Add a logger field at the top of the class (after `public static class SeriesCommand {`):

```csharp
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
```

Register the subcommand in `Create()` after `command.Subcommands.Add(CreateGetCommand());`:

```csharp
        command.Subcommands.Add(CreateUpdateCommand());
```

Add these two methods to the class:

```csharp
    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Series ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (does NOT merge into an existing same-named series — see Notes)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description; empty string clears the field" };
        var command = new Command("update", "Edit a series' name and/or description")
        {
            idOption,
            nameOption,
            descriptionOption
        };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Unlike 'authors update', renaming to an existing series name does NOT",
            "merge — ABS creates a second series with the duplicate name. Empty",
            "--name is rejected; --description \"\" clears the field. At least one",
            "of --name / --description is required.");
        command.AddExamples(
            "abs-cli series update --id \"se_abc\" --name \"The Stormlight Archive\"",
            "abs-cli series update --id \"se_abc\" --description \"Epic fantasy\"");
        command.AddResponseExample<SeriesItem>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
            }
            var body = BuildUpdateBodyForTesting(name, description);
            if (body.Count == 0)
            {
                _logger.Error("Specify at least one of --name, --description");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new SeriesService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.SeriesItem);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body. name is included only when non-empty (empty is
    /// rejected upstream); description is included whenever supplied, so an
    /// empty string clears it server-side. Exposed internally for unit testing.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBodyForTesting(string? name, string? description)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description;
        return body;
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests --filter SeriesCommandTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Services/SeriesService.cs src/AbsCli/Commands/SeriesCommand.cs tests/AbsCli.Tests/Commands/SeriesCommandTests.cs
git commit -m "feat: add series update command"
```

---

## Task 7: Docs — README + coverage map

**Files:**
- Modify: `README.md`, `docs/abs-api-coverage.md`

- [ ] **Step 1: README Commands table**

In `README.md`, add a `series update` row right after the `series get` row:

```markdown
| `series update --id <id> [--name <n>] [--description <d>\|""]` | Edit name and/or description (no merge-on-rename — see help) |
```

And add three narrator rows. Place them in a sensible spot (e.g. right after the `authors image remove` row / before the `tags` rows):

```markdown
| `narrators list` | List narrators in a library |
| `narrators rename <old-narrator> <new-narrator>` | Rename a narrator across a library (update; merges if new name exists) |
| `narrators delete <narrator>` | Remove a narrator from a library (update — not delete) |
```

Match the exact column format of surrounding rows.

- [ ] **Step 2: Coverage doc**

In `docs/abs-api-coverage.md`:
- `| PATCH | \`/api/series/:id\` | Update series | update | — |` → change the last column to `` `series update` ✅ ``.
- `| GET | \`/api/libraries/:id/narrators\` | List narrators | | — |` → last column `` `narrators list` ✅ ``.
- `| PATCH | \`/api/libraries/:id/narrators/:narratorId\` | Update narrator | update | — |` → last column `` `narrators rename` ✅ ``.
- `| DELETE | \`/api/libraries/:id/narrators/:narratorId\` | Remove narrator | delete | — |` → **fix permission `delete` → `update`** AND last column `` `narrators delete` ✅ ``.

- [ ] **Step 3: Verify**

```bash
rg -n "api/series/:id|narrators" docs/abs-api-coverage.md
rg -n "series update|narrators" README.md
```
Confirm the DELETE narrator row shows `update` (not `delete`) and all four rows show ✅.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/abs-api-coverage.md
git commit -m "docs: document series update + narrators, fix narrator delete permission"
```

---

## Task 8: Seed + smoke test

**Files:**
- Modify: `docker/seed.sh`, `docker/smoke-test.sh`

- [ ] **Step 1: Seed narrators**

In `docker/seed.sh`, find the "Seeding tags and genres" block added previously (it PATCHes `TAG_ITEM1`/`TAG_ITEM2` media). Extend those two PATCH bodies to also set narrators under `metadata`, OR add a follow-up block reusing the same item IDs. Add narrators including a throwaway `smoke-temp-narrator`. Example — change the second PATCH body to:

```bash
curl -sf -X PATCH "$ABS_URL/api/items/$TAG_ITEM2/media" \
    -H "$AUTH" -H 'Content-Type: application/json' \
    -d '{"tags":["Favorites","smoke-temp-tag"],"metadata":{"genres":["Science Fiction","smoke-temp-genre"],"narrators":["Rob Inglis","smoke-temp-narrator"]}}' > /dev/null
```

And add a narrator to the first item's metadata too (so the list has more than one):

```bash
curl -sf -X PATCH "$ABS_URL/api/items/$TAG_ITEM1/media" \
    -H "$AUTH" -H 'Content-Type: application/json' \
    -d '{"tags":["Favorites"],"metadata":{"genres":["Fantasy"],"narrators":["Rob Inglis"]}}' > /dev/null
```

- [ ] **Step 2: Help-example enumeration**

In `docker/smoke-test.sh`:
- Add `"narrators"` to the parent-command loop.
- Add to the leaf-command loop:
  ```bash
             "series update" \
             "narrators list" "narrators rename" "narrators delete" \
  ```

- [ ] **Step 3: series update smoke assertions**

Add to the Series section (or after it). Pick a seeded series id from `series list`:

```bash
SERIES_ID=$(json_get "$($CLI series list 2>/dev/null)" "['results'][0]['id']")
output=$($CLI series update --id "$SERIES_ID" --description "Smoke test series desc" 2>&1)
assert_json_expr "series update sets description" "d['description']=='Smoke test series desc'" "$output"
assert_json_expr "series update returns same id" "d['id']=='$SERIES_ID'" "$output"
# restore
$CLI series update --id "$SERIES_ID" --description "" 2>/dev/null > /dev/null
```

Confirm the `series list` results key: it is a `PaginatedResponse` with `results`. If `json_get` for `series list` differs, inspect the actual shape and adjust the accessor.

- [ ] **Step 4: narrators smoke assertions**

Add a Narrators section (after Tags & Genres), mirroring style:

```bash
# ============================================================
echo ""
echo "=== Narrators ==="
# ============================================================
output=$($CLI narrators list 2>&1)
assert_json_key "narrators list returns JSON" "narrators" "$output"
assert_json_expr "narrators list non-empty" "len(d['narrators'])>0" "$output"
assert_json_expr "narrators list items have name+numBooks" "'name' in d['narrators'][0] and 'numBooks' in d['narrators'][0]" "$output"

# rename roundtrip on the throwaway narrator
output=$($CLI narrators rename smoke-temp-narrator smoke-temp-narrator-renamed 2>&1)
assert_json_key "narrators rename returns updated" "updated" "$output"
output=$($CLI narrators rename smoke-temp-narrator-renamed smoke-temp-narrator 2>&1)
assert_json_key "narrators rename back returns updated" "updated" "$output"

# delete the throwaway narrator
output=$($CLI narrators delete smoke-temp-narrator 2>&1)
assert_json_key "narrators delete returns updated" "updated" "$output"
```

- [ ] **Step 5: 403 assertions (new features + tags/genres backfill)**

Find the permission-denial area (search for `abs_login readonlyuser readonlypass` and the "backup list as testuser" block). Add, in the `readonlyuser` (update-denial) group:

```bash
error_output=$($CLI series update --id "$SERIES_ID" --description x 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "series update as readonlyuser hits 'update' permission denial"
else
    fail "series update as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi
error_output=$($CLI narrators rename smoke-temp-narrator whatever 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "narrators rename as readonlyuser hits 'update' permission denial"
else
    fail "narrators rename as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi
```

And in the `testuser` (admin-denial) group (near "backup list as testuser"), backfill tags/genres:

```bash
error_output=$($CLI tags list 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied\|admin"; then
    pass "tags list as testuser shows admin permission denied"
else
    fail "tags list as testuser shows admin permission denied" "got: ${error_output:0:200}"
fi
error_output=$($CLI genres list 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied\|admin"; then
    pass "genres list as testuser shows admin permission denied"
else
    fail "genres list as testuser shows admin permission denied" "got: ${error_output:0:200}"
fi
```

Ensure `$SERIES_ID` is in scope where the readonlyuser block runs (it is a shell global once set in Step 3; if the denial block runs before Step 3, re-fetch it as root before switching users). Ensure the script re-logins as `root` after the denial blocks (the existing script already does `abs_login root root` at the end of that section — keep that).

- [ ] **Step 6: Run the smoke test**

Per CLAUDE.md, against the compose stack:
```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: all pass, exit 0. Only mark "smoke passed" after seeing it.

- [ ] **Step 7: Commit**

```bash
git add docker/seed.sh docker/smoke-test.sh
git commit -m "test: add series update + narrator smoke assertions and 403 coverage"
```

---

## Task 9: Full verification

- [ ] **Step 1: Full unit test run**

Run: `dotnet test AbsCli.sln`
Expected: all pass (including `ResponseExamplesDriftTest`, `PermissionSectionTests`, new suites).

- [ ] **Step 2: Format check (matches CI)**

Run: `dotnet format AbsCli.sln --verify-no-changes`
Expected: no changes. If it fails, `dotnet format AbsCli.sln`, commit as `chore: fix formatting`.

- [ ] **Step 3: Confirm wiring**

Run: `dotnet run --project src/AbsCli -- --help`
Expected: `narrators` appears. `dotnet run --project src/AbsCli -- series --help` shows `update`.

- [ ] **Step 4: Smoke gate**

Confirm the live smoke test in Task 8 passed. This gates the PR checkbox — do not check it unverified.

---

## Self-Review Notes (author checklist — completed during planning)

- **Spec coverage:** series update (Task 6), narrators list/rename/delete (Tasks 1-5), permission tags (narrators rename/delete = update, list none; series update = update), base64 narrator param (Task 1), no-merge series help + empty-name rejection (Task 6), narrator delete-needs-update help (Task 5), README + coverage-doc fix incl. DELETE-narrator permission correction (Task 7), seed narrators + smoke incl. new 403s AND tags/genres 403 backfill (Task 8), generator exclusion (Task 3). YAGNI exclusions honored.
- **Generator coupling:** Task 3 handles `ResponseExamples.g.cs` drift for the new response types + the excluded request type.
- **Type consistency:** `NarratorItem`/`NarratorListResponse`/`NarratorUpdateResponse`/`NarratorRenameRequest` and `AppJsonContext.Default.*` accessors match across models/service/command/tests; `BuildUpdateBodyForTesting(name, description)` signature consistent between Task 6 impl and its test.
- **CHANGELOG untouched** (release-owned).
